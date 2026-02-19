"""Tests for sequential_runner module.

This test suite addresses the critical gap of 0% coverage for the
sequential test runner. Tests cover:
- Runner lifecycle (startup, run, cleanup)
- Signal handling for interruption
- Fail-fast mode
- Error handling for startup failures
- Integration with mocked components
"""

from __future__ import annotations

import signal
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from comparison_runner.exceptions import NethermindStartupError, RPCError
from comparison_runner.models import TestResult, TestStatus
from comparison_runner.sequential_runner import (
    SequentialRunner,
    SequentialRunnerConfig,
)


@pytest.fixture
def config(tmp_path: Path) -> SequentialRunnerConfig:
    """Create a test configuration."""
    return SequentialRunnerConfig(
        nitro_path=tmp_path / "nitro",
        nethermind_build_dir=tmp_path / "build",
        host="127.0.0.1",
        port=20551,
        data_dir=tmp_path / "data",
        log_dir=tmp_path / "logs",
        startup_timeout_s=5,
        fail_fast=False,
        verbose=False,
    )


@pytest.fixture
def runner(config: SequentialRunnerConfig) -> SequentialRunner:
    """Create a runner instance."""
    return SequentialRunner(config)


class TestSequentialRunnerInit:
    """Tests for SequentialRunner initialization."""

    def test_creates_nethermind_process(self, runner: SequentialRunner) -> None:
        """Runner creates NethermindProcess with correct port."""
        assert runner.process.port == 20551
        assert runner.process.host == "127.0.0.1"

    def test_initializes_interrupted_event(self, runner: SequentialRunner) -> None:
        """Runner initializes interrupted event cleared."""
        assert not runner.interrupted.is_set()

    def test_uses_custom_data_dir(self, config: SequentialRunnerConfig) -> None:
        """Runner uses configured data directory."""
        runner = SequentialRunner(config)
        assert runner.process.data_dir == config.data_dir

    def test_defaults_data_dir_to_cwd(self, tmp_path: Path) -> None:
        """Runner defaults data_dir to .data in cwd when not specified."""
        config = SequentialRunnerConfig(
            nitro_path=tmp_path / "nitro",
            nethermind_build_dir=tmp_path / "build",
            data_dir=None,
        )
        runner = SequentialRunner(config)
        assert ".data" in str(runner.process.data_dir)


