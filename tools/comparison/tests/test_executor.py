"""Tests for the test executor module."""

from __future__ import annotations

import os
from pathlib import Path
from threading import Event
from unittest.mock import MagicMock, patch

import pytest

from comparison_runner.exceptions import TestExecutionError
from comparison_runner.models import TestStatus
from comparison_runner.test_executor import (
    DEFAULT_TEST_TIMEOUT,
    GO_TEST_PARALLEL,
    GoTestResult,
    build_test_env,
    execute_test,
    run_go_test,
)


class TestGoTestResult:
    """Tests for GoTestResult dataclass."""

    def test_default_values(self) -> None:
        """Should have correct defaults."""
        result = GoTestResult(exit_code=0)
        assert result.exit_code == 0
        assert result.error_msg == ""
        assert result.interrupted is False
        assert result.suppressed_warnings == 0

    def test_with_all_fields(self) -> None:
        """Should accept all fields."""
        result = GoTestResult(
            exit_code=1,
            error_msg="test failed",
            interrupted=True,
            suppressed_warnings=5,
        )
        assert result.exit_code == 1
        assert result.error_msg == "test failed"
        assert result.interrupted is True
        assert result.suppressed_warnings == 5


class TestBuildTestEnv:
    """Tests for build_test_env function."""

    def test_sets_nitro_path(self, tmp_path: Path) -> None:
        """Should set NITRO_PATH."""
        env = build_test_env(
            nitro_path=tmp_path,
            nethermind_host="127.0.0.1",
            nethermind_port=20551,
        )
        assert env["NITRO_PATH"] == str(tmp_path)

    def test_sets_secondary_el_url(self, tmp_path: Path) -> None:
        """Should set NITRO_SECONDARY_EL_URL."""
        env = build_test_env(
            nitro_path=tmp_path,
            nethermind_host="localhost",
            nethermind_port=30000,
        )
        assert env["NITRO_SECONDARY_EL_URL"] == "http://localhost:30000"

    def test_preserves_base_env(self, tmp_path: Path) -> None:
        """Should preserve base environment variables."""
        base_env = {"EXISTING_VAR": "value", "PATH": "/usr/bin"}
        env = build_test_env(
            nitro_path=tmp_path,
            nethermind_host="127.0.0.1",
            nethermind_port=20551,
            base_env=base_env,
        )
        assert env["EXISTING_VAR"] == "value"
        assert env["PATH"] == "/usr/bin"

    def test_uses_os_environ_when_no_base(self, tmp_path: Path) -> None:
        """Should use os.environ when base_env is None."""
        with patch.dict(os.environ, {"TEST_VAR": "test_value"}):
            env = build_test_env(
                nitro_path=tmp_path,
                nethermind_host="127.0.0.1",
                nethermind_port=20551,
            )
            assert "TEST_VAR" in env


def _create_mock_stdout(lines: list[str]) -> MagicMock:
    """Create a mock stdout that works with readline iteration."""
    mock_stdout = MagicMock()
    line_iter = iter(lines + [""])  # Add empty string to signal end
    mock_stdout.readline = lambda: next(line_iter)
    mock_stdout.close = MagicMock()
    return mock_stdout


