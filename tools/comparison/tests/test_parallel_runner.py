"""Tests for parallel_runner module.

This test suite covers the parallel test runner functionality:
- Runner initialization and configuration
- Worker lifecycle and distribution
- Thread-safe result collection
- Interruption handling
- Health check and restart logic
"""

from __future__ import annotations

import threading
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from comparison_runner.exceptions import RPCError
from comparison_runner.models import TestResult, TestStatus, WorkerInstance
from comparison_runner.parallel_runner import (
    ParallelRunner,
    ParallelRunnerConfig,
)
from comparison_runner.worker_pool import WorkQueue


@pytest.fixture
def config(tmp_path: Path) -> ParallelRunnerConfig:
    """Create a test configuration."""
    return ParallelRunnerConfig(
        nitro_path=tmp_path / "nitro",
        nethermind_build_dir=tmp_path / "build",
        max_workers=4,
        host="127.0.0.1",
        base_port=20551,
        root_dir=tmp_path / "root",
        log_dir=tmp_path / "logs",
        startup_timeout_s=5,
    )


@pytest.fixture
def runner(config: ParallelRunnerConfig) -> ParallelRunner:
    """Create a runner instance."""
    return ParallelRunner(config)


class TestParallelRunnerInit:
    """Tests for ParallelRunner initialization."""

    def test_initializes_with_config(self, runner: ParallelRunner) -> None:
        """Runner stores configuration."""
        assert runner.config.max_workers == 4
        assert runner.config.base_port == 20551
        assert runner.config.host == "127.0.0.1"

    def test_initializes_empty_results(self, runner: ParallelRunner) -> None:
        """Runner initializes with empty results list."""
        assert runner._results == []

    def test_initializes_interrupted_cleared(self, runner: ParallelRunner) -> None:
        """Runner initializes with interrupted event cleared."""
        assert not runner.interrupted.is_set()

    def test_pool_initially_none(self, runner: ParallelRunner) -> None:
        """Pool is None before run() is called."""
        assert runner._pool is None


class TestParallelRunnerRun:
    """Tests for ParallelRunner.run() method."""

    def test_empty_test_list_returns_empty(self, runner: ParallelRunner) -> None:
        """Running with empty test list returns empty results."""
        results = runner.run([])
        assert results == []

    @patch("comparison_runner.parallel_runner.InstancePool")
    @patch.object(ParallelRunner, "_run_workers")
    def test_caps_workers_at_test_count(
        self,
        mock_run_workers: MagicMock,
        mock_pool_cls: MagicMock,
        runner: ParallelRunner,
    ) -> None:
        """Number of workers is capped at test count."""
        mock_pool = MagicMock()
        mock_pool.__enter__ = MagicMock(return_value=mock_pool)
        mock_pool.__exit__ = MagicMock(return_value=False)
        mock_pool_cls.return_value = mock_pool

        runner.run(["TestA", "TestB"])  # 2 tests, 4 max workers

        # Check that workers is capped at 2
        pool_config = mock_pool_cls.call_args.kwargs["config"]
        assert pool_config.max_workers == 2

    @patch("comparison_runner.parallel_runner.InstancePool")
    @patch.object(ParallelRunner, "_run_workers")
    def test_creates_pool_with_context_manager(
        self,
        mock_run_workers: MagicMock,
        mock_pool_cls: MagicMock,
        runner: ParallelRunner,
    ) -> None:
        """Pool is used as context manager for cleanup."""
        mock_pool = MagicMock()
        mock_pool.__enter__ = MagicMock(return_value=mock_pool)
        mock_pool.__exit__ = MagicMock(return_value=False)
        mock_pool_cls.return_value = mock_pool

        runner.run(["TestA"])

        mock_pool.__enter__.assert_called_once()
        mock_pool.__exit__.assert_called_once()

    @patch("comparison_runner.parallel_runner.InstancePool")
    @patch.object(ParallelRunner, "_run_workers")
    def test_clears_state_on_run(
        self,
        mock_run_workers: MagicMock,
        mock_pool_cls: MagicMock,
        runner: ParallelRunner,
    ) -> None:
        """Run clears previous results and interrupted state."""
        mock_pool = MagicMock()
        mock_pool.__enter__ = MagicMock(return_value=mock_pool)
        mock_pool.__exit__ = MagicMock(return_value=False)
        mock_pool_cls.return_value = mock_pool

        # Simulate previous state
        runner._results = [TestResult(name="Old", status=TestStatus.PASSED)]
        runner.interrupted.set()

        runner.run(["TestA"])

        assert runner._results == []  # Cleared
        assert not runner.interrupted.is_set()  # Cleared


