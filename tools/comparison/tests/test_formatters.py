"""Tests for the formatters module."""

from comparison_runner.formatters import (
    BOX_WIDTH,
    SummaryStats,
    format_failed_tests,
    format_passed_tests_summary,
    format_summary_box,
)
from comparison_runner.models import TestResult, TestStatus


class TestSummaryStats:
    """Tests for SummaryStats dataclass."""

    def test_from_results_empty(self) -> None:
        """Empty results should give zero counts."""
        stats = SummaryStats.from_results([], 0.0)
        assert stats.total == 0
        assert stats.passed == 0
        assert stats.failed == 0
        assert stats.skipped == 0
        assert stats.errored == 0

    def test_from_results_counts_correctly(self) -> None:
        """Should count each status type correctly."""
        results = [
            TestResult(name="test1", status=TestStatus.PASSED),
            TestResult(name="test2", status=TestStatus.PASSED),
            TestResult(name="test3", status=TestStatus.FAILED, error_msg="fail"),
            TestResult(name="test4", status=TestStatus.SKIPPED),
            TestResult(name="test5", status=TestStatus.ERROR, error_msg="err"),
        ]
        stats = SummaryStats.from_results(results, 5.0)
        assert stats.total == 5
        assert stats.passed == 2
        assert stats.failed == 1
        assert stats.skipped == 1
        assert stats.errored == 1
        assert stats.duration == 5.0

    def test_pass_rate_zero_total(self) -> None:
        """Pass rate should be 0 when no tests ran (avoid divide-by-zero)."""
        stats = SummaryStats.from_results([], 0.0)
        assert stats.pass_rate == 0.0

    def test_pass_rate_calculation(self) -> None:
        """Pass rate should be calculated correctly."""
        results = [
            TestResult(name="test1", status=TestStatus.PASSED),
            TestResult(name="test2", status=TestStatus.PASSED),
            TestResult(name="test3", status=TestStatus.FAILED, error_msg="fail"),
            TestResult(name="test4", status=TestStatus.PASSED),
        ]
        stats = SummaryStats.from_results(results, 4.0)
        assert stats.pass_rate == 75.0

    def test_has_failures_true(self) -> None:
        """has_failures should be True when failures exist."""
        results = [
            TestResult(name="test1", status=TestStatus.FAILED, error_msg="fail"),
        ]
        stats = SummaryStats.from_results(results, 1.0)
        assert stats.has_failures is True

    def test_has_failures_false(self) -> None:
        """has_failures should be False when all pass."""
        results = [
            TestResult(name="test1", status=TestStatus.PASSED),
        ]
        stats = SummaryStats.from_results(results, 1.0)
        assert stats.has_failures is False


class TestFormatSummaryBox:
    """Tests for format_summary_box function."""

    def test_contains_header(self) -> None:
        """Output should contain the header text."""
        stats = SummaryStats(total=10, passed=8, failed=2, skipped=0, errored=0, duration=5.0)
        output = format_summary_box(stats)
        assert "COMPARISON TEST RESULTS" in output

    def test_contains_counts(self) -> None:
        """Output should contain test counts."""
        stats = SummaryStats(total=10, passed=8, failed=2, skipped=0, errored=0, duration=5.0)
        output = format_summary_box(stats)
        assert "10" in output  # total
        assert "8" in output  # passed
        assert "2" in output  # failed

    def test_all_passed_status(self) -> None:
        """Should show ALL TESTS PASSED when no failures."""
        stats = SummaryStats(total=10, passed=10, failed=0, skipped=0, errored=0, duration=5.0)
        output = format_summary_box(stats)
        assert "ALL TESTS PASSED" in output

    def test_some_failed_status(self) -> None:
        """Should show SOME TESTS FAILED when mixed results."""
        stats = SummaryStats(total=10, passed=8, failed=2, skipped=0, errored=0, duration=5.0)
        output = format_summary_box(stats)
        assert "SOME TESTS FAILED" in output

    def test_box_width_constant(self) -> None:
        """BOX_WIDTH constant should be 58."""
        assert BOX_WIDTH == 58


class TestFormatFailedTests:
    """Tests for format_failed_tests function."""

    def test_empty_results(self) -> None:
        """Empty results should return empty string."""
        output = format_failed_tests([])
        assert output == ""

    def test_no_failures(self) -> None:
        """No failures should return empty string."""
        results = [
            TestResult(name="test1", status=TestStatus.PASSED),
        ]
        output = format_failed_tests(results)
        assert output == ""

    def test_groups_by_error_type(self) -> None:
        """Should contain failure information."""
        results = [
            TestResult(name="test1", status=TestStatus.FAILED, error_msg="timeout"),
        ]
        output = format_failed_tests(results)
        assert "Failed Tests" in output
        assert "test1" in output


class TestFormatPassedTestsSummary:
    """Tests for format_passed_tests_summary function."""

    def test_no_failures_returns_empty(self) -> None:
        """When all pass, summary not shown."""
        results = [
            TestResult(name="test1", status=TestStatus.PASSED),
        ]
        output = format_passed_tests_summary(results)
        assert output == ""

    def test_with_failures_shows_passed_count(self) -> None:
        """When failures exist, show passed summary."""
        results = [
            TestResult(name="test1", status=TestStatus.PASSED),
            TestResult(name="test2", status=TestStatus.PASSED),
            TestResult(name="test3", status=TestStatus.FAILED, error_msg="fail"),
        ]
        output = format_passed_tests_summary(results)
        assert "Passed Tests" in output
        assert "2" in output
