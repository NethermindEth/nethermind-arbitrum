"""Base process class with common lifecycle management.

All managed processes (Nitro EL, Nitro CL, Nethermind) inherit from BaseProcess.
Uses asyncio.subprocess for non-blocking I/O and process groups for clean shutdown.
"""

from __future__ import annotations

import asyncio
import os
import signal
from abc import ABC, abstractmethod
from datetime import datetime
from pathlib import Path
from typing import TYPE_CHECKING

from ..config import HEALTH_CHECK_INTERVAL_S, HEALTH_CHECK_TIMEOUT_S, SHUTDOWN_GRACE_S
from ..models import LogEntry, ProcessState, ProcessStatus

if TYPE_CHECKING:
    from ..config import RunnerConfig


class BaseProcess(ABC):
    """Abstract base for managed node processes.

    Provides:
    - Async subprocess start/stop with process group isolation
    - Non-blocking log consumption from stdout
    - TCP health check on configurable port
    - Graceful shutdown with SIGTERM/SIGKILL escalation
    """

    def __init__(self, config: RunnerConfig) -> None:
        self.config = config
        self.state = ProcessState(name=self.name)
        self._proc: asyncio.subprocess.Process | None = None
        self._log_task: asyncio.Task[None] | None = None

    @property
    @abstractmethod
    def name(self) -> str:
        """Human-readable process name used in logs and TUI."""
        ...

    @abstractmethod
    def build_command(self) -> list[str]:
        """Build the full command-line argument list."""
        ...

    @abstractmethod
    def working_directory(self) -> Path:
        """Working directory for the subprocess."""
        ...

    @property
    def health_check_port(self) -> int | None:
        """TCP port to probe for health. None skips health check."""
        return None

    async def start(self) -> None:
        """Start the subprocess and begin consuming its output."""
        if self._proc is not None:
            return

        self.state.status = ProcessStatus.STARTING
        cmd = self.build_command()
        cwd = self.working_directory()

        # Ensure data directory exists
        data_path = self.config.data_path(self.name)
        data_path.mkdir(parents=True, exist_ok=True)

        self._proc = await asyncio.create_subprocess_exec(
            *cmd,
            stdout=asyncio.subprocess.PIPE,
            stderr=asyncio.subprocess.STDOUT,
            cwd=cwd,
            start_new_session=True,
        )

        self.state.pid = self._proc.pid
        self.state.start_time = datetime.now()
        self._log_task = asyncio.create_task(self._consume_logs())

    async def stop(self, grace_s: float = SHUTDOWN_GRACE_S) -> bool:
        """Stop the process gracefully.

        Sends SIGTERM to the process group, waits up to grace_s seconds,
        then escalates to SIGKILL.

        Returns:
            True if the process stopped cleanly (SIGTERM), False if force-killed.
        """
        if self._proc is None or self._proc.returncode is not None:
            self.state.status = ProcessStatus.STOPPED
            self._cancel_log_task()
            return True

        self.state.status = ProcessStatus.STOPPING

        try:
            os.killpg(self._proc.pid, signal.SIGTERM)
        except ProcessLookupError:
            self.state.status = ProcessStatus.STOPPED
            self._cancel_log_task()
            return True

        try:
            await asyncio.wait_for(self._proc.wait(), timeout=grace_s)
            self.state.status = ProcessStatus.STOPPED
            self.state.exit_code = self._proc.returncode
            self._cancel_log_task()
            return True
        except TimeoutError:
            pass

        # Escalate to SIGKILL
        try:
            os.killpg(self._proc.pid, signal.SIGKILL)
            await asyncio.wait_for(self._proc.wait(), timeout=5.0)
        except (TimeoutError, ProcessLookupError):
            pass

        self.state.status = ProcessStatus.STOPPED
        self.state.exit_code = self._proc.returncode
        self._cancel_log_task()
        return False

    async def wait_for_healthy(self, timeout: float = HEALTH_CHECK_TIMEOUT_S) -> bool:
        """Wait for the process to become healthy via TCP port probe.

        Returns:
            True if healthy within timeout, False otherwise.
        """
        port = self.health_check_port
        if port is None:
            self.state.status = ProcessStatus.HEALTHY
            return True

        deadline = asyncio.get_event_loop().time() + timeout
        while asyncio.get_event_loop().time() < deadline:
            # Check for early process death
            if self._proc is not None and self._proc.returncode is not None:
                self.state.status = ProcessStatus.CRASHED
                self.state.exit_code = self._proc.returncode
                return False

            try:
                _, writer = await asyncio.wait_for(
                    asyncio.open_connection("127.0.0.1", port),
                    timeout=1.0,
                )
                writer.close()
                await writer.wait_closed()
                self.state.status = ProcessStatus.HEALTHY
                return True
            except (TimeoutError, OSError):
                await asyncio.sleep(HEALTH_CHECK_INTERVAL_S)

        self.state.status = ProcessStatus.UNHEALTHY
        return False

    @property
    def is_running(self) -> bool:
        """Check if the underlying process is alive."""
        return self._proc is not None and self._proc.returncode is None

    def check_for_crash(self) -> bool:
        """Check if process has died unexpectedly. Returns True if crashed."""
        if self._proc is None:
            return False
        if self._proc.returncode is not None and self.state.status == ProcessStatus.HEALTHY:
            self.state.status = ProcessStatus.CRASHED
            self.state.exit_code = self._proc.returncode
            return True
        return False

    async def _consume_logs(self) -> None:
        """Read stdout line by line and populate the log and error buffers."""
        assert self._proc is not None and self._proc.stdout is not None
        try:
            async for raw_line in self._proc.stdout:
                line = raw_line.decode("utf-8", errors="replace")
                entry = self._parse_log_line(line)
                self.state.log_buffer.append(entry)
                if entry.is_notable:
                    self.state.error_log.append(entry)
        except asyncio.CancelledError:
            return

    def _parse_log_line(self, line: str) -> LogEntry:
        """Parse a raw log line into a structured LogEntry.

        Uses simple keyword detection. Both Nitro and Nethermind include
        level keywords (INFO, WARN, ERROR, DEBUG) in their log output.
        """
        upper = line.upper()
        if "ERROR" in upper or "ERR " in upper or "FATAL" in upper or "CRIT" in upper:
            level = "ERROR"
        elif "WARN" in upper:
            level = "WARN"
        elif "DEBUG" in upper or "DBG " in upper:
            level = "DEBUG"
        elif "TRACE" in upper or "TRC " in upper:
            level = "TRACE"
        else:
            level = "INFO"

        return LogEntry(
            timestamp=datetime.now(),
            process_name=self.name,
            level=level,
            message=line.rstrip(),
            raw=line,
        )

    def _cancel_log_task(self) -> None:
        """Cancel the log consumption task if running."""
        if self._log_task is not None and not self._log_task.done():
            self._log_task.cancel()
