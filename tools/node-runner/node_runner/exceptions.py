"""Custom exceptions for node-runner."""

from __future__ import annotations


class NodeRunnerError(Exception):
    """Base exception for all node-runner errors."""


class ConfigError(NodeRunnerError):
    """Invalid configuration."""


class StartupError(NodeRunnerError):
    """Process failed to start."""

    def __init__(self, message: str, process_name: str, port: int | None = None) -> None:
        self.process_name = process_name
        self.port = port
        super().__init__(message)


class HealthCheckError(NodeRunnerError):
    """Process failed health check within timeout."""

    def __init__(self, message: str, process_name: str, port: int, timeout: float) -> None:
        self.process_name = process_name
        self.port = port
        self.timeout = timeout
        super().__init__(message)


class ShutdownError(NodeRunnerError):
    """Error during graceful shutdown."""

    def __init__(self, message: str, pid: int) -> None:
        self.pid = pid
        super().__init__(message)


class InitError(NodeRunnerError):
    """EL initialization failed."""

    def __init__(self, message: str, exit_code: int | None = None) -> None:
        self.exit_code = exit_code
        super().__init__(message)
