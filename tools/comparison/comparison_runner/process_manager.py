"""Nethermind process lifecycle management.

This module provides classes and functions for starting, stopping,
and monitoring Nethermind instances for comparison testing.

Features:
- Clean process group handling with start_new_session=True
- Graceful shutdown with SIGTERM, fallback to SIGKILL
- Health checking via socket connection
- Restart capability for crashed instances
"""

from __future__ import annotations

import contextlib
import os
import signal
import socket
import subprocess
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import TYPE_CHECKING, Final, TextIO

from .config import DEBUG_PORT_OFFSET, DEFAULT_STARTUP_TIMEOUT_S
from .exceptions import NethermindStartupError, ProcessTerminationError

if TYPE_CHECKING:
    pass


DEFAULT_GRACE_PERIOD_S: Final[int] = 10
"""Default seconds to wait for graceful shutdown before SIGKILL."""

DEFAULT_CONFIG_NAME: Final[str] = "arbitrum-system-test"
"""Default Nethermind configuration name."""

HEALTH_CHECK_INTERVAL_S: Final[float] = 1.0
"""Interval between health check attempts."""


MAX_PORT_PROBE_ATTEMPTS: Final[int] = 100
"""Maximum ports to try when finding an available port."""


def find_available_port(
    start_port: int,
    host: str = "127.0.0.1",
    max_attempts: int = MAX_PORT_PROBE_ATTEMPTS,
) -> int:
    """Find an available port starting from start_port.

    Probes ports by attempting to bind, which detects:
    - Ports in use by other processes
    - Ports in TIME_WAIT state (can't bind without SO_REUSEADDR)

    This avoids TIME_WAIT conflicts without requiring C# changes.
    There's a small race window between probe and actual use, but
    it's practical for test runners.

    Args:
        start_port: Port to start searching from
        host: Host to bind to for availability check
        max_attempts: Maximum ports to try before giving up

    Returns:
        First available port found

    Raises:
        RuntimeError: If no available port found within max_attempts
    """
    from .config import MAX_PORT

    for offset in range(max_attempts):
        port = start_port + offset
        if port > MAX_PORT:
            break
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                # Don't use SO_REUSEADDR - we want to detect TIME_WAIT ports
                s.bind((host, port))
                return port
        except OSError:
            continue

    raise RuntimeError(
        f"No available port found in range {start_port}-{start_port + max_attempts - 1}"
    )


def allocate_port(worker_id: int, base_port: int = 20551) -> int:
    """Allocate port for worker using sequential assignment.

    Simple sequential allocation: worker N gets base_port + N.
    This is deterministic and sufficient for the normal case where
    Nethermind instances start once and run until all tests complete.

    For crash recovery (when a port is in TIME_WAIT), use PortAllocator.overflow_port()
    to get a fresh port beyond the initial range.

    Args:
        worker_id: Zero-based worker index
        base_port: Starting port for worker 0

    Returns:
        Port number for this worker (base_port + worker_id)
    """
    return base_port + worker_id


