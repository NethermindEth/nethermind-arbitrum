"""Worker pool and work queue for parallel test execution.

This module provides thread-safe pool management and work distribution
for running tests across multiple Nethermind instances.

Features:
- InstancePool with context manager support (typing.Self)
- Thread-safe WorkQueue wrapping queue.Queue
- Automatic port allocation with validation
- Health checking and restart capability
"""

from __future__ import annotations

import contextlib
import logging
import queue
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import TYPE_CHECKING, Final, Self

from .config import DEFAULT_BASE_PORT, MAX_PORT, MIN_PORT, PortAllocator
from .exceptions import ConfigurationError, WorkerPoolError
from .models import WorkerInstance
from .process_manager import (
    DEFAULT_CONFIG_NAME,
    NethermindProcess,
    allocate_port,
)

if TYPE_CHECKING:
    pass


DEFAULT_STARTUP_TIMEOUT_S: Final[int] = 120
"""Default timeout for worker startup in seconds."""

DEFAULT_GRACE_PERIOD_S: Final[int] = 10
"""Default seconds to wait for graceful shutdown."""


class WorkQueue:
    """Thread-safe work queue for distributing tests to workers.

    Wraps queue.Queue with a simpler interface for the parallel test runner.
    Workers call get() to retrieve the next test, returning None when empty.

    Example:
        >>> wq = WorkQueue(["TestA", "TestB", "TestC"])
        >>> while (test := wq.get()) is not None:
        ...     run_test(test)
        ...     wq.mark_done()
    """

    def __init__(self, tests: list[str]) -> None:
        """Initialize the queue with test names.

        Args:
            tests: List of test names to execute
        """
        self._queue: queue.Queue[str] = queue.Queue()
        self._total = len(tests)
        # Sort tests alphabetically for deterministic execution order
        # This helps identify state leakage between specific test pairs
        for test in sorted(tests):
            self._queue.put(test)

    def get(self) -> str | None:
        """Get the next test or None if the queue is empty.

        Non-blocking - returns immediately with None if no tests are available.

        Returns:
            Test name or None if the queue is empty
        """
        try:
            return self._queue.get_nowait()
        except queue.Empty:
            return None

    def mark_done(self) -> None:
        """Mark the current test as done (for queue.join() support)."""
        self._queue.task_done()

    def remaining(self) -> int:
        """Return an approximate number of remaining tests."""
        return self._queue.qsize()

    def total(self) -> int:
        """Return the total number of tests originally queued."""
        return self._total

    def is_empty(self) -> bool:
        """Return True if queue is empty."""
        return self._queue.empty()

    def join(self) -> None:
        """Block until all items have been processed."""
        self._queue.join()


@dataclass
class PoolConfig:
    """Configuration for InstancePool.

    Validates port ranges and worker counts on creation.
    """

    max_workers: int
    base_port: int = DEFAULT_BASE_PORT
    host: str = "127.0.0.1"
    config_name: str = DEFAULT_CONFIG_NAME

    def __post_init__(self) -> None:
        """Validate configuration."""
        if self.max_workers < 1:
            raise ConfigurationError(
                f"max_workers must be >= 1, got {self.max_workers}",
                field="max_workers",
            )
        if self.base_port < MIN_PORT:
            raise ConfigurationError(
                f"base_port {self.base_port} is privileged (< {MIN_PORT})",
                field="base_port",
            )
        if self.base_port > MAX_PORT:
            raise ConfigurationError(
                f"base_port {self.base_port} exceeds maximum {MAX_PORT}",
                field="base_port",
            )
        # Check that all worker ports would be valid (sequential: base_port + worker_id)
        # Reserve some headroom for overflow ports during crash recovery
        overflow_buffer = 100  # Room for ~100 restarts before port exhaustion
        last_worker_port = self.base_port + self.max_workers - 1
        last_possible_port = last_worker_port + overflow_buffer
        if last_possible_port > MAX_PORT:
            raise ConfigurationError(
                f"Port range overflow: workers use ports {self.base_port}-{last_worker_port}, "
                f"plus {overflow_buffer} overflow buffer would exceed {MAX_PORT}. "
                f"Reduce max_workers or lower base_port.",
                field="base_port",
            )


