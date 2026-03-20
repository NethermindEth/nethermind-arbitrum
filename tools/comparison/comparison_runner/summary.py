"""GitHub Actions Job Summary generation.

Generates markdown summaries for test results that display in the
GitHub Actions Summary tab.
"""

from __future__ import annotations

import os
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .models import TestResult


def _format_duration(seconds: float) -> str:
    """Format duration as human-readable string."""
    if seconds < 60:
        return f"{seconds:.0f}s"
    mins = int(seconds // 60)
    secs = int(seconds % 60)
    return f"{mins}m {secs}s"


def generate_github_summary(
    results: list[TestResult],
    duration: float,
    workers: int = 1,
    nitro_branch: str = "system-tests",
) -> str:
    """Generate GitHub Actions Job Summary markdown.

    Args:
        results: List of test results
        duration: Total execution time in seconds
        workers: Number of workers used
        nitro_branch: Nitro branch tested against

    Returns:
        Markdown string for GITHUB_STEP_SUMMARY
    """
    from .models import TestStatus

    total = len(results)
    passed = sum(1 for r in results if r.status == TestStatus.PASSED)
    failed = sum(1 for r in results if r.status == TestStatus.FAILED)
    errors = sum(1 for r in results if r.status == TestStatus.ERROR)
    skipped = sum(1 for r in results if r.status == TestStatus.SKIPPED)
    flaky = sum(1 for r in results if r.was_retried)

    pass_rate = (passed / total * 100) if total > 0 else 0

    # Determine status
    if failed + errors == 0 and total > 0:
        status_emoji = "✅"
        status_text = "All Tests Passed"
    elif passed == 0 and total > 0:
        status_emoji = "❌"
        status_text = "All Tests Failed"
    elif total == 0:
        status_emoji = "⚠️"
        status_text = "No Tests Run"
    else:
        status_emoji = "⚠️"
        status_text = "Some Tests Failed"

    lines = []

    # Header
    lines.append(f"## {status_emoji} {status_text}\n")

    # Summary table
    lines.append("| Metric | Value |")
    lines.append("|--------|-------|")
    lines.append(f"| **Total Tests** | {total} |")
    lines.append(f"| **Passed** | {passed} ✓ ({pass_rate:.1f}%) |")
    lines.append(f"| **Failed** | {failed} ✗ |")
    if flaky > 0:
        lines.append(f"| **Flaky** | {flaky} ↻ |")
    if errors > 0:
        lines.append(f"| **Errors** | {errors} ⚡ |")
    if skipped > 0:
        lines.append(f"| **Skipped** | {skipped} ⊘ |")
    lines.append(f"| **Duration** | {_format_duration(duration)} |")
    lines.append(f"| **Workers** | {workers} |")
    lines.append(f"| **Nitro Branch** | `{nitro_branch}` |")
    lines.append("")

    # Failed tests details
    failed_tests = [r for r in results if r.status in (TestStatus.FAILED, TestStatus.ERROR)]

    if failed_tests:
        lines.append("<details>")
        lines.append(f"<summary>❌ Failed Tests ({len(failed_tests)})</summary>\n")
        lines.append("| Test | Worker | Attempts | Duration | Error |")
        lines.append("|------|--------|----------|----------|-------|")

        for r in failed_tests[:50]:  # Limit to 50
            name = r.name
            worker = f"W{r.worker_id}" if r.worker_id >= 0 else "-"
            attempts = f"{r.attempt}/{r.max_attempts}" if r.max_attempts > 1 else "-"
            dur_str = f"{r.duration_s:.1f}s" if r.duration_s else "-"

            # Clean up error message for table
            err = r.error_msg or ""
            err_short = err.split("\n")[0][:40]
            if len(err.split("\n")[0]) > 40:
                err_short += "..."
            err_short = err_short.replace("|", "\\|") or "-"

            lines.append(f"| `{name}` | {worker} | {attempts} | {dur_str} | {err_short} |")

        if len(failed_tests) > 50:
            lines.append(f"\n*... and {len(failed_tests) - 50} more*")

        lines.append("\n</details>\n")

    # Passed tests (collapsed)
    passed_tests = [r for r in results if r.status == TestStatus.PASSED]
    if passed_tests:
        lines.append("<details>")
        lines.append(f"<summary>✅ Passed Tests ({len(passed_tests)})</summary>\n")

        for r in passed_tests:
            dur_str = f" ({r.duration_s:.1f}s)" if r.duration_s else ""
            lines.append(f"- `{r.name}`{dur_str}")

        lines.append("\n</details>")

    return "\n".join(lines)


def write_github_summary(
    results: list[TestResult],
    duration: float,
    workers: int = 1,
    nitro_branch: str = "system-tests",
) -> None:
    """Write summary to GITHUB_STEP_SUMMARY if available.

    Args:
        results: List of test results
        duration: Total execution time in seconds
        workers: Number of workers used
        nitro_branch: Nitro branch tested against
    """
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return  # Not running in GitHub Actions

    markdown = generate_github_summary(results, duration, workers, nitro_branch)

    with open(summary_path, "a", encoding="utf-8") as f:
        f.write(markdown)
        f.write("\n")
