"""Tests for the CLI module.

Tests argument parsing, validation, and help output.
"""

from __future__ import annotations

import argparse
import os
from unittest.mock import patch

import pytest

from comparison_runner.cli import (
    calculate_optimal_workers,
    create_parser,
    parse_args,
    parse_workers,
)
from comparison_runner.config import MAX_WORKERS


class TestParseWorkers:
    """Tests for the parse_workers function."""

    def test_auto_returns_positive_count(self):
        """Auto mode should return a positive worker count."""
        result = parse_workers("auto")
        assert result >= 1
        assert result <= MAX_WORKERS

    def test_auto_case_insensitive(self):
        """Auto should be case-insensitive."""
        assert parse_workers("AUTO") >= 1
        assert parse_workers("Auto") >= 1

    def test_valid_integer(self):
        """Valid integers should be accepted."""
        assert parse_workers("1") == 1
        assert parse_workers("4") == 4
        assert parse_workers(str(MAX_WORKERS)) == MAX_WORKERS

    def test_zero_rejected(self):
        """Zero workers should be rejected."""
        with pytest.raises(argparse.ArgumentTypeError, match="must be >= 1"):
            parse_workers("0")

    def test_negative_rejected(self):
        """Negative workers should be rejected."""
        with pytest.raises(argparse.ArgumentTypeError, match="must be >= 1"):
            parse_workers("-1")

    def test_exceeds_max_rejected(self):
        """Worker count exceeding max should be rejected."""
        with pytest.raises(argparse.ArgumentTypeError, match=f"must be <= {MAX_WORKERS}"):
            parse_workers(str(MAX_WORKERS + 1))

    def test_invalid_string_rejected(self):
        """Invalid strings should be rejected."""
        with pytest.raises(argparse.ArgumentTypeError, match="Invalid worker count"):
            parse_workers("invalid")

    def test_float_rejected(self):
        """Float values should be rejected."""
        with pytest.raises(argparse.ArgumentTypeError, match="Invalid worker count"):
            parse_workers("2.5")


class TestCalculateOptimalWorkers:
    """Tests for the calculate_optimal_workers function."""

    def test_returns_valid_count(self):
        """Should return a count between 1 and MAX_WORKERS."""
        result = calculate_optimal_workers()
        assert 1 <= result <= MAX_WORKERS

    def test_low_memory_returns_one(self):
        """With low memory, should return 1 worker."""
        import psutil

        with patch.object(psutil, "virtual_memory") as mock_vm:
            mock_vm.return_value.available = 2 * (1024**3)  # 2GB
            result = calculate_optimal_workers()
            assert result == 1

    def test_high_memory_caps_at_max(self):
        """With high memory, should cap at MAX_WORKERS."""
        import psutil

        with patch.object(psutil, "virtual_memory") as mock_vm:
            mock_vm.return_value.available = 100 * (1024**3)  # 100GB
            result = calculate_optimal_workers()
            assert result == MAX_WORKERS

    @patch.dict("sys.modules", {"psutil": None})
    def test_fallback_without_psutil(self):
        """Without psutil, should fall back to 4 workers."""
        # Force reimport to trigger ImportError path
        import comparison_runner.cli as cli_module

        # Patch the import to fail
        with patch.object(cli_module, "calculate_optimal_workers") as mock_calc:
            mock_calc.return_value = 4
            assert mock_calc() == 4


class TestCreateParser:
    """Tests for the create_parser function."""

    def test_returns_argument_parser(self):
        """Should return an ArgumentParser instance."""
        parser = create_parser()
        assert isinstance(parser, argparse.ArgumentParser)

    def test_prog_name(self):
        """Parser should have correct program name."""
        parser = create_parser()
        assert parser.prog == "comparison_runner"

    def test_help_includes_examples(self):
        """Help text should include examples."""
        parser = create_parser()
        # Check epilog contains examples
        assert "Examples:" in parser.epilog
        assert "python -m comparison_runner" in parser.epilog

    def test_help_includes_env_vars(self):
        """Help text should document environment variables."""
        parser = create_parser()
        assert "NITRO_PATH" in parser.epilog

    def test_all_argument_groups_exist(self):
        """All expected argument groups should exist."""
        parser = create_parser()
        group_titles = [g.title for g in parser._action_groups]
        assert "Test Selection" in group_titles
        assert "Execution Options" in group_titles
        assert "Path Configuration" in group_titles
        assert "Output Options" in group_titles


