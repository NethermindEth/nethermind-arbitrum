"""Data models for process state and log entries."""

from __future__ import annotations

from collections import deque
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum

from .config import ERROR_BUFFER_SIZE, LOG_BUFFER_SIZE


class ProcessStatus(Enum):
    """Lifecycle state of a managed process."""

    PENDING = "pending"
    INITIALIZING = "initializing"
    STARTING = "starting"
    HEALTHY = "healthy"
    UNHEALTHY = "unhealthy"
    STOPPING = "stopping"
    STOPPED = "stopped"
    CRASHED = "crashed"


@dataclass(slots=True)
class LogEntry:
    """A single parsed log line."""

    timestamp: datetime
    process_name: str
    level: str
    message: str
    raw: str

    @property
    def is_error(self) -> bool:
        return self.level in ("ERROR", "FATAL", "CRIT")

    @property
    def is_warning(self) -> bool:
        return self.level in ("WARN", "WARNING")

    @property
    def is_notable(self) -> bool:
        """True for lines worth persisting (errors and warnings)."""
        return self.is_error or self.is_warning


@dataclass
class ProcessState:
    """Runtime state of a managed process."""

    name: str
    status: ProcessStatus = ProcessStatus.PENDING
    pid: int | None = None
    start_time: datetime | None = None
    exit_code: int | None = None
    log_buffer: deque[LogEntry] = field(
        default_factory=lambda: deque(maxlen=LOG_BUFFER_SIZE)
    )
    error_log: deque[LogEntry] = field(
        default_factory=lambda: deque(maxlen=ERROR_BUFFER_SIZE)
    )

    @property
    def is_terminal(self) -> bool:
        """True if the process has reached a final state."""
        return self.status in (ProcessStatus.STOPPED, ProcessStatus.CRASHED)

    @property
    def error_count(self) -> int:
        return sum(1 for e in self.error_log if e.is_error)

    @property
    def warning_count(self) -> int:
        return sum(1 for e in self.error_log if e.is_warning)