def start_nethermind(
    port: int,
    data_dir: Path,
    build_dir: Path,
    log_file: TextIO | None = None,
    config_name: str = DEFAULT_CONFIG_NAME,
    env: dict[str, str] | None = None,
) -> subprocess.Popen[str]:
    """Start a Nethermind process.

    Args:
        port: RPC port for this instance
        data_dir: Directory for Nethermind data (each worker needs a unique dir)
        build_dir: Directory containing nethermind.dll
        log_file: File handle for stdout/stderr (None for /dev/null)
        config_name: Nethermind configuration name
        env: Environment variables (uses os.environ if None)

    Returns:
        Started subprocess.Popen instance

    Raises:
        NethermindStartupError: If a process fails to start
    """
    if env is None:
        env = os.environ.copy()

    # Port allocation per worker to ensure complete isolation:
    # - Main RPC port: base port (20551, 20552, ...)
    # - Debug port: base + 8000 (28551, 28552, ...)
    # - Engine port: base + 9000 (29551, 29552, ...) - disabled for Arbitrum but must be unique
    # - P2P port: base + 10000 (30551, 30552, ...) - disabled but must be unique if ever enabled
    # - Discovery port: base + 10000 (same as P2P, standard practice)
    debug_port = port + DEBUG_PORT_OFFSET
    engine_port = port + 9000
    p2p_port = port + 10000

    cmd = [
        "dotnet",
        "nethermind.dll",
        "-c",
        config_name,
        "--data-dir",
        str(data_dir),
        "--JsonRpc.UnsecureDevNoRpcAuthentication=true",
        f"--JsonRpc.Port={port}",
        # Engine API port - must be unique per worker even if not used (Merge.Enabled=true binds it)
        f"--JsonRpc.EnginePort={engine_port}",
        # Override AdditionalRpcUrls completely (replaces config file array)
        f"--JsonRpc.AdditionalRpcUrls=http://localhost:{port}|http|nitroexecution,http://localhost:{debug_port}|http|debug",
        # Disable P2P networking entirely - not needed for comparison testing
        "--Init.PeerManagerEnabled=false",
        "--Network.MaxActivePeers=0",
        "--Network.OnlyStaticPeers=true",
        # Set unique ports anyway in case any component still tries to bind
        f"--Network.P2PPort={p2p_port}",
        f"--Network.DiscoveryPort={p2p_port}",
        f"--Init.LogFileName={config_name}-{port}.log",  # Unique log per worker
        "--log",
        "debug",
        # Enable MultiGas exposure in receipts for comparison testing
        "--Arbitrum.ExposeMultiGas=true",
    ]

    stdout_target: int | TextIO = log_file if log_file else subprocess.DEVNULL

    try:
        proc = subprocess.Popen(
            cmd,
            cwd=str(build_dir),
            stdout=stdout_target,
            stderr=subprocess.STDOUT,
            text=True,
            env=env,
            start_new_session=True,  # Thread-safe; creates new process group
        )
        return proc
    except OSError as e:
        raise NethermindStartupError(
            f"Failed to start Nethermind: {e}",
            port=port,
        ) from e


def stop_nethermind(
    proc: subprocess.Popen[str],
    grace_s: int = DEFAULT_GRACE_PERIOD_S,
) -> bool:
    """Stop Nethermind gracefully with SIGTERM, then SIGKILL fallback.

    Args:
        proc: Popen instance to stop
        grace_s: Seconds to wait before SIGKILL

    Returns:
        True if a process stopped cleanly, False if force-killed

    Raises:
        ProcessTerminationError: If a process cannot be terminated
    """
    if proc.poll() is not None:
        return True  # Already dead

    # Send SIGTERM to a process group
    try:
        os.killpg(proc.pid, signal.SIGTERM)
    except ProcessLookupError:
        return True  # Process already gone

    # Wait for a graceful shutdown
    deadline = time.time() + grace_s
    while time.time() < deadline:
        if proc.poll() is not None:
            return True
        time.sleep(0.5)

    # Force kill
    try:
        os.killpg(proc.pid, signal.SIGKILL)
        proc.wait(timeout=5)
        return False
    except ProcessLookupError:
        return True
    except subprocess.TimeoutExpired as e:
        raise ProcessTerminationError(
            f"Process {proc.pid} did not terminate after SIGKILL",
            pid=proc.pid,
            signal_sent="SIGKILL",
        ) from e


def wait_for_ready(
    host: str,
    port: int,
    proc: subprocess.Popen[str] | None = None,
    timeout_s: int = DEFAULT_STARTUP_TIMEOUT_S,
) -> bool:
    """Wait for Nethermind RPC to become available.

    Args:
        host: RPC host address
        port: RPC port
        proc: Optional process to check for early termination
        timeout_s: Maximum seconds to wait

    Returns:
        True if RPC is available, False on timeout or process death
    """
    deadline = time.time() + timeout_s

    while time.time() < deadline:
        # Check if a process died early
        if proc is not None and proc.poll() is not None:
            return False

        try:
            with socket.create_connection((host, port), timeout=1.0):
                return True
        except OSError:
            time.sleep(HEALTH_CHECK_INTERVAL_S)

    return False


