"""Result formatting utilities for comparison test runner.

Separates presentation logic from execution logic for easier testing.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .models import TestResult

from .models import TestStatus

BOX_WIDTH = 58


@dataclass(slots=True)
class SummaryStats:
    """Computed summary statistics from test results."""

    total: int
    passed: int
    failed: int
    skipped: int
    errored: int
    duration: float

    @classmethod
    def from_results(cls, results: list[TestResult], duration: float) -> SummaryStats:
        """Compute statistics from a list of test results."""
        return cls(
            total=len(results),
            passed=sum(1 for r in results if r.status == TestStatus.PASSED),
            failed=sum(1 for r in results if r.status == TestStatus.FAILED),
            skipped=sum(1 for r in results if r.status == TestStatus.SKIPPED),
            errored=sum(1 for r in results if r.status == TestStatus.ERROR),
            duration=duration,
        )

    @property
    def pass_rate(self) -> float:
        """Calculate pass rate as percentage."""
        return (self.passed / self.total * 100) if self.total > 0 else 0.0

    @property
    def has_failures(self) -> bool:
        """Check if any tests failed or errored."""
        return (self.failed + self.errored) > 0


def _format_duration(seconds: float) -> str:
    if seconds < 60:
        return f"{seconds:.1f}s"
    minutes = int(seconds // 60)
    secs = seconds % 60
    return f"{minutes}m {secs:.0f}s"


def format_summary_box(stats: SummaryStats) -> str:
    """Format summary statistics as ASCII box."""
    lines = []

    # Determine status
    if not stats.has_failures and stats.total > 0:
        status_icon = "\u2705"
        status_text = "ALL TESTS PASSED"
    elif stats.passed == 0:
        status_icon = "\u274c"
        status_text = "ALL TESTS FAILED"
    else:
        status_icon = "\u26a0\ufe0f"
        status_text = "SOME TESTS FAILED"

    # Build box
    lines.append("")
    lines.append("\u2554" + "\u2550" * BOX_WIDTH + "\u2557")
    lines.append("\u2551" + " COMPARISON TEST RESULTS ".center(BOX_WIDTH) + "\u2551")
    lines.append("\u2560" + "\u2550" * BOX_WIDTH + "\u2563")
    lines.append(f"\u2551  {status_icon} {status_text:<53} \u2551")
    lines.append("\u2560" + "\u2550" * BOX_WIDTH + "\u2563")
    lines.append(f"\u2551  {'Total Tests:':<20} {stats.total:>8}                       \u2551")
    pass_str = f"{stats.passed:>8}  \u2713  ({stats.pass_rate:.1f}%)"
    lines.append(f"\u2551  {'Passed:':<20} {pass_str:<32} \u2551")
    lines.append(f"\u2551  {'Failed:':<20} {stats.failed:>8}  \u2717                       \u2551")
    if stats.skipped > 0:
        lines.append(
            f"\u2551  {'Skipped:':<20} {stats.skipped:>8}  \u2298                       \u2551"
        )
    if stats.errored > 0:
        lines.append(
            f"\u2551  {'Errors:':<20} {stats.errored:>8}  \u26a1                      \u2551"
        )
    lines.append("\u2560" + "\u2550" * BOX_WIDTH + "\u2563")
    dur_str = _format_duration(stats.duration)
    lines.append(f"\u2551  {'Duration:':<20} {dur_str:>8}                       \u2551")
    lines.append("\u255a" + "\u2550" * BOX_WIDTH + "\u255d")

    return "\n".join(lines)


def _extract_error_type(error_msg: str | None) -> str:
    """Extract a concise error type from error message."""
    if not error_msg:
        return "unknown error"

    msg = error_msg.lower()

    if "timeout" in msg:
        return "timeout"
    if "connection refused" in msg or "connection reset" in msg:
        return "connection error"
    if "assertion" in msg or "expected" in msg:
        return "assertion failure"
    if "panic" in msg:
        return "panic"
    if "nil pointer" in msg or "null" in msg:
        return "nil/null error"

    # Try to extract first line if multi-line
    first_line = error_msg.split("\n")[0][:50]
    return first_line if first_line else "unknown error"


def format_failed_tests(results: list[TestResult]) -> str:
    """Format failed tests grouped by error type."""
    failed_tests = [r for r in results if r.status in (TestStatus.FAILED, TestStatus.ERROR)]
    if not failed_tests:
        return ""

    lines = []

    # Group by error type
    error_groups: dict[str, list[TestResult]] = {}
    for r in failed_tests:
        error_type = _extract_error_type(r.error_msg)
        if error_type not in error_groups:
            error_groups[error_type] = []
        error_groups[error_type].append(r)

    lines.append("")
    lines.append("\u250c" + "\u2500" * BOX_WIDTH + "\u2510")
    lines.append("\u2502" + f" Failed Tests ({len(failed_tests)}) ".center(BOX_WIDTH) + "\u2502")
    lines.append("\u2514" + "\u2500" * BOX_WIDTH + "\u2518")

    for error_type, tests in sorted(error_groups.items(), key=lambda x: -len(x[1])):
        lines.append(f"\n  [{error_type}] ({len(tests)} tests)")
        # Show up to 10 tests per group, then summarize
        for r in tests[:10]:
            name = r.name[:45] + "..." if len(r.name) > 48 else r.name
            lines.append(f"    \u2022 {name}")
        if len(tests) > 10:
            lines.append(f"    ... and {len(tests) - 10} more")

    return "\n".join(lines)


def format_passed_tests_summary(results: list[TestResult]) -> str:
    """Format passed tests summary (collapsed view)."""
    passed_tests = [r for r in results if r.status == TestStatus.PASSED]
    failed_tests = [r for r in results if r.status in (TestStatus.FAILED, TestStatus.ERROR)]

    if not passed_tests or not failed_tests:
        return ""

    lines = []
    lines.append("")
    lines.append("\u250c" + "\u2500" * BOX_WIDTH + "\u2510")
    lines.append("\u2502" + f" Passed Tests ({len(passed_tests)}) ".center(BOX_WIDTH) + "\u2502")
    lines.append("\u2514" + "\u2500" * BOX_WIDTH + "\u2518")
    lines.append(f"  \u2713 {len(passed_tests)} tests passed successfully")

    return "\n".join(lines)
