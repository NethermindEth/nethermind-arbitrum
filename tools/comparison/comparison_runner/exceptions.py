"""Custom exception hierarchy for the comparison test runner.

All exceptions inherit from ComparisonTestError for consistent error handling.
Each exception type captures relevant context for debugging.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from pathlib import Path


class ComparisonTestError(Exception):
    """Base exception for all comparison test runner errors.

    All custom exceptions inherit from this class, allowing callers
    to catch all runner-specific errors with a single except clause.
    """


class ConfigurationError(ComparisonTestError):
    """Raised when configuration is invalid or missing.

    Examples:
        - Invalid port range
        - Missing required paths
        - Invalid worker count
    """

    def __init__(self, message: str, field: str | None = None) -> None:
        self.field = field
        super().__init__(message)


class NethermindStartupError(ComparisonTestError):
    """Raised when a Nethermind instance fails to start.

    Captures the port and log directory for debugging.
    """

    def __init__(
        self,
        message: str,
        port: int | None = None,
        log_dir: Path | None = None,
        worker_id: int | None = None,
    ) -> None:
        self.port = port
        self.log_dir = log_dir
        self.worker_id = worker_id
        super().__init__(message)

    def __str__(self) -> str:
        parts = [super().__str__()]
        if self.port is not None:
            parts.append(f"port={self.port}")
        if self.worker_id is not None:
            parts.append(f"worker={self.worker_id}")
        if self.log_dir is not None:
            parts.append(f"logs={self.log_dir}")
        return " | ".join(parts)


class TestExecutionError(ComparisonTestError):
    """Raised when a test fails to execute properly.

    Distinct from a test failure - this indicates infrastructure problems
    like process spawn failures or timeout issues.
    """

    def __init__(
        self,
        message: str,
        test_name: str | None = None,
        exit_code: int | None = None,
        worker_id: int | None = None,
    ) -> None:
        self.test_name = test_name
        self.exit_code = exit_code
        self.worker_id = worker_id
        super().__init__(message)

    def __str__(self) -> str:
        parts = [super().__str__()]
        if self.test_name is not None:
            parts.append(f"test={self.test_name}")
        if self.exit_code is not None:
            parts.append(f"exit_code={self.exit_code}")
        if self.worker_id is not None:
            parts.append(f"worker={self.worker_id}")
        return " | ".join(parts)


class RPCError(ComparisonTestError):
    """Raised when an RPC call fails.

    Captures the RPC method and port for debugging.
    """

    def __init__(
        self,
        message: str,
        method: str | None = None,
        port: int | None = None,
        response: str | None = None,
    ) -> None:
        self.method = method
        self.port = port
        self.response = response
        super().__init__(message)

    def __str__(self) -> str:
        parts = [super().__str__()]
        if self.method is not None:
            parts.append(f"method={self.method}")
        if self.port is not None:
            parts.append(f"port={self.port}")
        return " | ".join(parts)


class WorkerPoolError(ComparisonTestError):
    """Raised when the worker pool encounters an error.

    Examples:
        - All workers crashed
        - No available workers
        - Worker allocation failure
    """

    def __init__(
        self,
        message: str,
        active_workers: int | None = None,
        total_workers: int | None = None,
    ) -> None:
        self.active_workers = active_workers
        self.total_workers = total_workers
        super().__init__(message)


class ProcessTerminationError(ComparisonTestError):
    """Raised when a process cannot be cleanly terminated.

    Used during cleanup when processes don't respond to signals.
    """

    def __init__(
        self,
        message: str,
        pid: int | None = None,
        signal_sent: str | None = None,
    ) -> None:
        self.pid = pid
        self.signal_sent = signal_sent
        super().__init__(message)