class TestRunGoTest:
    """Tests for run_go_test function."""

    def test_nitro_path_not_found(self, tmp_path: Path) -> None:
        """Should raise TestExecutionError when nitro_path doesn't exist."""
        nonexistent = tmp_path / "nonexistent"

        with pytest.raises(TestExecutionError) as exc_info:
            run_go_test(
                test_name="TestTransfer",
                nitro_path=nonexistent,
                env={},
            )
        assert "NITRO_PATH not found" in str(exc_info.value)
        assert exc_info.value.test_name == "TestTransfer"

    def test_successful_test(self, tmp_path: Path) -> None:
        """Should return exit_code 0 on success."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout(
            [
                "=== RUN TestTransfer\n",
                "--- PASS: TestTransfer\n",
            ]
        )
        mock_proc.wait.return_value = 0

        with patch("subprocess.Popen", return_value=mock_proc):
            result = run_go_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                env={"NITRO_SECONDARY_EL_URL": "http://localhost:20551"},
            )

        assert result.exit_code == 0
        assert result.interrupted is False

    def test_failed_test(self, tmp_path: Path) -> None:
        """Should return non-zero exit_code on failure."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout(
            [
                "=== RUN TestTransfer\n",
                "--- FAIL: TestTransfer\n",
            ]
        )
        mock_proc.wait.return_value = 1

        with patch("subprocess.Popen", return_value=mock_proc):
            result = run_go_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                env={},
            )

        assert result.exit_code == 1

    def test_interrupt_handling(self, tmp_path: Path) -> None:
        """Should handle interrupt event."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()

        interrupted = Event()
        interrupted.set()  # Already interrupted

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout(["line1\n", "line2\n"])
        mock_proc.wait.return_value = -1

        with patch("subprocess.Popen", return_value=mock_proc):
            result = run_go_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                env={},
                interrupted=interrupted,
            )

        assert result.interrupted is True
        assert result.exit_code == -1
        mock_proc.terminate.assert_called_once()

    def test_ld_warning_suppression(self, tmp_path: Path) -> None:
        """Should suppress ld warnings and count them."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout(
            [
                "normal line\n",
                "ld: warning: duplicate symbol\n",
                "ld: warning: another warning\n",
                "another normal line\n",
            ]
        )
        mock_proc.wait.return_value = 0

        with patch("subprocess.Popen", return_value=mock_proc):
            result = run_go_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                env={},
            )

        assert result.suppressed_warnings == 2

    def test_writes_to_log_file(self, tmp_path: Path) -> None:
        """Should write output to log file."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()
        log_path = tmp_path / "test.log"

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout(["test output\n"])
        mock_proc.wait.return_value = 0

        with patch("subprocess.Popen", return_value=mock_proc):
            run_go_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                env={},
                log_path=log_path,
            )

        assert log_path.exists()
        content = log_path.read_text()
        assert "test output" in content


class TestExecuteTest:
    """Tests for execute_test convenience function."""

    def test_returns_test_result(self, tmp_path: Path) -> None:
        """Should return TestResult with correct fields."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout(["--- PASS: TestTransfer\n"])
        mock_proc.wait.return_value = 0

        with patch("subprocess.Popen", return_value=mock_proc):
            result = execute_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                nethermind_host="127.0.0.1",
                nethermind_port=20551,
            )

        assert result.name == "TestTransfer"
        assert result.status == TestStatus.PASSED
        assert result.exit_code == 0
        assert result.worker_id == -1  # Sequential mode

    def test_failed_test_result(self, tmp_path: Path) -> None:
        """Should return FAILED status on non-zero exit."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout(["--- FAIL: TestTransfer\n"])
        mock_proc.wait.return_value = 1

        with patch("subprocess.Popen", return_value=mock_proc):
            result = execute_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                nethermind_host="127.0.0.1",
                nethermind_port=20551,
            )

        assert result.status == TestStatus.FAILED
        assert "exit code 1" in result.error_msg

    def test_with_worker_id(self, tmp_path: Path) -> None:
        """Should set worker_id in result."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout([])
        mock_proc.wait.return_value = 0

        with patch("subprocess.Popen", return_value=mock_proc):
            result = execute_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                nethermind_host="127.0.0.1",
                nethermind_port=20551,
                worker_id=3,
            )

        assert result.worker_id == 3

    def test_creates_log_dir(self, tmp_path: Path) -> None:
        """Should create test-specific log directory."""
        nitro_path = tmp_path / "nitro"
        nitro_path.mkdir()
        log_dir = tmp_path / "logs"

        mock_proc = MagicMock()
        mock_proc.stdout = _create_mock_stdout([])
        mock_proc.wait.return_value = 0

        with patch("subprocess.Popen", return_value=mock_proc):
            result = execute_test(
                test_name="TestTransfer",
                nitro_path=nitro_path,
                nethermind_host="127.0.0.1",
                nethermind_port=20551,
                log_dir=log_dir,
            )

        assert result.log_dir == log_dir / "TestTransfer"
        assert (log_dir / "TestTransfer").exists()

    def test_handles_execution_error(self, tmp_path: Path) -> None:
        """Should return FAILED on TestExecutionError."""
        nonexistent = tmp_path / "nonexistent"

        result = execute_test(
            test_name="TestTransfer",
            nitro_path=nonexistent,
            nethermind_host="127.0.0.1",
            nethermind_port=20551,
        )

        assert result.status == TestStatus.FAILED
        assert "NITRO_PATH not found" in result.error_msg


class TestConstants:
    """Tests for module constants."""

    def test_default_timeout(self) -> None:
        """Default timeout should be set."""
        assert DEFAULT_TEST_TIMEOUT == "5m"

    def test_go_test_parallel(self) -> None:
        """Go test parallelism should be 1."""
        assert GO_TEST_PARALLEL == 1