class TestStop:
    """Tests for stop() method."""

    def test_stop_sets_interrupted(self, runner: ParallelRunner) -> None:
        """Stop sets the interrupted event."""
        assert not runner.interrupted.is_set()

        runner.stop()

        assert runner.interrupted.is_set()


class TestRunSingleTest:
    """Tests for _run_single_test method."""

    @patch("comparison_runner.parallel_runner.reinitialize_state")
    @patch("comparison_runner.parallel_runner.execute_test")
    def test_calls_reinitialize_then_execute(
        self,
        mock_execute: MagicMock,
        mock_reinit: MagicMock,
        runner: ParallelRunner,
    ) -> None:
        """Single test calls reinitialize_state then execute_test."""
        mock_execute.return_value = TestResult(
            name="TestA",
            status=TestStatus.PASSED,
            duration_s=1.0,
            worker_id=0,
        )

        result = runner._run_single_test(worker_id=0, port=20551, test_name="TestA")

        # Debug port = base port + 8000
        mock_reinit.assert_called_once_with(
            host="127.0.0.1",
            port=28551,
            test_name="TestA",
        )
        mock_execute.assert_called_once()
        assert result.status == TestStatus.PASSED

    @patch("comparison_runner.parallel_runner.reinitialize_state")
    def test_returns_failed_on_reinit_error(
        self,
        mock_reinit: MagicMock,
        runner: ParallelRunner,
    ) -> None:
        """Returns failed result when reinitialize_state raises RPCError."""
        mock_reinit.side_effect = RPCError(
            "Connection failed",
            method="debug_reinitialize",
            port=20551,
        )

        result = runner._run_single_test(worker_id=0, port=20551, test_name="TestA")

        assert result.status == TestStatus.FAILED
        assert "reinitialize failed" in result.error_msg
        assert result.worker_id == 0

    @patch("comparison_runner.parallel_runner.reinitialize_state")
    @patch("comparison_runner.parallel_runner.execute_test")
    def test_passes_worker_id_to_execute(
        self,
        mock_execute: MagicMock,
        mock_reinit: MagicMock,
        runner: ParallelRunner,
    ) -> None:
        """Execute_test receives correct worker_id."""
        mock_execute.return_value = TestResult(name="TestA", status=TestStatus.PASSED)

        runner._run_single_test(worker_id=3, port=20554, test_name="TestA")

        call_kwargs = mock_execute.call_args.kwargs
        assert call_kwargs["worker_id"] == 3
        assert call_kwargs["nethermind_port"] == 20554


class TestRecordResult:
    """Tests for _record_result method."""

    def test_appends_result_thread_safe(self, runner: ParallelRunner) -> None:
        """Results are appended thread-safely."""
        results_to_add = [
            TestResult(name=f"Test{i}", status=TestStatus.PASSED, worker_id=i % 4)
            for i in range(100)
        ]

        threads = [
            threading.Thread(target=runner._record_result, args=(r,)) for r in results_to_add
        ]

        for t in threads:
            t.start()
        for t in threads:
            t.join()

        assert len(runner._results) == 100

    def test_logs_passed_result(self, runner: ParallelRunner) -> None:
        """Passed results are logged at INFO level."""
        result = TestResult(
            name="TestA",
            status=TestStatus.PASSED,
            duration_s=1.0,
            worker_id=0,
        )

        with patch.object(runner.logger, "info") as mock_log:
            runner._record_result(result)
            mock_log.assert_called_once()
            assert "TestA" in mock_log.call_args[0][0]
            assert "✓" in mock_log.call_args[0][0]  # Uses checkmark symbol

    def test_logs_failed_result(self, runner: ParallelRunner) -> None:
        """Failed results are logged at ERROR level."""
        result = TestResult(
            name="TestA",
            status=TestStatus.FAILED,
            duration_s=1.0,
            worker_id=0,
        )

        with patch.object(runner.logger, "error") as mock_log:
            runner._record_result(result)
            mock_log.assert_called_once()


class TestHealthCheck:
    """Tests for _handle_health_check method."""

    def test_healthy_worker_no_restart(self, runner: ParallelRunner) -> None:
        """Healthy worker does not trigger restart."""
        mock_pool = MagicMock()
        mock_pool.check_health.return_value = True
        runner._pool = mock_pool

        runner._handle_health_check(worker_id=0)

        mock_pool.check_health.assert_called_once_with(0)
        mock_pool.restart_worker.assert_not_called()

    def test_unhealthy_worker_triggers_restart(self, runner: ParallelRunner) -> None:
        """Unhealthy worker triggers restart attempt."""
        mock_pool = MagicMock()
        mock_pool.check_health.return_value = False
        mock_pool.restart_worker.return_value = True
        runner._pool = mock_pool

        runner._handle_health_check(worker_id=0)

        mock_pool.restart_worker.assert_called_once_with(0, runner.config.startup_timeout_s)

    def test_failed_restart_logs_error_but_continues(self, runner: ParallelRunner) -> None:
        """Failed restart logs error but other workers continue (no global interrupt)."""
        mock_pool = MagicMock()
        mock_pool.check_health.return_value = False
        mock_pool.restart_worker.return_value = False
        runner._pool = mock_pool

        runner._handle_health_check(worker_id=0)

        # Worker failure should NOT set global interrupt - other workers continue
        assert not runner.interrupted.is_set()


