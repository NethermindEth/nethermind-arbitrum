"""Entry point for python -m comparison_runner.

This module provides the main() function that orchestrates test execution,
dispatching to either SequentialRunner or ParallelRunner based on worker count.
"""

from __future__ import annotations

import sys
import time
from pathlib import Path
from typing import TYPE_CHECKING

from .cli import parse_args
from .formatters import (
    SummaryStats,
    format_failed_tests,
    format_passed_tests_summary,
    format_summary_box,
)
from .logging_utils import get_logger, setup_logging
from .models import TestResult, TestStatus
from .parallel_runner import ParallelRunner, ParallelRunnerConfig
from .sequential_runner import SequentialRunner, SequentialRunnerConfig
from .summary import write_github_summary
from .test_discovery import get_tests

if TYPE_CHECKING:
    import argparse


# Relative path from repo root to Nethermind build directory
NETHERMIND_BUILD_RELPATH = Path(
    "src/Nethermind/src/Nethermind/artifacts/bin/Nethermind.Runner/debug"
)


def find_nethermind_root() -> Path:
    """Find Nethermind repository root by looking for marker files.

    Searches upward from the current working directory and the package location.

    Returns:
        Path to Nethermind repository root

    Raises:
        SystemExit: If Nethermind root cannot be found
    """
    markers = ["src/Nethermind", "tools/comparison", ".git"]

    # Search from current directory upward
    for start in [Path.cwd(), Path(__file__).resolve().parent]:
        current = start
        for _ in range(10):  # Limit search depth
            if all((current / marker).exists() for marker in markers):
                return current
            parent = current.parent
            if parent == current:
                break
            current = parent

    # Not found
    print(
        "Error: Cannot find Nethermind repository root.\n"
        "Run from within the nethermind-arbitrum repository or use --nethermind-path.",
        file=sys.stderr,
    )
    sys.exit(1)


def load_tests(args: argparse.Namespace) -> list[str]:
    """Load test names from filter, file, or auto-discovery.

    Priority:
    1. If --test-file provided, load from file (with optional filter)
    2. If --test-filter provided, discover and filter
    3. Otherwise, auto-discover all tests from Nitro

    Args:
        args: Parsed command-line arguments

    Returns:
        List of test names to run
    """
    try:
        tests = get_tests(
            nitro_path=args.nitro_path,
            test_filter=args.test_filter or None,
            test_file=args.test_file,
        )
    except FileNotFoundError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)

    if not tests:
        print("Error: No tests found to run", file=sys.stderr)
        sys.exit(1)

    return tests


def print_summary(results: list[TestResult], duration: float) -> int:
    """Print test execution summary and return exit code.

    Args:
        results: List of test results
        duration: Total execution time in seconds

    Returns:
        Exit code (0 = success, 1 = failures)
    """
    stats = SummaryStats.from_results(results, duration)

    print(format_summary_box(stats))
    if stats.has_failures:
        print(format_failed_tests(results))
    print(format_passed_tests_summary(results))
    print()

    # Fail if any tests failed/errored OR if no tests ran
    if stats.has_failures or stats.total == 0:
        return 1
    return 0


def print_json_results(results: list[TestResult], duration: float) -> int:
    """Print results as JSON and return exit code.

    Args:
        results: List of test results
        duration: Total execution time in seconds

    Returns:
        Exit code (0 = success, 1 = failures)
    """
    import json

    output = {
        "duration_s": round(duration, 2),
        "total": len(results),
        "passed": sum(1 for r in results if r.status == TestStatus.PASSED),
        "failed": sum(1 for r in results if r.status == TestStatus.FAILED),
        "skipped": sum(1 for r in results if r.status == TestStatus.SKIPPED),
        "errors": sum(1 for r in results if r.status == TestStatus.ERROR),
        "results": [
            {
                "name": r.name,
                "status": r.status.name,
                "duration_s": round(r.duration_s, 2) if r.duration_s else None,
                "worker_id": r.worker_id if r.worker_id >= 0 else None,
                "error_msg": r.error_msg,
            }
            for r in results
        ],
    }

    print(json.dumps(output, indent=2))

    failed_count = sum(1 for r in results if r.status in (TestStatus.FAILED, TestStatus.ERROR))
    # Fail if any tests failed/errored OR if no tests ran (worker startup failure)
    if failed_count > 0 or len(results) == 0:
        return 1
    return 0


def main() -> int:
    """Entry point for python -m comparison_runner.

    Returns:
        Exit code (0 = success, 1 = failure)
    """
    args = parse_args()

    # Setup logging
    import logging

    setup_logging(level=logging.DEBUG if args.verbose else logging.INFO)
    logger = get_logger()

    # Find Nethermind build directory and root
    if args.nethermind_build_dir:
        nethermind_build_dir = args.nethermind_build_dir
        # Use cwd as root when build dir is explicitly provided
        nethermind_root = Path.cwd()
    else:
        nethermind_root = args.nethermind_path or find_nethermind_root()
        nethermind_build_dir = nethermind_root / NETHERMIND_BUILD_RELPATH

    if not nethermind_build_dir.exists():
        print(
            f"Error: Nethermind build directory not found: {nethermind_build_dir}\n"
            "Run 'dotnet build' in the Nethermind directory first, or use --nethermind-build-dir.",
            file=sys.stderr,
        )
        return 1

    # Load tests
    tests = load_tests(args)
    logger.info(f"Running {len(tests)} tests with {args.workers} worker(s)")

    # Create runner based on worker count
    start_time = time.time()

    runner: SequentialRunner | ParallelRunner
    if args.workers == 1:
        # Sequential mode
        seq_config = SequentialRunnerConfig(
            nitro_path=args.nitro_path,
            nethermind_build_dir=nethermind_build_dir,
            port=args.base_port,
            log_dir=args.log_dir,
            startup_timeout_s=args.timeout,
            fail_fast=args.fail_fast,
            verbose=args.verbose,
        )
        runner = SequentialRunner(seq_config)
    else:
        # Parallel mode
        par_config = ParallelRunnerConfig(
            nitro_path=args.nitro_path,
            nethermind_build_dir=nethermind_build_dir,
            max_workers=args.workers,
            base_port=args.base_port,
            root_dir=nethermind_root,
            log_dir=args.log_dir,
            startup_timeout_s=args.timeout,
        )
        runner = ParallelRunner(par_config)

    # Run tests
    try:
        results = runner.run(tests)
    except KeyboardInterrupt:
        logger.warning("Interrupted by user")
        results = []

    duration = time.time() - start_time

    # Write GitHub Actions Job Summary if requested
    if args.github_summary:
        write_github_summary(
            results=results,
            duration=duration,
            workers=args.workers,
            nitro_branch=args.nitro_branch,
        )

    # Output results
    if args.json:
        return print_json_results(results, duration)
    else:
        return print_summary(results, duration)


if __name__ == "__main__":
    sys.exit(main())