class TestSequentialRunnerRun:
    """Tests for SequentialRunner.run() method."""

    def test_empty_test_list_returns_empty(self, runner: SequentialRunner) -> None:
        """Running with empty test list returns empty results."""
        results = runner.run([])
        assert results == []

    @patch.object(SequentialRunner, "_startup")
    @patch.object(SequentialRunner, "_run_test_loop")
    @patch.object(SequentialRunner, "_cleanup")
    def test_calls_lifecycle_methods(
        self,
        mock_cleanup: MagicMock,
        mock_run_loop: MagicMock,
        mock_startup: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Run calls startup, run_loop, and cleanup in order."""
        mock_run_loop.return_value = []

        runner.run(["TestA"])

        mock_startup.assert_called_once()
        mock_run_loop.assert_called_once_with(["TestA"])
        mock_cleanup.assert_called_once()

    @patch.object(SequentialRunner, "_startup")
    @patch.object(SequentialRunner, "_cleanup")
    def test_cleanup_called_on_startup_failure(
        self,
        mock_cleanup: MagicMock,
        mock_startup: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Cleanup is called even when startup fails."""
        mock_startup.side_effect = NethermindStartupError("Failed", port=20551)

        results = runner.run(["TestA", "TestB"])

        mock_cleanup.assert_called_once()
        assert len(results) == 2
        assert all(r.status == TestStatus.FAILED for r in results)
        assert "Startup failed" in results[0].error_msg

    @patch.object(SequentialRunner, "_startup")
    @patch.object(SequentialRunner, "_run_test_loop")
    @patch.object(SequentialRunner, "_cleanup")
    def test_returns_results_from_run_loop(
        self,
        mock_cleanup: MagicMock,
        mock_run_loop: MagicMock,
        mock_startup: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Run returns results from run_loop."""
        expected = [
            TestResult(name="TestA", status=TestStatus.PASSED),
            TestResult(name="TestB", status=TestStatus.FAILED),
        ]
        mock_run_loop.return_value = expected

        results = runner.run(["TestA", "TestB"])

        assert results == expected


class TestRunSingleTest:
    """Tests for _run_single_test method."""

    @patch("comparison_runner.sequential_runner.reinitialize_state")
    @patch("comparison_runner.sequential_runner.execute_test")
    def test_calls_reinitialize_then_execute(
        self,
        mock_execute: MagicMock,
        mock_reinit: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Single test calls reinitialize_state then execute_test."""
        mock_execute.return_value = TestResult(
            name="TestA",
            status=TestStatus.PASSED,
            duration_s=1.0,
        )

        result = runner._run_single_test("TestA")

        # Debug port = base port + 8000
        mock_reinit.assert_called_once_with(
            host="127.0.0.1",
            port=28551,
            test_name="TestA",
        )
        mock_execute.assert_called_once()
        assert result.status == TestStatus.PASSED

    @patch("comparison_runner.sequential_runner.reinitialize_state")
    def test_returns_failed_on_reinit_error(
        self,
        mock_reinit: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Returns failed result when reinitialize_state raises RPCError."""
        mock_reinit.side_effect = RPCError(
            "Connection failed",
            method="debug_reinitialize",
            port=20551,
        )

        result = runner._run_single_test("TestA")

        assert result.status == TestStatus.FAILED
        assert "reinitialize failed" in result.error_msg
        assert result.name == "TestA"

    @patch("comparison_runner.sequential_runner.reinitialize_state")
    @patch("comparison_runner.sequential_runner.execute_test")
    def test_passes_correct_params_to_execute(
        self,
        mock_execute: MagicMock,
        mock_reinit: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Execute_test is called with correct parameters."""
        mock_execute.return_value = TestResult(name="TestA", status=TestStatus.PASSED)

        runner._run_single_test("TestA")

        call_kwargs = mock_execute.call_args.kwargs
        assert call_kwargs["test_name"] == "TestA"
        assert call_kwargs["nitro_path"] == runner.config.nitro_path
        assert call_kwargs["nethermind_host"] == "127.0.0.1"
        assert call_kwargs["nethermind_port"] == 20551
        assert call_kwargs["worker_id"] == -1  # Sequential mode


class TestRunTestLoop:
    """Tests for _run_test_loop method."""

    @patch.object(SequentialRunner, "_run_single_test")
    def test_runs_all_tests_in_order(
        self,
        mock_run_single: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Test loop executes all tests in order."""
        results_sequence = [
            TestResult(name="TestA", status=TestStatus.PASSED),
            TestResult(name="TestB", status=TestStatus.PASSED),
            TestResult(name="TestC", status=TestStatus.PASSED),
        ]
        mock_run_single.side_effect = results_sequence

        results = runner._run_test_loop(["TestA", "TestB", "TestC"])

        assert len(results) == 3
        assert mock_run_single.call_count == 3
        # Verify order
        calls = [c.args[0] for c in mock_run_single.call_args_list]
        assert calls == ["TestA", "TestB", "TestC"]

    @patch.object(SequentialRunner, "_run_single_test")
    def test_marks_skipped_when_interrupted(
        self,
        mock_run_single: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Tests are marked skipped when interrupted."""
        runner.interrupted.set()

        results = runner._run_test_loop(["TestA", "TestB"])

        assert len(results) == 2
        assert all(r.status == TestStatus.SKIPPED for r in results)
        assert all("interrupted" in r.error_msg for r in results)
        mock_run_single.assert_not_called()

    @patch.object(SequentialRunner, "_run_single_test")
    def test_fail_fast_stops_on_failure(
        self,
        mock_run_single: MagicMock,
        config: SequentialRunnerConfig,
    ) -> None:
        """Fail-fast mode stops after first failure."""
        config = SequentialRunnerConfig(
            nitro_path=config.nitro_path,
            nethermind_build_dir=config.nethermind_build_dir,
            fail_fast=True,
        )
        runner = SequentialRunner(config)

        mock_run_single.side_effect = [
            TestResult(name="TestA", status=TestStatus.PASSED),
            TestResult(name="TestB", status=TestStatus.FAILED),
        ]

        results = runner._run_test_loop(["TestA", "TestB", "TestC"])

        assert len(results) == 3
        assert results[0].status == TestStatus.PASSED
        assert results[1].status == TestStatus.FAILED
        assert results[2].status == TestStatus.SKIPPED
        assert "fail-fast" in results[2].error_msg
        assert mock_run_single.call_count == 2  # Stopped after failure


class TestSignalHandling:
    """Tests for signal handling."""

    def test_stop_sets_interrupted(self, runner: SequentialRunner) -> None:
        """Stop() sets the interrupted event."""
        assert not runner.interrupted.is_set()

        runner.stop()

        assert runner.interrupted.is_set()

    def test_signal_handlers_setup_and_restore(
        self,
        runner: SequentialRunner,
    ) -> None:
        """Signal handlers are setup and restored correctly."""
        original_sigint = signal.getsignal(signal.SIGINT)

        runner._setup_signal_handlers()
        assert signal.getsignal(signal.SIGINT) != original_sigint
        assert signal.SIGINT in runner._original_handlers

        runner._restore_signal_handlers()
        assert signal.getsignal(signal.SIGINT) == original_sigint
        assert len(runner._original_handlers) == 0


class TestStartupAndCleanup:
    """Tests for _startup and _cleanup methods."""

    @patch("comparison_runner.sequential_runner.NethermindProcess")
    def test_startup_starts_and_waits_for_ready(
        self,
        mock_process_cls: MagicMock,
        config: SequentialRunnerConfig,
    ) -> None:
        """Startup starts process and waits for ready."""
        mock_process = MagicMock()
        mock_process.wait_for_ready.return_value = True
        mock_process_cls.return_value = mock_process

        runner = SequentialRunner(config)
        runner.process = mock_process

        runner._startup()

        mock_process.start.assert_called_once()
        mock_process.wait_for_ready.assert_called_once_with(5)

    @patch("comparison_runner.sequential_runner.NethermindProcess")
    def test_startup_raises_on_timeout(
        self,
        mock_process_cls: MagicMock,
        config: SequentialRunnerConfig,
    ) -> None:
        """Startup raises NethermindStartupError on timeout."""
        mock_process = MagicMock()
        mock_process.wait_for_ready.return_value = False
        mock_process_cls.return_value = mock_process

        runner = SequentialRunner(config)
        runner.process = mock_process

        with pytest.raises(NethermindStartupError) as exc_info:
            runner._startup()

        assert "failed to become ready" in str(exc_info.value).lower()
        mock_process.stop.assert_called_once()

    def test_cleanup_stops_process(self, runner: SequentialRunner) -> None:
        """Cleanup stops the Nethermind process."""
        runner.process.stop = MagicMock()

        runner._cleanup()

        runner.process.stop.assert_called_once()


class TestIntegration:
    """Integration tests with realistic component mocking."""

    @patch("comparison_runner.sequential_runner.reinitialize_state")
    @patch("comparison_runner.sequential_runner.execute_test")
    @patch.object(SequentialRunner, "_startup")
    @patch.object(SequentialRunner, "_cleanup")
    def test_full_run_flow(
        self,
        mock_cleanup: MagicMock,
        mock_startup: MagicMock,
        mock_execute: MagicMock,
        mock_reinit: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Full run executes tests and returns results."""
        mock_execute.side_effect = [
            TestResult(name="TestA", status=TestStatus.PASSED, duration_s=1.0),
            TestResult(name="TestB", status=TestStatus.FAILED, duration_s=2.0),
        ]

        results = runner.run(["TestA", "TestB"])

        assert len(results) == 2
        assert results[0].status == TestStatus.PASSED
        assert results[1].status == TestStatus.FAILED
        assert mock_reinit.call_count == 2
        assert mock_execute.call_count == 2

    @patch("comparison_runner.sequential_runner.reinitialize_state")
    @patch("comparison_runner.sequential_runner.execute_test")
    @patch.object(SequentialRunner, "_startup")
    @patch.object(SequentialRunner, "_cleanup")
    def test_timing_data_recorded(
        self,
        mock_cleanup: MagicMock,
        mock_startup: MagicMock,
        mock_execute: MagicMock,
        mock_reinit: MagicMock,
        runner: SequentialRunner,
    ) -> None:
        """Results contain timing data."""
        mock_execute.return_value = TestResult(
            name="TestA",
            status=TestStatus.PASSED,
            duration_s=1.5,
        )

        results = runner.run(["TestA"])

        assert results[0].duration_s == 1.5
