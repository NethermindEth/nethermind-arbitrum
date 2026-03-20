"""Parallel test runner for comparison testing.

This module provides a ParallelRunner class that orchestrates test execution
using multiple Nethermind instances via ThreadPoolExecutor. Tests are
distributed dynamically across workers using a thread-safe work queue.

Features:
- Composition over inheritance (uses InstancePool, WorkQueue)
- Small focused methods (<50 lines each)
- Thread-safe result collection
- Clean shutdown on interruption
- Unified TestResult return type
"""

from __future__ import annotations

import logging
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
from pathlib import Path
from threading import Event

from .config import DEBUG_PORT_OFFSET, DEFAULT_BASE_PORT, DEFAULT_STARTUP_TIMEOUT_S
from .exceptions import RPCError
from .models import TestResult, TestStatus
from .rpc_client import reinitialize_state
from .test_executor import clean_test_cache, compile_test_binary, execute_test
from .worker_pool import InstancePool, PoolConfig, WorkQueue


@dataclass
class ParallelRunnerConfig:
    """Configuration for parallel runner."""

    nitro_path: Path
    nethermind_build_dir: Path
    max_workers: int
    host: str = "127.0.0.1"
    base_port: int = DEFAULT_BASE_PORT
    root_dir: Path | None = None
    log_dir: Path | None = None
    startup_timeout_s: int = DEFAULT_STARTUP_TIMEOUT_S
    max_retries: int = 0


