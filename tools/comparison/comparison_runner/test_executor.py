"""Go test execution for comparison testing.

This module provides a single source of truth for running Go tests,
eliminating code duplication from the original monolithic implementation.

Features:
- Single run_go_test function for both sequential and parallel modes
- Proper subprocess management with signal handling
- Log file handling with ld warning suppression
- Interrupt support via threading.Event
"""

from __future__ import annotations

import os
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from threading import Event
from typing import Final

from .exceptions import TestExecutionError
from .logging_utils import get_logger
from .models import TestResult, TestStatus

DEFAULT_TEST_TIMEOUT: Final[str] = "5m"
"""Default timeout for individual Go tests."""

GO_TEST_PARALLEL: Final[int] = 1
"""Parallelism within a single Go test (always 1 for comparison mode)."""

_logger = get_logger()


def clean_test_cache(nitro_path: Path) -> bool:
    """Clean Go test cache once at startup.

    This should be called ONCE before starting any test execution,
    NOT per-test. Running it concurrently from multiple workers
    causes race conditions.

    Args:
        nitro_path: Path to Nitro repository

    Returns:
        True if cache was cleaned successfully, False otherwise
    """
    if not nitro_path.exists():
        _logger.warning(f"NITRO_PATH not found, skipping cache clean: {nitro_path}")
        return False

    _logger.info("Cleaning Go test cache...")
    result = subprocess.run(
        ["go", "clean", "-testcache"],
        cwd=str(nitro_path),
        capture_output=True,
        text=True,
    )

    if result.returncode != 0:
        _logger.warning(f"Failed to clean test cache: {result.stderr}")
        return False

    _logger.info("Go test cache cleaned successfully")
    return True


def compile_test_binary(nitro_path: Path, output_path: Path | None = None) -> Path | None:
    """Pre-compile the Go test binary once at startup.

    This separates compilation time from test execution time, providing
    accurate timing for individual tests. The compiled binary can be
    reused by all workers.

    Args:
        nitro_path: Path to Nitro repository
        output_path: Where to write the binary (default: nitro_path/system_tests.test)

    Returns:
        Path to compiled binary, or None if compilation failed
    """
    import time

    if not nitro_path.exists():
        _logger.warning(f"NITRO_PATH not found, skipping compilation: {nitro_path}")
        return None

    if output_path is None:
        output_path = nitro_path / "system_tests.test"

    _logger.info("Pre-compiling Go test binary (this may take several minutes)...")
    start_time = time.time()

    # Build environment with CGO flags for macOS
    env = os.environ.copy()
    if sys.platform == "darwin":
        env["CGO_LDFLAGS"] = "-Wl,-no_warn_duplicate_libraries"

    result = subprocess.run(
        [
            "go",
            "test",
            "-c",  # Compile but don't run
            "./system_tests",
            "-o",
            str(output_path),
        ],
        cwd=str(nitro_path),
        capture_output=True,
        text=True,
        env=env,
    )

    compile_time = time.time() - start_time

    if result.returncode != 0:
        _logger.error(f"Failed to compile test binary: {result.stderr}")
        return None

    _logger.info(f"Go test binary compiled successfully in {compile_time:.1f}s")
    return output_path


@dataclass(slots=True)
class GoTestResult:
    """Result from a Go test execution.

    This is a lightweight result focused on the execution itself,
    separate from the full TestResult, which includes more metadata.
    """

    exit_code: int
    error_msg: str = ""
    interrupted: bool = False
    suppressed_warnings: int = 0


