"""Tests for the process manager module."""

from __future__ import annotations

from pathlib import Path
from unittest.mock import MagicMock, patch

from comparison_runner.process_manager import (
    DEFAULT_CONFIG_NAME,
    DEFAULT_GRACE_PERIOD_S,
    NethermindProcess,
    allocate_port,
    is_process_healthy,
    stop_nethermind,
    wait_for_ready,
)


class TestAllocatePort:
    """Tests for allocate_port function."""

    def test_base_port_for_worker_zero(self) -> None:
        """Worker 0 should get base port."""
        port = allocate_port(0, base_port=20551)
        assert port == 20551

    def test_sequential_ports(self) -> None:
        """Workers should get sequential ports (base + worker_id)."""
        assert allocate_port(0, 20551) == 20551
        assert allocate_port(1, 20551) == 20552  # sequential
        assert allocate_port(2, 20551) == 20553  # sequential

    def test_custom_base_port(self) -> None:
        """Should use custom base port."""
        port = allocate_port(0, base_port=30000)
        assert port == 30000


class TestIsProcessHealthy:
    """Tests for is_process_healthy function."""

    def test_none_process_is_not_healthy(self) -> None:
        """None process should not be healthy."""
        assert is_process_healthy(None) is False

    def test_running_process_is_healthy(self) -> None:
        """Running process should be healthy."""
        mock_proc = MagicMock()
        mock_proc.poll.return_value = None  # Still running
        assert is_process_healthy(mock_proc) is True

    def test_terminated_process_is_not_healthy(self) -> None:
        """Terminated process should not be healthy."""
        mock_proc = MagicMock()
        mock_proc.poll.return_value = 0  # Exited with code 0
        assert is_process_healthy(mock_proc) is False


class TestStopNethermind:
    """Tests for stop_nethermind function."""

    def test_already_dead_process(self) -> None:
        """Already dead process should return True."""
        mock_proc = MagicMock()
        mock_proc.poll.return_value = 0  # Already dead

        result = stop_nethermind(mock_proc)
        assert result is True

    def test_graceful_shutdown(self) -> None:
        """Should return True on graceful SIGTERM shutdown."""
        mock_proc = MagicMock()
        mock_proc.pid = 12345
        # First poll: running, then dead
        mock_proc.poll.side_effect = [None, 0]

        with patch("os.killpg") as mock_killpg:
            result = stop_nethermind(mock_proc, grace_s=1)

            mock_killpg.assert_called_once()
            assert result is True


class TestWaitForReady:
    """Tests for wait_for_ready function."""

    def test_immediate_success(self) -> None:
        """Should return True when connection succeeds immediately."""
        with patch("socket.create_connection") as mock_conn:
            mock_ctx = MagicMock()
            mock_conn.return_value.__enter__ = MagicMock(return_value=mock_ctx)
            mock_conn.return_value.__exit__ = MagicMock(return_value=False)

            result = wait_for_ready("127.0.0.1", 20551, timeout_s=5)
            assert result is True

    def test_timeout_returns_false(self) -> None:
        """Should return False on timeout."""
        with patch("socket.create_connection") as mock_conn:
            mock_conn.side_effect = OSError("Connection refused")

            result = wait_for_ready("127.0.0.1", 20551, timeout_s=1)
            assert result is False

    def test_process_death_returns_false(self) -> None:
        """Should return False if process dies during wait."""
        mock_proc = MagicMock()
        mock_proc.poll.return_value = 1  # Dead with error

        with patch("socket.create_connection") as mock_conn:
            mock_conn.side_effect = OSError("Connection refused")

            result = wait_for_ready("127.0.0.1", 20551, proc=mock_proc, timeout_s=5)
            assert result is False


class TestNethermindProcess:
    """Tests for NethermindProcess class."""

    def test_init_sets_fields(self) -> None:
        """Should initialize with correct fields."""
        nm = NethermindProcess(
            port=20551,
            data_dir=Path("/tmp/test"),
            host="localhost",
        )
        assert nm.port == 20551
        assert nm.data_dir == Path("/tmp/test")
        assert nm.host == "localhost"
        assert nm.proc is None
        assert nm.is_running is False

    def test_is_running_when_no_process(self) -> None:
        """Should return False when no process."""
        nm = NethermindProcess(port=20551, data_dir=Path("/tmp/test"))
        assert nm.is_running is False

    def test_is_healthy_when_no_process(self) -> None:
        """Should return False when no process."""
        nm = NethermindProcess(port=20551, data_dir=Path("/tmp/test"))
        assert nm.is_healthy is False

    def test_stop_with_no_process(self) -> None:
        """Should handle stop when no process running."""
        nm = NethermindProcess(port=20551, data_dir=Path("/tmp/test"))
        result = nm.stop()
        assert result is True

    def test_start_creates_data_dir(self, tmp_path: Path) -> None:
        """Should create data directory on start."""
        data_dir = tmp_path / "data"
        nm = NethermindProcess(port=20551, data_dir=data_dir)

        with patch("comparison_runner.process_manager.start_nethermind") as mock_start:
            mock_proc = MagicMock()
            mock_start.return_value = mock_proc

            build_dir = tmp_path / "build"
            build_dir.mkdir()

            nm.start(build_dir)

            assert data_dir.exists()

    def test_start_already_running(self, tmp_path: Path) -> None:
        """Should not start if already running."""
        nm = NethermindProcess(port=20551, data_dir=tmp_path)
        mock_proc = MagicMock()
        mock_proc.poll.return_value = None  # Running
        nm.proc = mock_proc

        with patch("comparison_runner.process_manager.start_nethermind") as mock_start:
            nm.start(tmp_path)
            mock_start.assert_not_called()


class TestConstants:
    """Tests for module constants."""

    def test_default_grace_period_is_positive(self) -> None:
        """Grace period should be positive."""
        assert DEFAULT_GRACE_PERIOD_S > 0

    def test_default_config_name(self) -> None:
        """Default config name should be set."""
        assert DEFAULT_CONFIG_NAME == "arbitrum-system-test"