class InstancePool:
    """Manages a pool of Nethermind instances for parallel test execution.

    Provides on-demand instance creation, thread-safe access, automatic restart
    capability, and graceful shutdown of all instances.

    Thread Safety:
        The pool is designed for a ThreadPoolExecutor pattern where each worker
        thread exclusively owns a single worker_id. The pool's lock protects
        the instances dictionary but does not serialize operations on individual
        instances.

    Usage:
        >>> with InstancePool(config, log_dir, build_dir) as pool:
        ...     pool.start_worker(0)
        ...     # ... run tests ...
        ... # shutdown_all() called automatically

    Uses typing.Self for proper __enter__ return type.
    """

    def __init__(
        self,
        config: PoolConfig,
        log_dir: Path | None = None,
        build_dir: Path | None = None,
        root_dir: Path | None = None,
        env: dict[str, str] | None = None,
    ) -> None:
        """Initialize the instance pool.

        Args:
            config: Pool configuration (workers, ports, etc.)
            log_dir: Directory for log files (None disables logging)
            build_dir: Directory containing nethermind.dll
            root_dir: Root directory for data directories
            env: Environment variables for Nethermind processes
        """
        self.config = config
        self.log_dir = log_dir
        self.build_dir = build_dir
        self.root_dir = root_dir or Path.cwd()
        self.env = env

        self._instances: dict[int, WorkerInstance] = {}
        self._processes: dict[int, NethermindProcess] = {}
        self._lock = threading.Lock()
        self._port_allocator = PortAllocator(config.base_port, config.max_workers)

    def get_or_create(self, worker_id: int) -> WorkerInstance:
        """Get an existing instance or create a new one for a worker.

        Thread-safe lazy initialization - instance is created only on the first
        request for a given worker_id.

        Args:
            worker_id: Worker ID (0 to max_workers-1)

        Returns:
            WorkerInstance for the worker

        Raises:
            ValueError: If worker_id is out of range
        """
        if worker_id < 0 or worker_id >= self.config.max_workers:
            raise ValueError(
                f"worker_id must be in range [0, {self.config.max_workers}), got {worker_id}"
            )

        with self._lock:
            if worker_id not in self._instances:
                port = allocate_port(worker_id, self.config.base_port)
                instance = WorkerInstance(
                    worker_id=worker_id,
                    nethermind_port=port,
                    data_dir=self.root_dir / f".data-worker-{worker_id}",
                )
                self._instances[worker_id] = instance

                # Create a corresponding process manager
                self._processes[worker_id] = NethermindProcess(
                    port=port,
                    data_dir=instance.data_dir or self.root_dir / f".data-worker-{worker_id}",
                    host=self.config.host,
                    config_name=self.config.config_name,
                )

            return self._instances[worker_id]

    def start_worker(
        self,
        worker_id: int,
        timeout_s: int = DEFAULT_STARTUP_TIMEOUT_S,
    ) -> bool:
        """Start a worker instance and wait for it to be ready.

        Args:
            worker_id: Worker ID to start
            timeout_s: Seconds to wait for RPC availability

        Returns:
            True if instance started and is healthy

        Raises:
            WorkerPoolError: If build_dir is not configured
        """
        if self.build_dir is None:
            raise WorkerPoolError(
                "Cannot start worker: build_dir not configured",
                active_workers=self.active_count(),
                total_workers=self.config.max_workers,
            )

        _ = self.get_or_create(worker_id)  # Ensure instance exists
        process = self._processes[worker_id]

        # Already running and healthy
        if process.is_healthy:
            return True

        # Setup log path
        log_path = None
        if self.log_dir:
            log_path = self.log_dir / f"nethermind-worker-{worker_id}.log"

        try:
            process.start(self.build_dir, log_path, self.env)
            ready = process.wait_for_ready(timeout_s)
            if not ready:
                # Check if process died early
                if process.proc and process.proc.poll() is not None:
                    exit_code = process.proc.returncode
                    logging.getLogger(__name__).error(
                        f"Worker {worker_id}: Nethermind exited with code {exit_code}"
                    )
                else:
                    logging.getLogger(__name__).error(
                        f"Worker {worker_id}: RPC not available after {timeout_s}s"
                    )
            return ready
        except Exception as e:
            logging.getLogger(__name__).error(
                f"Worker {worker_id}: Failed to start - {type(e).__name__}: {e}"
            )
            return False

    def restart_worker(
        self,
        worker_id: int,
        timeout_s: int = DEFAULT_STARTUP_TIMEOUT_S,
        grace_s: int = DEFAULT_GRACE_PERIOD_S,
    ) -> bool:
        """Restart a failed or unhealthy worker instance.

        If the original port is in TIME_WAIT, allocates an overflow port
        beyond the initial worker range.

        Args:
            worker_id: Worker ID to restart
            timeout_s: Seconds to wait for RPC availability
            grace_s: Seconds for graceful shutdown

        Returns:
            True if instance restarted and is healthy
        """
        if self.build_dir is None:
            return False

        with self._lock:
            process = self._processes.get(worker_id)
            instance = self._instances.get(worker_id)
            if not process or not instance:
                return False

        # Stop the old process
        process.stop(grace_s)

        # Try the original port first, fall back to overflow port
        import socket

        original_port = instance.nethermind_port
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.bind(("127.0.0.1", original_port))
            new_port = original_port
        except OSError:
            # Original port in TIME_WAIT, get overflow port
            new_port = self._port_allocator.overflow_port()
            logging.getLogger(__name__).info(
                f"Worker {worker_id}: Original port {original_port} unavailable, "
                f"using overflow port {new_port}"
            )

        # Update instance and process with new port
        with self._lock:
            instance.nethermind_port = new_port
            self._processes[worker_id] = NethermindProcess(
                port=new_port,
                data_dir=instance.data_dir or self.root_dir / f".data-worker-{worker_id}",
                host=self.config.host,
                config_name=self.config.config_name,
            )
            process = self._processes[worker_id]

        log_path = None
        if self.log_dir:
            log_path = self.log_dir / f"nethermind-worker-{worker_id}.log"

        try:
            process.start(self.build_dir, log_path, self.env)
            return process.wait_for_ready(timeout_s)
        except Exception as e:
            logging.getLogger(__name__).error(
                f"Worker {worker_id}: Failed to restart - {type(e).__name__}: {e}"
            )
            return False

    def check_health(self, worker_id: int) -> bool:
        """Check if a worker instance is healthy and responding.

        Args:
            worker_id: Worker ID to check

        Returns:
            True if an instance process is running and healthy
        """
        with self._lock:
            process = self._processes.get(worker_id)
            if not process:
                return False
            return process.is_healthy

    def get_port(self, worker_id: int) -> int:
        """Get the port for a worker.

        Returns the stored port from the worker instance, not a re-probed port.
        This ensures consistency - the port returned is the one actually in use.

        Args:
            worker_id: Worker ID

        Returns:
            Port number for this worker

        Raises:
            RuntimeError: If worker has not been initialized via get_or_create()
        """
        with self._lock:
            if worker_id not in self._instances:
                raise RuntimeError(
                    f"Worker {worker_id} not initialized. Call get_or_create() first."
                )
            return self._instances[worker_id].nethermind_port

    def shutdown_all(self, grace_s: int = DEFAULT_GRACE_PERIOD_S) -> None:
        """Stop all instances gracefully.

        Args:
            grace_s: Seconds to wait for graceful shutdown per instance
        """
        with self._lock:
            processes_to_stop = list(self._processes.values())

        # Stop outside lock to allow concurrent shutdown
        for process in processes_to_stop:
            with contextlib.suppress(Exception):
                process.stop(grace_s)

        with self._lock:
            self._instances.clear()
            self._processes.clear()

    def active_count(self) -> int:
        """Return a number of currently active (running) instances."""
        with self._lock:
            return sum(1 for p in self._processes.values() if p.is_running)

    def healthy_count(self) -> int:
        """Return a number of healthy instances."""
        with self._lock:
            return sum(1 for p in self._processes.values() if p.is_healthy)

    def __len__(self) -> int:
        """Return the number of created instances (may not all be running)."""
        with self._lock:
            return len(self._instances)

    def __enter__(self) -> Self:
        """Enter context manager."""
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: object,
    ) -> None:
        """Exit context manager - ensures shutdown_all() is called."""
        self.shutdown_all()