class ParallelRunner:
    """Orchestrates parallel test execution using ThreadPoolExecutor.

    Uses composition to combine InstancePool, WorkQueue, reinitialize_state,
    and execute_test into a cohesive parallel runner. This replaces the
    monolithic ParallelTestRunner class with a focused, maintainable implementation.

    Thread Safety:
        - WorkQueue is thread-safe (uses queue.Queue internally)
        - A results list is protected by _results_lock
        - Each worker exclusively owns its worker_id

    Example:
        >>> config = ParallelRunnerConfig(
        ...     nitro_path=Path("/path/to/nitro"),
        ...     nethermind_build_dir=Path("/path/to/nethermind/build"),
        ...     max_workers=4,
        ... )
        >>> runner = ParallelRunner(config)
        >>> results = runner.run(["TestA", "TestB", "TestC"])
    """

    def __init__(self, config: ParallelRunnerConfig) -> None:
        """Initialize the parallel runner.

        Args:
            config: Runner configuration
        """
        self.config = config
        self.logger = logging.getLogger(__name__)

        # Thread-safe result collection
        self._results: list[TestResult] = []
        self._results_lock = threading.Lock()

        # Progress tracking
        self._total_tests = 0
        self._completed_count = 0

        # Interruption handling
        self.interrupted = Event()

        # Pool will be created in run()
        self._pool: InstancePool | None = None

        # Pre-compiled test binary (set in run())
        self._test_binary: Path | None = None

    def run(self, tests: list[str]) -> list[TestResult]:
        """Run tests in parallel using ThreadPoolExecutor.

        Args:
            tests: List of test names to execute

        Returns:
            List of TestResult for all tests
        """
        if not tests:
            return []

        # Cap workers at number of tests
        num_workers = min(self.config.max_workers, len(tests))

        self.logger.info(f"Starting parallel execution: {len(tests)} tests, {num_workers} workers")

        # Reset state
        self._results = []
        self._total_tests = len(tests)
        self._completed_count = 0
        self.interrupted.clear()

        # Clean Go test cache once before starting any tests
        # This avoids race conditions when multiple workers would clean concurrently
        clean_test_cache(self.config.nitro_path)

        # Pre-compile Go test binary once (separates compilation from test timing)
        self._test_binary = compile_test_binary(self.config.nitro_path)

        # Create pool configuration
        pool_config = PoolConfig(
            max_workers=num_workers,
            base_port=self.config.base_port,
            host=self.config.host,
        )

        # Create an instance pool with a context manager for cleanup
        self._pool = InstancePool(
            config=pool_config,
            log_dir=self.config.log_dir,
            build_dir=self.config.nethermind_build_dir,
            root_dir=self.config.root_dir or Path.cwd(),
        )

        work_queue = WorkQueue(tests)

        try:
            with self._pool:
                self._run_workers(num_workers, work_queue)
        except Exception as e:
            self.logger.error(f"Parallel execution failed: {e}")
            raise  # Re-raise so the caller knows about pool failures

        return self._results

    def stop(self) -> None:
        """Signal all workers to stop (for external interrupt handling)."""
        self.interrupted.set()

    def _run_workers(self, num_workers: int, work_queue: WorkQueue) -> None:
        """Launch worker threads and wait for completion.

        Args:
            num_workers: Number of worker threads
            work_queue: Work queue with tests
        """
        with ThreadPoolExecutor(max_workers=num_workers) as executor:
            futures = [
                executor.submit(self._worker_loop, worker_id, work_queue)
                for worker_id in range(num_workers)
            ]

            # Wait for all workers
            for future in futures:
                try:
                    future.result()
                except Exception as e:
                    self.logger.error(f"Worker exception: {e}")

    def _worker_loop(self, worker_id: int, work_queue: WorkQueue) -> None:
        """Worker loop: start instance, process tests until queue empty.

        Args:
            worker_id: Unique worker ID (0 to num_workers-1)
            work_queue: Shared work queue
        """
        if self._pool is None:
            raise RuntimeError("Worker pool not initialized")

        # Start the worker's Nethermind instance
        if not self._pool.start_worker(worker_id, self.config.startup_timeout_s):
            self.logger.error(f"Worker {worker_id}: Failed to start instance")
            return

        instance = self._pool.get_or_create(worker_id)
        port = instance.nethermind_port
        self.logger.info(f"Worker {worker_id}: Started on port {port}")

        max_attempts = self.config.max_retries + 1

        while not self.interrupted.is_set():
            # Get the next test
            test_name = work_queue.get()
            if test_name is None:
                break  # Queue empty

            try:
                self.logger.debug(f"Worker {worker_id}: Running {test_name}")
                result = self._run_with_retries(worker_id, port, test_name, max_attempts)
                self._record_result(result)
                # Check health and restart if needed - if worker is dead, stop this worker
                if not self._handle_health_check(worker_id):
                    self.logger.error(f"Worker {worker_id}: Stopping due to unrecoverable failure")
                    break  # Exit worker loop, but other workers continue
            finally:
                work_queue.mark_done()

        self.logger.info(f"Worker {worker_id}: Finished")

    def _run_with_retries(
        self,
        worker_id: int,
        port: int,
        test_name: str,
        max_attempts: int,
    ) -> TestResult:
        """Run a test with retries on failure.

        Returns the final TestResult with attempt tracking.
        Duration accumulates across all attempts.
        Checks interruption and worker health between attempts.
        """
        total_duration = 0.0
        result = TestResult(name=test_name, status=TestStatus.FAILED, worker_id=worker_id)

        for attempt in range(1, max_attempts + 1):
            if self.interrupted.is_set():
                result.attempt = attempt
                result.max_attempts = max_attempts
                return result

            result = self._run_single_test(worker_id, port, test_name)
            total_duration += result.duration_s
            result.attempt = attempt
            result.max_attempts = max_attempts
            result.duration_s = total_duration

            if result.status == TestStatus.PASSED:
                if attempt > 1:
                    self.logger.info(
                        f"  \u21b3 [W{worker_id}] {test_name}: PASSED on attempt "
                        f"{attempt}/{max_attempts} (flaky)"
                    )
                return result

            if attempt < max_attempts:
                self.logger.warning(
                    f"  \u21b3 [W{worker_id}] {test_name}: FAILED attempt "
                    f"{attempt}/{max_attempts}, retrying..."
                )
                # Check worker health before retrying — restart if needed
                if not self._handle_health_check(worker_id):
                    self.logger.error(
                        f"  \u21b3 [W{worker_id}] {test_name}: Worker unhealthy, "
                        f"cannot retry"
                    )
                    return result
                # Update port in case worker was restarted on a new port
                if self._pool is not None:
                    instance = self._pool.get_or_create(worker_id)
                    port = instance.nethermind_port
            else:
                self.logger.error(
                    f"  \u21b3 [W{worker_id}] {test_name}: FAILED all {max_attempts} attempts"
                )

        return result

    def _run_single_test(
        self,
        worker_id: int,
        port: int,
        test_name: str,
    ) -> TestResult:
        """Run a single test on a specific worker.

        Args:
            worker_id: Worker ID for logging
            port: Nethermind port
            test_name: Name of the test

        Returns:
            TestResult with an outcome
        """
        start_time = time.time()

        # Reinitialize state via RPC
        try:
            reinitialize_state(
                host=self.config.host,
                port=port + DEBUG_PORT_OFFSET,
                test_name=test_name,
            )
        except RPCError as e:
            return TestResult(
                name=test_name,
                status=TestStatus.FAILED,
                duration_s=time.time() - start_time,
                error_msg=f"reinitialize failed: {e}",
                worker_id=worker_id,
            )

        # Execute the test
        return execute_test(
            test_name=test_name,
            nitro_path=self.config.nitro_path,
            nethermind_host=self.config.host,
            nethermind_port=port,
            log_dir=self.config.log_dir,
            worker_id=worker_id,
            interrupted=self.interrupted,
            test_binary=self._test_binary,
        )

    def _record_result(self, result: TestResult) -> None:
        """Thread-safe result recording with logging.

        Args:
            result: TestResult to record
        """
        with self._results_lock:
            self._results.append(result)
            self._completed_count += 1
            completed = self._completed_count
            total = self._total_tests

        # Calculate progress percentage
        pct = (completed / total * 100) if total > 0 else 0

        # Format: [123/387 32%] [W2] TestName: PASSED (1.2s)
        duration_str = f"({result.duration_s:.1f}s)" if result.duration_s else ""
        worker_str = f"[W{result.worker_id}]" if result.worker_id >= 0 else ""
        status_icon = "✓" if result.status == TestStatus.PASSED else "✗"

        msg = (
            f"[{completed}/{total} {pct:>3.0f}%] {worker_str} "
            f"{result.name}: {status_icon} {duration_str}"
        )

        if result.status == TestStatus.PASSED:
            self.logger.info(msg)
        else:
            self.logger.error(msg)

    def _handle_health_check(self, worker_id: int) -> bool:
        """Check worker health and restart if needed.

        Args:
            worker_id: Worker to check

        Returns:
            True if worker is healthy or was restarted successfully,
            False if worker is dead and should stop processing
        """
        if self._pool is None:
            return False  # Pool isn't initialized

        if self._pool.check_health(worker_id):
            return True  # Worker is healthy

        # Worker is unhealthy - try to restart with retries
        max_retries = 3
        for attempt in range(1, max_retries + 1):
            self.logger.warning(
                f"Worker {worker_id}: Unhealthy, restart {attempt}/{max_retries}..."
            )
            if self._pool.restart_worker(worker_id, self.config.startup_timeout_s):
                self.logger.info(f"Worker {worker_id}: Restarted successfully")
                return True
            # Brief pause before retry
            time.sleep(1)

        # All retries failed - mark this worker as dead but DON'T stop other workers
        self.logger.error(
            f"Worker {worker_id}: Failed to restart after {max_retries} attempts. "
            f"Worker will stop, but other workers continue."
        )
        return False  # Signal this worker to stop
