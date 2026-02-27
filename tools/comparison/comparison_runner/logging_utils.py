"""Structured logging utilities for the comparison test runner.

Replaces the custom log() function with proper logging module usage.
Provides consistent formatting and test status logging helpers.
"""

from __future__ import annotations

import logging
import sys
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .models import TestResult

# Module-level logger
_logger: logging.Logger | None = None


def setup_logging(
    level: int = logging.INFO,
    name: str = "comparison_runner",
) -> logging.Logger:
    """Configure and return the comparison runner logger.

    Sets up structured logging with timestamp, level, and message format.
    Idempotent - calling multiple times returns the same configured logger.

    Args:
        level: Logging level (default: INFO)
        name: Logger name (default: comparison_runner)

    Returns:
        Configured logger instance

    Example:
        >>> logger = setup_logging()
        >>> logger.info("Starting test run")
        [12:34:56] [INFO] Starting test run
    """
    global _logger

    if _logger is not None:
        return _logger

    logger = logging.getLogger(name)
    logger.setLevel(level)

    # Avoid duplicate handlers if setup is called multiple times
    if not logger.handlers:
        handler = logging.StreamHandler(sys.stdout)
        handler.setLevel(level)

        # Format: [HH:MM:SS] [LEVEL] message
        formatter = logging.Formatter(
            fmt="[%(asctime)s] [%(levelname)s] %(message)s",
            datefmt="%H:%M:%S",
        )
        handler.setFormatter(formatter)
        logger.addHandler(handler)

    # Don't propagate to root logger
    logger.propagate = False

    _logger = logger
    return logger


def get_logger() -> logging.Logger:
    """Get the comparison runner logger, setting it up if needed.

    Returns:
        The configured logger instance
    """
    global _logger
    if _logger is None:
        return setup_logging()
    return _logger


def reset_logger() -> None:
    """Reset the logger (useful for testing).

    Removes all handlers and clears the cached logger instance.
    """
    global _logger
    if _logger is not None:
        for handler in _logger.handlers[:]:
            _logger.removeHandler(handler)
        _logger = None


def log_test_status(result: TestResult) -> None:
    """Log a test result with its status icon.

    Args:
        result: The test result to log

    Example:
        >>> log_test_status(result)
        [12:34:56] [INFO] ✓ PASS TestTransfer (1.23s)
    """
    logger = get_logger()
    duration = f"({result.duration_s:.2f}s)" if result.duration_s > 0 else ""
    worker = f"[W{result.worker_id}] " if result.worker_id >= 0 else ""

    msg = f"{result.status.icon} {worker}{result.name} {duration}".strip()

    # Use appropriate log level based on status
    match result.status.value:
        case "passed":
            logger.info(msg)
        case "failed" | "timeout":
            logger.error(msg)
        case "skipped":
            logger.warning(msg)
        case _:
            logger.debug(msg)


def log_worker_status(
    worker_id: int,
    message: str,
    level: int = logging.INFO,
) -> None:
    """Log a message with worker context.

    Args:
        worker_id: The worker ID
        message: The message to log
        level: Log level (default: INFO)
    """
    logger = get_logger()
    logger.log(level, f"[W{worker_id}] {message}")


def log_progress(
    completed: int,
    total: int,
    passed: int,
    failed: int,
) -> None:
    """Log test progress summary.

    Args:
        completed: Number of completed tests
        total: Total number of tests
        passed: Number of passed tests
        failed: Number of failed tests
    """
    logger = get_logger()
    pct = (completed / total * 100) if total > 0 else 0
    logger.info(f"Progress: {completed}/{total} ({pct:.0f}%) | Passed: {passed} | Failed: {failed}")