def run_go_test(
    test_name: str,
    nitro_path: Path,
    env: dict[str, str],
    log_path: Path | None = None,
    interrupted: Event | None = None,
    timeout: str = DEFAULT_TEST_TIMEOUT,
    test_binary: Path | None = None,
) -> GoTestResult:
    """Run a single Go test.

    This is the single source of truth for Go test execution,
    removing duplication between sequential and parallel runners.

    Args:
        test_name: Name of the test to run (exact match)
        nitro_path: Path to Nitro repository
        env: Environment variables (must include NITRO_SECONDARY_EL_URL)
        log_path: Path to write test output (None for /dev/null)
        interrupted: Optional event to check for interruption
        timeout: Go test timeout string (default "5m")
        test_binary: Pre-compiled test binary (if None, uses `go test`)

    Returns:
        GoTestResult with exit code and any error message

    Raises:
        TestExecutionError: If test execution fails to start
    """
    if not nitro_path.exists():
        raise TestExecutionError(
            f"NITRO_PATH not found: {nitro_path}",
            test_name=test_name,
        )

    # Build the exact test filter (escape special regex chars)
    exact_filter = f"^{re.escape(test_name)}$"

    # Use pre-compiled binary if available (much faster - no compilation overhead)
    if test_binary is not None and test_binary.exists():
        cmd = [
            str(test_binary),
            f"-test.run={exact_filter}",
            "-test.v",
            f"-test.parallel={GO_TEST_PARALLEL}",
            f"-test.timeout={timeout}",
            "-test.count=1",
            "--",  # Delimiter for custom test flags
            "-test_loglevel=-4",  # Debug level for comparison logs
        ]
    else:
        # Fall back to go test (includes compilation time)
        # Note: -args passes remaining arguments to the test binary
        cmd = [
            "go",
            "test",
            "./system_tests",
            "-run",
            exact_filter,
            "-v",
            f"-parallel={GO_TEST_PARALLEL}",
            "-timeout",
            timeout,
            "-count=1",
            "-args",
            "--",  # Delimiter for custom test flags
            "-test_loglevel=-4",  # Debug level for comparison logs
        ]

    # Setup test environment
    test_env = env.copy()
    test_env["NITRO_EXECUTION_MODE"] = "comparison"

    # Note: go clean -testcache is now called ONCE at startup via clean_test_cache()
    # This avoids race conditions when multiple workers clean concurrently

    # Suppress duplicate library warnings (macOS ld64 only)
    if sys.platform == "darwin":
        test_env["CGO_LDFLAGS"] = "-Wl,-no_warn_duplicate_libraries"

    # Open a log file or /dev/null
    if log_path:
        log_path.parent.mkdir(parents=True, exist_ok=True)
        log_file_handle = log_path.open("w", encoding="utf-8")
    else:
        log_file_handle = Path(os.devnull).open("w")  # noqa: SIM115

    try:
        with log_file_handle as log_file:
            try:
                proc = subprocess.Popen(
                    cmd,
                    cwd=str(nitro_path),
                    stdout=subprocess.PIPE,
                    stderr=subprocess.STDOUT,
                    text=True,
                    env=test_env,
                )
            except OSError as e:
                raise TestExecutionError(
                    f"Failed to start Go test: {e}",
                    test_name=test_name,
                ) from e

            suppressed = 0
            assert proc.stdout is not None

            for line in iter(proc.stdout.readline, ""):
                # Check for interruption
                if interrupted is not None and interrupted.is_set():
                    proc.terminate()
                    try:
                        proc.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        proc.kill()
                        proc.wait()
                    return GoTestResult(
                        exit_code=-1,
                        error_msg="interrupted",
                        interrupted=True,
                        suppressed_warnings=suppressed,
                    )

                # Filter out ld warnings (noisy on macOS)
                if "ld: warning" in line:
                    suppressed += 1
                    continue

                log_file.write(line)

            proc.stdout.close()
            exit_code = proc.wait()

            return GoTestResult(
                exit_code=exit_code,
                suppressed_warnings=suppressed,
            )

    except Exception as e:
        if isinstance(e, TestExecutionError):
            raise
        raise TestExecutionError(
            f"Test execution failed: {e}",
            test_name=test_name,
        ) from e


def build_test_env(
    nitro_path: Path,
    nethermind_host: str,
    nethermind_port: int,
    base_env: dict[str, str] | None = None,
) -> dict[str, str]:
    """Build environment variables for Go test execution.

    Args:
        nitro_path: Path to Nitro repository
        nethermind_host: Nethermind RPC host
        nethermind_port: Nethermind RPC port
        base_env: Base environment (uses os.environ if None)

    Returns:
        Environment dict ready for run_go_test
    """
    env = (base_env or os.environ).copy()
    env["NITRO_PATH"] = str(nitro_path)
    env["NITRO_SECONDARY_EL_URL"] = f"http://{nethermind_host}:{nethermind_port}"
    return env


def execute_test(
    test_name: str,
    nitro_path: Path,
    nethermind_host: str,
    nethermind_port: int,
    log_dir: Path | None = None,
    worker_id: int = -1,
    interrupted: Event | None = None,
    test_binary: Path | None = None,
) -> TestResult:
    """Execute a test and return a full TestResult.

    Convenience function that combines build_test_env and run_go_test
    into a single call that returns a TestResult.

    Args:
        test_name: Name of the test
        nitro_path: Path to Nitro repository
        nethermind_host: Nethermind RPC host
        nethermind_port: Nethermind RPC port
        log_dir: Directory for logs (test-specific subdir created)
        worker_id: Worker ID (-1 for sequential mode)
        interrupted: Optional interrupt event
        test_binary: Pre-compiled test binary (faster execution)

    Returns:
        TestResult with status and metadata
    """
    import time

    start_time = time.time()

    # Build environment
    env = build_test_env(nitro_path, nethermind_host, nethermind_port)

    # Note: Nitro comparison mode now uses dynamic port allocation (WSPort=0 with URL="self")
    # This eliminates TIME_WAIT socket conflicts in parallel test execution

    # Determine log path
    log_path = None
    test_log_dir = None
    if log_dir:
        test_log_dir = log_dir / test_name
        test_log_dir.mkdir(parents=True, exist_ok=True)
        suffix = f"-worker{worker_id}" if worker_id >= 0 else ""
        log_path = test_log_dir / f"nitro-test{suffix}.log"

    try:
        go_result = run_go_test(
            test_name=test_name,
            nitro_path=nitro_path,
            env=env,
            log_path=log_path,
            interrupted=interrupted,
            test_binary=test_binary,
        )

        duration = time.time() - start_time

        # Map result to status
        if go_result.interrupted:
            status = TestStatus.SKIPPED
            error_msg = "interrupted"
        elif go_result.exit_code == 0:
            status = TestStatus.PASSED
            error_msg = ""
        else:
            status = TestStatus.FAILED
            error_msg = go_result.error_msg or f"exit code {go_result.exit_code}"

        return TestResult(
            name=test_name,
            status=status,
            exit_code=go_result.exit_code,
            duration_s=duration,
            error_msg=error_msg,
            log_dir=test_log_dir,
            worker_id=worker_id,
        )

    except TestExecutionError as e:
        duration = time.time() - start_time
        return TestResult(
            name=test_name,
            status=TestStatus.FAILED,
            exit_code=None,
            duration_s=duration,
            error_msg=str(e),
            log_dir=test_log_dir,
            worker_id=worker_id,
        )