class TestWorkerLoop:
    """Tests for _worker_loop method."""

    @patch.object(ParallelRunner, "_run_single_test")
    @patch.object(ParallelRunner, "_record_result")
    @patch.object(ParallelRunner, "_handle_health_check")
    def test_processes_tests_from_queue(
        self,
        mock_health: MagicMock,
        mock_record: MagicMock,
        mock_run_single: MagicMock,
        runner: ParallelRunner,
    ) -> None:
        """Worker processes tests until queue is empty."""
        mock_pool = MagicMock()
        mock_pool.start_worker.return_value = True
        mock_pool.get_or_create.return_value = WorkerInstance(
            worker_id=0,
            nethermind_port=20551,
        )
        runner._pool = mock_pool

        mock_run_single.return_value = TestResult(name="TestA", status=TestStatus.PASSED)

        work_queue = WorkQueue(["TestA", "TestB"])

        runner._worker_loop(worker_id=0, work_queue=work_queue)

        assert mock_run_single.call_count == 2
        assert mock_record.call_count == 2
        assert work_queue.is_empty()

    @patch.object(ParallelRunner, "_run_single_test")
    def test_stops_on_interrupted(
        self,
        mock_run_single: MagicMock,
        runner: ParallelRunner,
    ) -> None:
        """Worker stops when interrupted event is set."""
        mock_pool = MagicMock()
        mock_pool.start_worker.return_value = True
        mock_pool.get_or_create.return_value = WorkerInstance(
            worker_id=0,
            nethermind_port=20551,
        )
        runner._pool = mock_pool

        work_queue = WorkQueue(["TestA", "TestB", "TestC"])
        runner.interrupted.set()

        runner._worker_loop(worker_id=0, work_queue=work_queue)

        mock_run_single.assert_not_called()

    def test_fails_gracefully_on_startup_failure(self, runner: ParallelRunner) -> None:
        """Worker exits gracefully if instance fails to start."""
        mock_pool = MagicMock()
        mock_pool.start_worker.return_value = False
        runner._pool = mock_pool

        work_queue = WorkQueue(["TestA"])

        # Should not raise
        runner._worker_loop(worker_id=0, work_queue=work_queue)

        # Queue should still have the test (worker didn't process it)
        assert not work_queue.is_empty()


class TestIntegration:
    """Integration tests with realistic mocking."""

    @patch("comparison_runner.parallel_runner.InstancePool")
    @patch("comparison_runner.parallel_runner.reinitialize_state")
    @patch("comparison_runner.parallel_runner.execute_test")
    def test_full_parallel_run(
        self,
        mock_execute: MagicMock,
        mock_reinit: MagicMock,
        mock_pool_cls: MagicMock,
        config: ParallelRunnerConfig,
    ) -> None:
        """Full parallel run executes all tests."""
        # Setup mock pool
        mock_pool = MagicMock()
        mock_pool.__enter__ = MagicMock(return_value=mock_pool)
        mock_pool.__exit__ = MagicMock(return_value=False)
        mock_pool.start_worker.return_value = True
        mock_pool.check_health.return_value = True

        def get_or_create(worker_id: int) -> WorkerInstance:
            return WorkerInstance(
                worker_id=worker_id,
                nethermind_port=20551 + worker_id,
            )

        mock_pool.get_or_create.side_effect = get_or_create
        mock_pool_cls.return_value = mock_pool

        # Setup mock execute to return different results
        call_count = [0]

        def execute_side_effect(**kwargs):
            call_count[0] += 1
            return TestResult(
                name=kwargs["test_name"],
                status=TestStatus.PASSED,
                duration_s=0.1,
                worker_id=kwargs["worker_id"],
            )

        mock_execute.side_effect = execute_side_effect

        # Run with 2 workers and 4 tests
        config_2w = ParallelRunnerConfig(
            nitro_path=config.nitro_path,
            nethermind_build_dir=config.nethermind_build_dir,
            max_workers=2,
        )
        runner = ParallelRunner(config_2w)
        results = runner.run(["TestA", "TestB", "TestC", "TestD"])

        # All tests should be executed
        assert len(results) == 4
        assert all(r.status == TestStatus.PASSED for r in results)
        assert mock_reinit.call_count == 4
        assert mock_execute.call_count == 4
