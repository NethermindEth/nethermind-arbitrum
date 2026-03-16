"""Go test execution for comparison testing.

This module provides a single source of truth for running Go tests,
eliminating code duplication from the original monolithic implementation.

Features:
- Single run_go_test function for both sequential and parallel modes
- Proper subprocess management with signal handling
- Log file handling with ld warning suppression
- Interrupt support via threading.Event
- Smart rebuild detection for Go and .NET projects
"""

from __future__ import annotations

import hashlib
import json
import os
import re
import subprocess
import sys
import time
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

# Build state tracking
BUILD_STATE_FILE = ".comparison-build-state.json"


def _get_git_hash(project_path: Path, paths: list[str] | None = None) -> str | None:
    """Get combined hash of git state for specified paths.

    Uses git to compute a hash that changes when any tracked files change.
    This is more reliable than file modification times.

    Args:
        project_path: Root path of the git repository
        paths: Specific paths to check (relative to project_path), or None for all

    Returns:
        Hash string if successful, None if git command fails
    """
    try:
        # Get the tree hash which changes when any file content changes
        cmd = ["git", "rev-parse", "HEAD"]
        result = subprocess.run(
            cmd,
            cwd=str(project_path),
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            return None
        head_hash = result.stdout.strip()

        # Also include uncommitted changes in the hash
        cmd = ["git", "status", "--porcelain"]
        if paths:
            cmd.extend(["--"] + paths)
        result = subprocess.run(
            cmd,
            cwd=str(project_path),
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            return None

        # Combine HEAD hash with status of working tree
        status_hash = hashlib.md5(result.stdout.encode()).hexdigest()[:8]
        return f"{head_hash[:12]}_{status_hash}"
    except Exception:
        return None


def _load_build_state(state_file: Path) -> dict:
    """Load build state from JSON file."""
    if state_file.exists():
        try:
            return json.loads(state_file.read_text())
        except Exception:
            pass
    return {}


def _save_build_state(state_file: Path, state: dict) -> None:
    """Save build state to JSON file."""
    try:
        state_file.write_text(json.dumps(state, indent=2))
    except Exception as e:
        _logger.warning(f"Failed to save build state: {e}")


def _needs_rebuild(
    project_path: Path,
    binary_path: Path,
    state_file: Path,
    project_key: str,
    check_paths: list[str] | None = None,
) -> bool:
    """Check if a project needs to be rebuilt.

    Args:
        project_path: Path to the project root
        binary_path: Path to the output binary
        state_file: Path to build state JSON file
        project_key: Key to identify this project in state file
        check_paths: Specific paths to check for changes (relative to project_path)

    Returns:
        True if rebuild is needed, False otherwise
    """
    # Always rebuild if binary doesn't exist
    if not binary_path.exists():
        _logger.info(f"Binary not found: {binary_path}")
        return True

    # Get current git hash
    current_hash = _get_git_hash(project_path, check_paths)
    if current_hash is None:
        _logger.warning("Could not get git hash, forcing rebuild")
        return True

    # Load previous build state
    state = _load_build_state(state_file)
    previous_hash = state.get(project_key, {}).get("hash")

    if previous_hash != current_hash:
        _logger.info(f"Project changed: {project_key} ({previous_hash} -> {current_hash})")
        return True

    _logger.info(f"Project unchanged: {project_key} (hash: {current_hash})")
    return False


def _record_build(
    state_file: Path,
    project_key: str,
    project_path: Path,
    check_paths: list[str] | None = None,
) -> None:
    """Record successful build in state file."""
    current_hash = _get_git_hash(project_path, check_paths)
    if current_hash is None:
        return

    state = _load_build_state(state_file)
    state[project_key] = {
        "hash": current_hash,
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
    }
    _save_build_state(state_file, state)


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


def compile_test_binary(
    nitro_path: Path,
    output_path: Path | None = None,
    force: bool = False,
) -> Path | None:
    """Pre-compile the Go test binary if project has changed.

    Uses git to detect changes and only rebuilds when necessary.
    This provides fast startup when code hasn't changed.

    Args:
        nitro_path: Path to Nitro repository
        output_path: Where to write the binary (default: nitro_path/system_tests.test)
        force: Force rebuild even if no changes detected

    Returns:
        Path to compiled binary, or None if compilation failed
    """
    if not nitro_path.exists():
        _logger.warning(f"NITRO_PATH not found, skipping compilation: {nitro_path}")
        return None

    if output_path is None:
        output_path = nitro_path / "system_tests.test"

    state_file = nitro_path / BUILD_STATE_FILE

    # Check if rebuild is needed
    if not force and not _needs_rebuild(
        project_path=nitro_path,
        binary_path=output_path,
        state_file=state_file,
        project_key="nitro",
        check_paths=["execution/", "arbos/", "arbnode/", "system_tests/", "go-ethereum/"],
    ):
        _logger.info("Nitro binary up-to-date, skipping rebuild")
        return output_path

    # Delete old binary to force full recompilation
    if output_path.exists():
        _logger.info(f"Removing old test binary: {output_path}")
        output_path.unlink()

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
            "-a",  # Force rebuild all packages
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

    # Record successful build
    _record_build(state_file, "nitro", nitro_path,
                  ["execution/", "arbos/", "arbnode/", "system_tests/", "go-ethereum/"])

    return output_path


def build_nethermind(
    nethermind_path: Path,
    build_dir: Path | None = None,
    force: bool = False,
) -> bool:
    """Build Nethermind if project has changed.

    Uses git to detect changes and only rebuilds when necessary.

    Args:
        nethermind_path: Path to Nethermind repository root
        build_dir: Build output directory (for state tracking)
        force: Force rebuild even if no changes detected

    Returns:
        True if build succeeded (or was skipped), False on failure
    """
    if not nethermind_path.exists():
        _logger.warning(f"Nethermind path not found: {nethermind_path}")
        return False

    # Use a marker file in build dir to track build state
    if build_dir is None:
        build_dir = nethermind_path / "src" / "Nethermind" / "artifacts"

    state_file = nethermind_path / BUILD_STATE_FILE
    marker_file = build_dir / ".build-complete"

    # Check if rebuild is needed
    if not force and not _needs_rebuild(
        project_path=nethermind_path,
        binary_path=marker_file,
        state_file=state_file,
        project_key="nethermind",
        check_paths=["src/Nethermind.Arbitrum/", "src/Nethermind/"],
    ):
        _logger.info("Nethermind build up-to-date, skipping rebuild")
        return True

    _logger.info("Building Nethermind (this may take a few minutes)...")
    start_time = time.time()

    # Find the solution file (try .slnx first, then .sln)
    sln_candidates = [
        nethermind_path / "src" / "Nethermind.Arbitrum.slnx",
        nethermind_path / "src" / "Nethermind" / "Nethermind.sln",
        nethermind_path / "Nethermind.sln",
    ]
    sln_file = None
    for candidate in sln_candidates:
        if candidate.exists():
            sln_file = candidate
            break

    if sln_file is None:
        _logger.error("Nethermind solution file not found")
        return False

    # Build only the Runner project (not tests) to avoid test compilation errors
    runner_project = nethermind_path / "src" / "Nethermind" / "src" / "Nethermind" / "Nethermind.Runner" / "Nethermind.Runner.csproj"
    if not runner_project.exists():
        # Fallback to solution if project not found
        runner_project = sln_file

    result = subprocess.run(
        [
            "dotnet",
            "build",
            str(runner_project),
            "-c", "Debug",
        ],
        cwd=str(nethermind_path),
        capture_output=True,
        text=True,
    )

    build_time = time.time() - start_time

    if result.returncode != 0:
        _logger.error(f"Failed to build Nethermind: {result.stderr}")
        return False

    _logger.info(f"Nethermind built successfully in {build_time:.1f}s")

    # Create marker file and record build
    build_dir.mkdir(parents=True, exist_ok=True)
    marker_file.touch()
    _record_build(state_file, "nethermind", nethermind_path,
                  ["src/Nethermind.Arbitrum/", "src/Nethermind/"])

    return True


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
