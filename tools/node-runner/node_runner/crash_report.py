"""Crash report generation on shutdown.

Saves the last N lines from each process's ring buffer plus an error summary.
Reports are timestamped and stored in the data directory.
"""

from __future__ import annotations

import logging
from collections.abc import Sequence
from datetime import datetime
from pathlib import Path
from typing import TYPE_CHECKING

from .config import CRASH_REPORT_LINES

if TYPE_CHECKING:
    from .processes.base import BaseProcess

logger = logging.getLogger(__name__)


def write_crash_report(
    processes: Sequence[BaseProcess],
    output_dir: Path,
) -> Path | None:
    """Write a crash report with recent logs from all processes.

    Creates a timestamped directory containing:
    - summary.txt: Process status overview
    - {name}.log: Last CRASH_REPORT_LINES lines per process
    - {name}.errors.log: All captured error/warning lines per process

    Args:
        processes: List of managed processes to report on
        output_dir: Base directory for crash reports

    Returns:
        Path to the created report directory, or None if no processes to report
    """
    if not processes:
        return None

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    report_dir = output_dir / f"report_{timestamp}"

    try:
        report_dir.mkdir(parents=True, exist_ok=True)
    except OSError as e:
        logger.error("Failed to create crash report directory %s: %s", report_dir, e)
        return None

    # Write summary
    _write_summary(processes, report_dir / "summary.txt")

    # Write per-process logs
    for proc in processes:
        _write_process_log(proc, report_dir)
        _write_error_log(proc, report_dir)

    logger.info("Crash report written to %s", report_dir)
    return report_dir


def _write_summary(processes: Sequence[BaseProcess], path: Path) -> None:
    """Write a summary of all process states."""
    try:
        with path.open("w", encoding="utf-8") as f:
            f.write("Node Runner Shutdown Report\n")
            f.write(f"Generated: {datetime.now().isoformat()}\n")
            f.write("=" * 60 + "\n\n")

            for proc in processes:
                state = proc.state
                f.write(f"Process: {state.name}\n")
                f.write(f"  Status:    {state.status.value}\n")
                f.write(f"  PID:       {state.pid}\n")
                f.write(f"  Exit Code: {state.exit_code}\n")
                f.write(f"  Started:   {state.start_time}\n")
                f.write(f"  Errors:    {state.error_count}\n")
                f.write(f"  Warnings:  {state.warning_count}\n")
                f.write(f"  Log Lines: {len(state.log_buffer)}\n")
                f.write("\n")
    except OSError as e:
        logger.error("Failed to write summary: %s", e)


def _write_process_log(proc: BaseProcess, report_dir: Path) -> None:
    """Write the last N log lines for a process."""
    path = report_dir / f"{proc.name}.log"
    entries = list(proc.state.log_buffer)[-CRASH_REPORT_LINES:]

    try:
        with path.open("w", encoding="utf-8") as f:
            for entry in entries:
                f.write(entry.raw)
    except OSError as e:
        logger.error("Failed to write log for %s: %s", proc.name, e)


def _write_error_log(proc: BaseProcess, report_dir: Path) -> None:
    """Write only the error/warning lines for a process."""
    if not proc.state.error_log:
        return

    path = report_dir / f"{proc.name}.errors.log"

    try:
        with path.open("w", encoding="utf-8") as f:
            for entry in proc.state.error_log:
                f.write(entry.raw)
    except OSError as e:
        logger.error("Failed to write error log for %s: %s", proc.name, e)


def write_error_logs(
    processes: Sequence[BaseProcess],
    error_dir: Path,
) -> None:
    """Write persistent error-only log files (appended during runtime).

    These are small files containing only ERROR/WARN lines, suitable
    for long-running sessions where full logs would be too large.

    Args:
        processes: List of managed processes
        error_dir: Directory for error log files
    """
    error_dir.mkdir(parents=True, exist_ok=True)

    for proc in processes:
        if not proc.state.error_log:
            continue

        path = error_dir / f"{proc.name}.errors.log"
        try:
            with path.open("w", encoding="utf-8") as f:
                for entry in proc.state.error_log:
                    f.write(entry.raw)
        except OSError as e:
            logger.error("Failed to write error log for %s: %s", proc.name, e)
