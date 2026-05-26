"""Command-line interface for the comparison test runner.

Provides argument parsing with validation, improved help text, and examples.
"""

from __future__ import annotations

import argparse
import os
from pathlib import Path

from .config import (
    DEFAULT_BASE_PORT,
    DEFAULT_MAX_RETRIES,
    DEFAULT_STARTUP_TIMEOUT_S,
    MAX_WORKERS,
    MEMORY_PER_WORKER_GB,
    SYSTEM_RESERVE_GB,
)


def calculate_optimal_workers() -> int:
    """Calculate optimal worker count based on available system memory.

    Uses the same formula as the original implementation:
    - Reserve SYSTEM_RESERVE_GB for OS and Nitro
    - Allocate MEMORY_PER_WORKER_GB per worker
    - Cap at MAX_WORKERS

    Returns:
        Optimal number of workers (1-MAX_WORKERS)
    """
    try:
        import psutil

        available_gb = psutil.virtual_memory().available / (1024**3)
        usable_gb = max(0, available_gb - SYSTEM_RESERVE_GB)
        workers = int(usable_gb / MEMORY_PER_WORKER_GB)
        return max(1, min(workers, MAX_WORKERS))
    except ImportError:
        import logging

        logging.getLogger(__name__).warning(
            "psutil not installed - defaulting to 4 workers. Install with: pip install psutil"
        )
        return 4


def parse_workers(value: str) -> int:
    """Parse the --workers argument.

    Args:
        value: Either "auto" or an integer string

    Returns:
        Number of workers to use

    Raises:
        argparse.ArgumentTypeError: If value is invalid
    """
    if value.lower() == "auto":
        return calculate_optimal_workers()

    try:
        workers = int(value)
        if workers < 1:
            raise argparse.ArgumentTypeError(f"Worker count must be >= 1, got {workers}")
        if workers > MAX_WORKERS:
            raise argparse.ArgumentTypeError(
                f"Worker count must be <= {MAX_WORKERS}, got {workers}"
            )
        return workers
    except ValueError:
        raise argparse.ArgumentTypeError(
            f"Invalid worker count: '{value}'. Use 'auto' or an integer 1-{MAX_WORKERS}."
        ) from None


def create_parser() -> argparse.ArgumentParser:
    """Create the argument parser with improved help and validation.

    Returns:
        Configured ArgumentParser
    """
    parser = argparse.ArgumentParser(
        prog="comparison_runner",
        description="Run Nethermind+Nitro comparison tests",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python -m comparison_runner                      # Auto-discover all ~391 tests
  python -m comparison_runner --workers 4          # 4 parallel workers
  python -m comparison_runner --workers 1          # Sequential mode
  python -m comparison_runner -t TestTransfer      # Filter by pattern
  python -m comparison_runner -t "Transfer|Deploy" # Multiple patterns (regex)
  python -m comparison_runner --test-file my.txt   # Load from file
  python -m comparison_runner --fail-fast          # Stop on first failure

Environment Variables:
  NITRO_PATH          Path to Nitro repository (required if not using --nitro-path)
  NETHERMIND_PATH     Path to Nethermind repository (optional)
""",
    )

    # -------------------------------------------------------------------------
    # Test Selection
    # -------------------------------------------------------------------------
    test_group = parser.add_argument_group("Test Selection")
    test_group.add_argument(
        "-t",
        "--test-filter",
        default="",
        metavar="PATTERN",
        help="Go test -run filter pattern (e.g., 'TestTransfer')",
    )
    test_group.add_argument(
        "--test-file",
        type=Path,
        default=None,
        metavar="FILE",
        help="File containing test names (default: auto-discover from Nitro)",
    )

    # -------------------------------------------------------------------------
    # Execution Options
    # -------------------------------------------------------------------------
    exec_group = parser.add_argument_group("Execution Options")
    exec_group.add_argument(
        "--workers",
        default="auto",
        type=parse_workers,
        metavar="N|auto",
        help=f"Number of parallel workers, 1-{MAX_WORKERS} or 'auto' (default: auto)",
    )
    exec_group.add_argument(
        "--base-port",
        type=int,
        default=DEFAULT_BASE_PORT,
        metavar="PORT",
        help=f"Base port for Nethermind instances (default: {DEFAULT_BASE_PORT})",
    )
    exec_group.add_argument(
        "--timeout",
        type=int,
        default=DEFAULT_STARTUP_TIMEOUT_S,
        metavar="SECONDS",
        help=f"Nethermind startup timeout (default: {DEFAULT_STARTUP_TIMEOUT_S}s)",
    )
    exec_group.add_argument(
        "--fail-fast",
        action="store_true",
        help="Stop execution on first test failure",
    )
    exec_group.add_argument(
        "--retries",
        type=int,
        default=DEFAULT_MAX_RETRIES,
        metavar="N",
        help=f"Retry failed tests up to N times (default: {DEFAULT_MAX_RETRIES}, 0 to disable)",
    )

    # -------------------------------------------------------------------------
    # Path Configuration
    # -------------------------------------------------------------------------
    path_group = parser.add_argument_group("Path Configuration")
    path_group.add_argument(
        "--nitro-path",
        type=Path,
        default=None,
        metavar="DIR",
        help="Path to Nitro repository (default: $NITRO_PATH)",
    )
    path_group.add_argument(
        "--nethermind-path",
        type=Path,
        default=None,
        metavar="DIR",
        help="Path to Nethermind repository (default: auto-detect)",
    )
    path_group.add_argument(
        "--nethermind-build-dir",
        type=Path,
        default=None,
        metavar="DIR",
        help="Path to Nethermind build directory (overrides --nethermind-path)",
    )
    path_group.add_argument(
        "--log-dir",
        type=Path,
        default=None,
        metavar="DIR",
        help="Directory for log files (optional)",
    )

    # -------------------------------------------------------------------------
    # Output Options
    # -------------------------------------------------------------------------
    output_group = parser.add_argument_group("Output Options")
    output_group.add_argument(
        "-v",
        "--verbose",
        action="store_true",
        help="Show detailed progress output",
    )
    output_group.add_argument(
        "--json",
        action="store_true",
        help="Output results as JSON",
    )
    output_group.add_argument(
        "--github-summary",
        action="store_true",
        help="Write GitHub Actions Job Summary (requires GITHUB_STEP_SUMMARY env)",
    )
    output_group.add_argument(
        "--nitro-branch",
        default="system-tests",
        metavar="BRANCH",
        help="Nitro branch name for summary (default: system-tests)",
    )

    return parser


def parse_args(args: list[str] | None = None) -> argparse.Namespace:
    """Parse command-line arguments.

    Args:
        args: Arguments to parse (defaults to sys.argv[1:])

    Returns:
        Parsed arguments namespace
    """
    parser = create_parser()
    parsed = parser.parse_args(args)

    # Resolve nitro_path from environment if not provided
    if parsed.nitro_path is None:
        env_path = os.environ.get("NITRO_PATH")
        if env_path:
            parsed.nitro_path = Path(env_path)
        else:
            parser.error(
                "Nitro path not specified. Use --nitro-path or set NITRO_PATH environment variable."
            )

    # Validate nitro_path exists
    if not parsed.nitro_path.is_dir():
        parser.error(f"Nitro path does not exist: {parsed.nitro_path}")

    return parsed
