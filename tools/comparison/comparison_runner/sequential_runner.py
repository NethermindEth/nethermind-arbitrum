"""Sequential test runner for comparison testing.

This module provides a SequentialRunner class that orchestrates test execution
using a single persistent Nethermind instance. Tests are run one at a time with
state reinitialization between each test.

Features:
- Composition over inheritance (uses NethermindProcess, not God class)
- Small focused methods (<50 lines each)
- Signal handling for clean interruption
- Unified TestResult return type
"""

from __future__ import annotations

import logging
import signal
import time
from dataclasses import dataclass
from pathlib import Path
from threading import Event
from typing import Any

from .config import DEBUG_PORT_OFFSET, DEFAULT_STARTUP_TIMEOUT_S
from .exceptions import NethermindStartupError, RPCError
from .models import TestResult, TestStatus
from .process_manager import NethermindProcess
from .rpc_client import reinitialize_state
from .test_executor import clean_test_cache, compile_test_binary, execute_test


@dataclass
class SequentialRunnerConfig:
    """Configuration specific to sequential runner.

    Extends ComparisonConfig with sequential-mode-specific options.
    """

    nitro_path: Path
    nethermind_build_dir: Path
    host: str = "127.0.0.1"
    port: int = 20551
    data_dir: Path | None = None
    log_dir: Path | None = None
    startup_timeout_s: int = DEFAULT_STARTUP_TIMEOUT_S
    fail_fast: bool = False
    verbose: bool = False
    max_retries: int = 0