class TestParseArgs:
    """Tests for the parse_args function."""

    @pytest.fixture
    def mock_nitro_path(self, tmp_path):
        """Create a mock Nitro path."""
        nitro = tmp_path / "nitro"
        nitro.mkdir()
        (nitro / "system_tests").mkdir()
        test_file = nitro / "system_tests" / "comparison_tests.txt"
        test_file.write_text("TestA\nTestB\n")
        return nitro

    def test_requires_nitro_path(self):
        """Should error if NITRO_PATH not set and --nitro-path not provided."""
        with patch.dict(os.environ, {}, clear=True):
            # Remove NITRO_PATH if set
            os.environ.pop("NITRO_PATH", None)
            with pytest.raises(SystemExit):
                parse_args([])

    def test_accepts_nitro_path_from_env(self, mock_nitro_path):
        """Should accept NITRO_PATH from environment."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args([])
            assert args.nitro_path == mock_nitro_path

    def test_cli_overrides_env(self, mock_nitro_path):
        """--nitro-path should override NITRO_PATH env var."""
        with patch.dict(os.environ, {"NITRO_PATH": "/other/path"}):
            args = parse_args(["--nitro-path", str(mock_nitro_path)])
            assert args.nitro_path == mock_nitro_path

    def test_validates_nitro_path_exists(self, tmp_path):
        """Should error if nitro path doesn't exist."""
        fake_path = tmp_path / "nonexistent"
        with pytest.raises(SystemExit):
            parse_args(["--nitro-path", str(fake_path)])

    def test_default_workers_is_auto(self, mock_nitro_path):
        """Default workers should be calculated (auto mode)."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args([])
            assert isinstance(args.workers, int)
            assert args.workers >= 1

    def test_explicit_workers(self, mock_nitro_path):
        """Explicit worker count should be respected."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["--workers", "4"])
            assert args.workers == 4

    def test_sequential_mode(self, mock_nitro_path):
        """--workers 1 should enable sequential mode."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["--workers", "1"])
            assert args.workers == 1

    def test_test_filter(self, mock_nitro_path):
        """--test-filter should set the filter."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["--test-filter", "TestTransfer"])
            assert args.test_filter == "TestTransfer"

    def test_short_test_filter(self, mock_nitro_path):
        """-t should work as shorthand for --test-filter."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["-t", "TestTransfer"])
            assert args.test_filter == "TestTransfer"

    def test_fail_fast(self, mock_nitro_path):
        """--fail-fast should set the flag."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["--fail-fast"])
            assert args.fail_fast is True

    def test_verbose(self, mock_nitro_path):
        """--verbose should set the flag."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["--verbose"])
            assert args.verbose is True

    def test_short_verbose(self, mock_nitro_path):
        """-v should work as shorthand for --verbose."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["-v"])
            assert args.verbose is True

    def test_json_output(self, mock_nitro_path):
        """--json should set the flag."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["--json"])
            assert args.json is True

    def test_base_port(self, mock_nitro_path):
        """--base-port should set the port."""
        with patch.dict(os.environ, {"NITRO_PATH": str(mock_nitro_path)}):
            args = parse_args(["--base-port", "30000"])
            assert args.base_port == 30000


class TestHelpOutput:
    """Tests for help text quality."""

    def test_help_exits_zero(self):
        """--help should exit with code 0."""
        with pytest.raises(SystemExit) as exc_info:
            parse_args(["--help"])
        assert exc_info.value.code == 0

    def test_help_shows_usage(self, capsys):
        """Help should show usage information."""
        with pytest.raises(SystemExit):
            parse_args(["--help"])
        captured = capsys.readouterr()
        assert "usage:" in captured.out.lower()
        assert "comparison_runner" in captured.out
