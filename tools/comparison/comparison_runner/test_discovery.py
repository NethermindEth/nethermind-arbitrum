"""Test discovery for Go system tests.

This module provides automatic discovery of Go test functions from the
Nitro system_tests directory.

Features:
- Regex-based parsing of Go test files
- Caching of discovered tests
- Filtering by pattern
"""

from __future__ import annotations

import re
from functools import lru_cache
from pathlib import Path
from typing import Final

GO_TEST_PATTERN: Final[re.Pattern[str]] = re.compile(
    r"^func\s+(Test[A-Za-z0-9_]+)\s*\(\s*\w+\s+\*testing\.T\s*\)",
    re.MULTILINE,
)
"""Regex pattern to match Go test function declarations."""

SYSTEM_TESTS_DIR: Final[str] = "system_tests"
"""Default directory name for system tests within Nitro."""


def discover_tests_from_file(file_path: Path) -> list[str]:
    """Extract test function names from a single Go file.

    Args:
        file_path: Path to a Go test file

    Returns:
        List of test function names found in the file
    """
    if not file_path.exists() or file_path.suffix != ".go":
        return []

    try:
        content = file_path.read_text(encoding="utf-8")
        matches = GO_TEST_PATTERN.findall(content)
        return list(matches)
    except (OSError, UnicodeDecodeError):
        return []


@lru_cache(maxsize=4)
def discover_all_tests(nitro_path: Path) -> tuple[str, ...]:
    """Discover all test functions in Nitro's system_tests directory.

    Results are cached to avoid repeated filesystem scans.

    Args:
        nitro_path: Path to the Nitro repository root

    Returns:
        Tuple of test function names (sorted alphabetically)

    Raises:
        FileNotFoundError: If system_tests directory doesn't exist
    """
    system_tests_dir = nitro_path / SYSTEM_TESTS_DIR

    if not system_tests_dir.exists():
        raise FileNotFoundError(f"System tests directory not found: {system_tests_dir}")

    all_tests: list[str] = []

    for go_file in system_tests_dir.glob("*_test.go"):
        tests = discover_tests_from_file(go_file)
        all_tests.extend(tests)

    # Return sorted tuple (hashable for caching)
    return tuple(sorted(set(all_tests)))


def filter_tests(
    tests: list[str] | tuple[str, ...],
    pattern: str | None = None,
) -> list[str]:
    """Filter tests by a regex pattern.

    Args:
        tests: List of test names
        pattern: Optional regex pattern to filter by (Go -run style)

    Returns:
        Filtered list of test names
    """
    if not pattern:
        return list(tests)

    try:
        regex = re.compile(pattern)
        return [t for t in tests if regex.search(t)]
    except re.error:
        # Invalid regex - try literal match
        return [t for t in tests if pattern in t]


def load_tests_from_file(file_path: Path) -> list[str]:
    """Load test names from a text file (one per line).

    Args:
        file_path: Path to the test list file

    Returns:
        List of test names

    Raises:
        FileNotFoundError: If the file doesn't exist
    """
    if not file_path.exists():
        raise FileNotFoundError(f"Test file not found: {file_path}")

    tests: list[str] = []
    for line in file_path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        # Skip empty lines and comments
        if line and not line.startswith("#"):
            tests.append(line)

    return tests


def get_tests(
    nitro_path: Path,
    test_filter: str | None = None,
    test_file: Path | None = None,
) -> list[str]:
    """Get tests to run based on filter, file, or discovery.

    Priority:
    1. If test_file is provided, load from file
    2. If test_filter is provided, discover and filter
    3. Otherwise, discover all tests

    Args:
        nitro_path: Path to Nitro repository
        test_filter: Optional Go -run style pattern
        test_file: Optional path to test list file

    Returns:
        List of test names to run
    """
    # Load from file if provided
    if test_file is not None:
        tests = load_tests_from_file(test_file)
        # Apply filter on top if both provided
        if test_filter:
            tests = filter_tests(tests, test_filter)
        return tests

    # Discover all tests
    all_tests = discover_all_tests(nitro_path)

    # Apply filter if provided
    if test_filter:
        return filter_tests(all_tests, test_filter)

    return list(all_tests)


def clear_cache() -> None:
    """Clear the test discovery cache."""
    discover_all_tests.cache_clear()