def is_process_healthy(proc: subprocess.Popen[str] | None) -> bool:
    """Check if a process is running.

    Args:
        proc: Process to check

    Returns:
        True if a process is running
    """
    if proc is None:
        return False
    return proc.poll() is None


@dataclass
class NethermindProcess:
    """Manages the lifecycle of a single Nethermind instance.

    Provides start, stop, restart, and health check capabilities.
    Uses process groups for clean termination.

    Example:
        >>> nm = NethermindProcess(port=20551, data_dir=Path(".data"))
        >>> nm.start(build_dir)
        >>> nm.wait_for_ready()
        >>> # ... run tests ...
        >>> nm.stop()
    """

    port: int
    data_dir: Path
    host: str = "127.0.0.1"
    config_name: str = DEFAULT_CONFIG_NAME
    proc: subprocess.Popen[str] | None = field(default=None, repr=False)
    log_file: TextIO | None = field(default=None, repr=False)
    _is_healthy: bool = field(default=False, repr=False)

    def start(
        self,
        build_dir: Path,
        log_path: Path | None = None,
        env: dict[str, str] | None = None,
    ) -> None:
        """Start the Nethermind process.

        Args:
            build_dir: Directory containing nethermind.dll
            log_path: Optional path for log file
            env: Environment variables

        Raises:
            NethermindStartupError: If a process fails to start
        """
        if self.is_running:
            return

        # Setup log file
        if log_path:
            log_path.parent.mkdir(parents=True, exist_ok=True)
            self.log_file = log_path.open("w", encoding="utf-8")
        else:
            self.log_file = None

        # Ensure data directory exists
        self.data_dir.mkdir(parents=True, exist_ok=True)

        try:
            self.proc = start_nethermind(
                port=self.port,
                data_dir=self.data_dir,
                build_dir=build_dir,
                log_file=self.log_file,
                config_name=self.config_name,
                env=env,
            )
        except NethermindStartupError:
            self._cleanup_log_file()
            raise

    def stop(self, grace_s: int = DEFAULT_GRACE_PERIOD_S) -> bool:
        """Stop the Nethermind process.

        Args:
            grace_s: Seconds for graceful shutdown

        Returns:
            True if stopped cleanly
        """
        self._is_healthy = False

        if self.proc is None:
            self._cleanup_log_file()
            return True

        try:
            result = stop_nethermind(self.proc, grace_s)
        finally:
            self.proc = None
            self._cleanup_log_file()

        return result

    def wait_for_ready(self, timeout_s: int = DEFAULT_STARTUP_TIMEOUT_S) -> bool:
        """Wait for RPC to become available.

        Args:
            timeout_s: Maximum seconds to wait

        Returns:
            True if ready, False on timeout
        """
        ready = wait_for_ready(
            host=self.host,
            port=self.port,
            proc=self.proc,
            timeout_s=timeout_s,
        )
        self._is_healthy = ready
        return ready

    def restart(
        self,
        build_dir: Path,
        log_path: Path | None = None,
        env: dict[str, str] | None = None,
        grace_s: int = DEFAULT_GRACE_PERIOD_S,
        timeout_s: int = DEFAULT_STARTUP_TIMEOUT_S,
    ) -> bool:
        """Restart the Nethermind process.

        Args:
            build_dir: Directory containing nethermind.dll
            log_path: Optional path for log file
            env: Environment variables
            grace_s: Seconds for graceful shutdown
            timeout_s: Seconds to wait for startup

        Returns:
            True if restart successful and healthy
        """
        self.stop(grace_s)
        self.start(build_dir, log_path, env)
        return self.wait_for_ready(timeout_s)

    @property
    def is_running(self) -> bool:
        """Check if a process is running."""
        return is_process_healthy(self.proc)

    @property
    def is_healthy(self) -> bool:
        """Check if a process is running and was marked healthy."""
        return self._is_healthy and self.is_running

    def _cleanup_log_file(self) -> None:
        """Close and clean up a log file handle."""
        if self.log_file is not None:
            with contextlib.suppress(Exception):
                self.log_file.close()
            self.log_file = None
