"""Unified data models for the comparison test runner.

This module provides dataclasses for test results and worker instances.
All classes use Python 3.12+ features: slots=True, match statements.
"""

from __future__ import annotations

import subprocess
from dataclasses import dataclass
from enum import Enum
from pathlib import Path

# Type aliases for clarity
TestName = str
PortNumber = int


class TestStatus(Enum):
    """Status of a test execution."""

    PENDING = "pending"
    RUNNING = "running"
    PASSED = "passed"
    FAILED = "failed"
    TIMEOUT = "timeout"
    SKIPPED = "skipped"
    ERROR = "error"

    @property
    def icon(self) -> str:
        """Return a visual icon for the status."""
        match self:
            case TestStatus.PENDING:
                return "○ PENDING"
            case TestStatus.RUNNING:
                return "◐ RUNNING"
            case TestStatus.PASSED:
                return "✓ PASS"
            case TestStatus.FAILED:
                return "✗ FAIL"
            case TestStatus.TIMEOUT:
                return "⏱ TIMEOUT"
            case TestStatus.SKIPPED:
                return "⊘ SKIPPED"
            case TestStatus.ERROR:
                return "⚠ ERROR"

    def is_terminal(self) -> bool:
        """Return True if this status represents a completed test."""
        match self:
            case (
                TestStatus.PASSED
                | TestStatus.FAILED
                | TestStatus.TIMEOUT
                | TestStatus.SKIPPED
                | TestStatus.ERROR
            ):
                return True
            case _:
                return False


@dataclass(slots=True)
class TestResult:
    """Result of a single test execution.

    Unified model for both sequential and parallel modes.
    In sequential mode, worker_id is -1.
    """

    name: str
    status: TestStatus = TestStatus.PENDING
    exit_code: int | None = None
    duration_s: float = 0.0
    error_msg: str = ""
    log_dir: Path | None = None
    worker_id: int = -1  # -1 indicates sequential mode
    attempt: int = 1  # Which attempt produced this result (1-based)
    max_attempts: int = 1  # Total attempts allowed (1 = no retries)

    @property
    def was_retried(self) -> bool:
        """True if this test passed on a retry (flaky test indicator)."""
        return self.status == TestStatus.PASSED and self.attempt > 1

    def is_sequential(self) -> bool:
        """Return True if this result is from sequential mode."""
        return self.worker_id == -1

    def is_terminal(self) -> bool:
        """Return True if the test has completed."""
        return self.status.is_terminal()


@dataclass(slots=True)
class WorkerInstance:
    """Manages a single Nethermind instance for test execution.

    Each worker has its own port and process, allowing parallel test execution.
    """

    worker_id: int
    nethermind_port: PortNumber
    nethermind_proc: subprocess.Popen[bytes] | None = None
    data_dir: Path | None = None
    current_test: str = ""

    def is_running(self) -> bool:
        """Return True if the Nethermind process is currently running."""
        if self.nethermind_proc is None:
            return False
        return self.nethermind_proc.poll() is None

    def is_idle(self) -> bool:
        """Return True if the worker is running but not executing a test."""
        return self.is_running() and not self.current_test
