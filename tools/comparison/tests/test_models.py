"""Tests for comparison_runner.models module."""

from unittest.mock import MagicMock

from comparison_runner.models import (
    TestResult,
    TestStatus,
    WorkerInstance,
)


class TestTestStatus:
    """Tests for TestStatus enum."""

    def test_icon_pending(self) -> None:
        """PENDING status returns correct icon."""
        assert TestStatus.PENDING.icon == "○ PENDING"

    def test_icon_running(self) -> None:
        """RUNNING status returns correct icon."""
        assert TestStatus.RUNNING.icon == "◐ RUNNING"

    def test_icon_passed(self) -> None:
        """PASSED status returns correct icon."""
        assert TestStatus.PASSED.icon == "✓ PASS"

    def test_icon_failed(self) -> None:
        """FAILED status returns correct icon."""
        assert TestStatus.FAILED.icon == "✗ FAIL"

    def test_icon_timeout(self) -> None:
        """TIMEOUT status returns correct icon."""
        assert TestStatus.TIMEOUT.icon == "⏱ TIMEOUT"

    def test_icon_skipped(self) -> None:
        """SKIPPED status returns correct icon."""
        assert TestStatus.SKIPPED.icon == "⊘ SKIPPED"

    def test_is_terminal_pending(self) -> None:
        """PENDING is not a terminal state."""
        assert not TestStatus.PENDING.is_terminal()

    def test_is_terminal_running(self) -> None:
        """RUNNING is not a terminal state."""
        assert not TestStatus.RUNNING.is_terminal()

    def test_is_terminal_passed(self) -> None:
        """PASSED is a terminal state."""
        assert TestStatus.PASSED.is_terminal()

    def test_is_terminal_failed(self) -> None:
        """FAILED is a terminal state."""
        assert TestStatus.FAILED.is_terminal()

    def test_is_terminal_timeout(self) -> None:
        """TIMEOUT is a terminal state."""
        assert TestStatus.TIMEOUT.is_terminal()

    def test_is_terminal_skipped(self) -> None:
        """SKIPPED is a terminal state."""
        assert TestStatus.SKIPPED.is_terminal()


class TestTestResult:
    """Tests for TestResult dataclass."""

    def test_default_values(self) -> None:
        """TestResult has correct default values."""
        result = TestResult(name="TestFoo")
        assert result.name == "TestFoo"
        assert result.status == TestStatus.PENDING
        assert result.exit_code is None
        assert result.duration_s == 0.0
        assert result.error_msg == ""
        assert result.log_dir is None
        assert result.worker_id == -1

    def test_is_sequential_default(self) -> None:
        """Default worker_id=-1 indicates sequential mode."""
        result = TestResult(name="TestFoo")
        assert result.is_sequential()

    def test_is_sequential_parallel(self) -> None:
        """Positive worker_id indicates parallel mode."""
        result = TestResult(name="TestFoo", worker_id=0)
        assert not result.is_sequential()

        result = TestResult(name="TestFoo", worker_id=3)
        assert not result.is_sequential()

    def test_is_terminal_pending(self) -> None:
        """PENDING result is not terminal."""
        result = TestResult(name="TestFoo", status=TestStatus.PENDING)
        assert not result.is_terminal()

    def test_is_terminal_passed(self) -> None:
        """PASSED result is terminal."""
        result = TestResult(name="TestFoo", status=TestStatus.PASSED)
        assert result.is_terminal()

    def test_slots_attribute(self) -> None:
        """TestResult uses __slots__ for memory efficiency."""
        result = TestResult(name="TestFoo")
        assert hasattr(result, "__slots__") or not hasattr(result, "__dict__")


class TestWorkerInstance:
    """Tests for WorkerInstance dataclass."""

    def test_default_values(self) -> None:
        """WorkerInstance has correct default values."""
        worker = WorkerInstance(worker_id=0, nethermind_port=20551)
        assert worker.worker_id == 0
        assert worker.nethermind_port == 20551
        assert worker.nethermind_proc is None
        assert worker.data_dir is None
        assert worker.current_test == ""

    def test_is_running_no_process(self) -> None:
        """Worker with no process is not running."""
        worker = WorkerInstance(worker_id=0, nethermind_port=20551)
        assert not worker.is_running()

    def test_is_running_process_alive(self) -> None:
        """Worker with running process is running."""
        mock_proc = MagicMock()
        mock_proc.poll.return_value = None  # None means still running

        worker = WorkerInstance(
            worker_id=0,
            nethermind_port=20551,
            nethermind_proc=mock_proc,
        )
        assert worker.is_running()

    def test_is_running_process_terminated(self) -> None:
        """Worker with terminated process is not running."""
        mock_proc = MagicMock()
        mock_proc.poll.return_value = 0  # Exit code means terminated

        worker = WorkerInstance(
            worker_id=0,
            nethermind_port=20551,
            nethermind_proc=mock_proc,
        )
        assert not worker.is_running()

    def test_is_idle_not_running(self) -> None:
        """Worker not running is not idle."""
        worker = WorkerInstance(worker_id=0, nethermind_port=20551)
        assert not worker.is_idle()

    def test_is_idle_running_with_test(self) -> None:
        """Worker running a test is not idle."""
        mock_proc = MagicMock()
        mock_proc.poll.return_value = None

        worker = WorkerInstance(
            worker_id=0,
            nethermind_port=20551,
            nethermind_proc=mock_proc,
            current_test="TestFoo",
        )
        assert not worker.is_idle()

    def test_is_idle_running_no_test(self) -> None:
        """Worker running without a test is idle."""
        mock_proc = MagicMock()
        mock_proc.poll.return_value = None

        worker = WorkerInstance(
            worker_id=0,
            nethermind_port=20551,
            nethermind_proc=mock_proc,
            current_test="",
        )
        assert worker.is_idle()