class SequentialRunner:
    """Orchestrates sequential test execution with a persistent Nethermind instance.

    Uses composition to combine NethermindProcess, reinitialize_state, and
    execute_test into a cohesive test runner. This replaces the monolithic
    TestRunner class with a focused, maintainable implementation.

    Example:
        >>> config = SequentialRunnerConfig(
        ...     nitro_path=Path("/path/to/nitro"),
        ...     nethermind_build_dir=Path("/path/to/nethermind/build"),
        ... )
        >>> runner = SequentialRunner(config)
        >>> results = runner.run(["TestA", "TestB", "TestC"])
    """

    def __init__(self, config: SequentialRunnerConfig) -> None:
        """Initialize the sequential runner.

        Args:
            config: Runner configuration
        """
        self.config = config
        self.logger = logging.getLogger(__name__)

        # Determine data directory
        data_dir = config.data_dir or Path.cwd() / ".data"

        # Compose the Nethermind process manager
        self.process = NethermindProcess(
            port=config.port,
            data_dir=data_dir,
            host=config.host,
        )

        # Interruption handling
        self.interrupted = Event()
        self._original_handlers: dict[signal.Signals, Any] = {}

        # Pre-compiled test binary (set in run())
        self._test_binary: Path | None = None

    def run(self, tests: list[str]) -> list[TestResult]:
        """Run tests sequentially.

        Args:
            tests: List of test names to execute

        Returns:
            List of TestResult for all tests
        """
        if not tests:
            return []

        # Clean Go test cache once before starting any tests
        clean_test_cache(self.config.nitro_path)

        # Pre-compile Go test binary once (separates compilation from test timing)
        self._test_binary = compile_test_binary(self.config.nitro_path)

        results: list[TestResult] = []

        try:
            self._setup_signal_handlers()
            self._startup()
            results = self._run_test_loop(tests)
        except NethermindStartupError as e:
            self.logger.error(f"Startup failed: {e}")
            # Return failed results for all tests
            results = [
                TestResult(
                    name=test,
                    status=TestStatus.FAILED,
                    error_msg=f"Startup failed: {e}",
                )
                for test in tests
            ]
        finally:
            self._cleanup()
            self._restore_signal_handlers()

        return results

    def stop(self) -> None:
        """Signal the runner to stop (for external interrupt handling)."""
        self.interrupted.set()

    def _startup(self) -> None:
        """Start Nethermind and wait for it to be ready.

        Raises:
            NethermindStartupError: If Nethermind fails to start or become ready
        """
        self.logger.info(f"Starting Nethermind on port {self.config.port}...")

        # Determine log path
        log_path = None
        if self.config.log_dir:
            log_path = self.config.log_dir / "nethermind.log"

        # Start the process
        self.process.start(
            build_dir=self.config.nethermind_build_dir,
            log_path=log_path,
        )

        # Wait for ready
        if not self.process.wait_for_ready(self.config.startup_timeout_s):
            self.process.stop()
            raise NethermindStartupError(
                f"Nethermind failed to become ready within {self.config.startup_timeout_s}s",
                port=self.config.port,
            )

        self.logger.info("Nethermind is ready")

    def _run_test_loop(self, tests: list[str]) -> list[TestResult]:
        """Execute tests one by one.

        Args:
            tests: List of test names

        Returns:
            List of TestResult
        """
        results: list[TestResult] = []
        max_attempts = self.config.max_retries + 1

        for i, test_name in enumerate(tests):
            if self.interrupted.is_set():
                # Mark remaining tests as skipped
                results.append(
                    TestResult(
                        name=test_name,
                        status=TestStatus.SKIPPED,
                        error_msg="interrupted",
                    )
                )
                continue

            result = self._run_with_retries(test_name, max_attempts)
            results.append(result)

            # Log progress
            self._log_result(result, i + 1, len(tests))

            # Fail-fast check (only on final outcome, not individual attempts)
            if self.config.fail_fast and result.status == TestStatus.FAILED:
                self.logger.info("Stopping due to fail-fast")
                # Mark remaining as skipped
                for remaining in tests[i + 1 :]:
                    results.append(
                        TestResult(
                            name=remaining,
                            status=TestStatus.SKIPPED,
                            error_msg="skipped due to fail-fast",
                        )
                    )
                break

        return results

    def _run_with_retries(self, test_name: str, max_attempts: int) -> TestResult:
        """Run a test with retries on failure.

        Returns the final TestResult with attempt tracking.
        Duration accumulates across all attempts.
        """
        total_duration = 0.0
        result = TestResult(name=test_name, status=TestStatus.FAILED)

        for attempt in range(1, max_attempts + 1):
            if self.interrupted.is_set():
                result.attempt = attempt
                result.max_attempts = max_attempts
                return result

            result = self._run_single_test(test_name)
            total_duration += result.duration_s
            result.attempt = attempt
            result.max_attempts = max_attempts
            result.duration_s = total_duration

            if result.status == TestStatus.PASSED:
                if attempt > 1:
                    self.logger.info(
                        f"  \u21b3 {test_name}: PASSED on attempt {attempt}/{max_attempts} (flaky)"
                    )
                return result

            if attempt < max_attempts:
                self.logger.warning(
                    f"  \u21b3 {test_name}: FAILED attempt {attempt}/{max_attempts}, retrying..."
                )
            else:
                self.logger.error(f"  \u21b3 {test_name}: FAILED all {max_attempts} attempts")

        return result

    def _run_single_test(self, test_name: str) -> TestResult:
        """Run a single test with state reinitialization.

        Args:
            test_name: Name of the test

        Returns:
            TestResult with an outcome
        """
        start_time = time.time()

        # Reinitialize state via RPC
        try:
            reinitialize_state(
                host=self.config.host,
                port=self.config.port + DEBUG_PORT_OFFSET,
                test_name=test_name,
            )
        except RPCError as e:
            return TestResult(
                name=test_name,
                status=TestStatus.FAILED,
                duration_s=time.time() - start_time,
                error_msg=f"reinitialize failed: {e}",
            )

        # Execute the test
        return execute_test(
            test_name=test_name,
            nitro_path=self.config.nitro_path,
            nethermind_host=self.config.host,
            nethermind_port=self.config.port,
            log_dir=self.config.log_dir,
            worker_id=-1,  # Sequential mode
            interrupted=self.interrupted,
            test_binary=self._test_binary,
        )

    def _cleanup(self) -> None:
        """Stop Nethermind and clean up resources."""
        self.logger.info("Stopping Nethermind...")
        self.process.stop()

    def _log_result(self, result: TestResult, current: int, total: int) -> None:
        """Log a test result with progress."""
        pct = (current / total * 100) if total > 0 else 0
        duration_str = f"({result.duration_s:.1f}s)" if result.duration_s else ""
        status_icon = "✓" if result.status == TestStatus.PASSED else "✗"

        msg = f"[{current}/{total} {pct:>3.0f}%] {result.name}: {status_icon} {duration_str}"
        if result.error_msg:
            msg += f" - {result.error_msg}"

        if result.status == TestStatus.PASSED:
            self.logger.info(msg)
        elif result.status == TestStatus.FAILED:
            self.logger.error(msg)
        else:
            self.logger.warning(msg)

    def _setup_signal_handlers(self) -> None:
        """Install signal handlers for graceful interruption."""

        def handler(signum: int, _frame: object) -> None:
            self.logger.info(f"Received signal {signum}, stopping...")
            self.interrupted.set()

        for sig in (signal.SIGINT, signal.SIGTERM):
            self._original_handlers[sig] = signal.signal(sig, handler)

    def _restore_signal_handlers(self) -> None:
        """Restore original signal handlers."""
        for sig, handler in self._original_handlers.items():
            signal.signal(sig, handler)
        self._original_handlers.clear()
