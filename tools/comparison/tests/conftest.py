"""Shared test fixtures for comparison_runner test suite.

This module provides common fixtures used across multiple test modules,
reducing duplication and ensuring consistent test setup.

Fixtures provided:
- sample_tests: Standard test list for parameterized tests
- temp_log_dir: Temporary log directory with cleanup
- mock_popen: Mock Popen for process tests
- mock_requests: Mock requests for RPC tests
- sample_test_names: Longer list of test names for property testing
"""

from __future__ import annotations

import os
from pathlib import Path
from typing import TYPE_CHECKING
from unittest.mock import MagicMock, patch

import pytest

if TYPE_CHECKING:
    from collections.abc import Generator


@pytest.fixture
def sample_tests() -> list[str]:
    """Standard test list for parameterized tests.

    Returns:
        List of three common test names.
    """
    return ["TestTransfer", "TestDeploy", "TestEstimateGas"]


@pytest.fixture
def sample_test_names() -> list[str]:
    """Extended test list for property-based testing.

    Returns:
        List of typical Arbitrum Nitro system test names.
    """
    return [
        "TestTransfer",
        "TestDeploy",
        "TestEstimateGas",
        "TestContractCall",
        "TestL1Oracle",
        "TestPrecompiles",
        "TestBatchPoster",
        "TestSequencer",
        "TestValidation",
        "TestStateRoot",
    ]


@pytest.fixture
def temp_log_dir(tmp_path: Path) -> Path:
    """Temporary log directory with cleanup.

    Args:
        tmp_path: pytest's built-in temp directory fixture.

    Returns:
        Path to created log directory.
    """
    log_dir = tmp_path / "logs"
    log_dir.mkdir()
    return log_dir


@pytest.fixture
def temp_data_dir(tmp_path: Path) -> Path:
    """Temporary data directory with cleanup.

    Args:
        tmp_path: pytest's built-in temp directory fixture.

    Returns:
        Path to created data directory.
    """
    data_dir = tmp_path / "data"
    data_dir.mkdir()
    return data_dir


@pytest.fixture
def fake_nitro_path(tmp_path: Path) -> Path:
    """Fake Nitro repository path for testing.

    Creates a minimal directory structure that satisfies path validation.

    Args:
        tmp_path: pytest's built-in temp directory fixture.

    Returns:
        Path to fake Nitro directory.
    """
    nitro_path = tmp_path / "arbitrum-nitro"
    nitro_path.mkdir()
    (nitro_path / "system_tests").mkdir()
    return nitro_path


@pytest.fixture
def fake_nethermind_build(tmp_path: Path) -> Path:
    """Fake Nethermind build directory for testing.

    Args:
        tmp_path: pytest's built-in temp directory fixture.

    Returns:
        Path to fake Nethermind build directory.
    """
    build_dir = tmp_path / "nethermind-build"
    build_dir.mkdir()
    runner_exe = build_dir / "Nethermind.Runner"
    runner_exe.touch()
    return build_dir


@pytest.fixture
def mock_popen() -> Generator[MagicMock, None, None]:
    """Mock subprocess.Popen for process tests.

    Provides a mock process with standard behavior:
    - poll() returns None (process running)
    - pid is 12345
    - terminate/kill are MagicMock

    Yields:
        Configured MagicMock for Popen.
    """
    with patch("comparison_runner.process_manager.subprocess.Popen") as mock:
        proc = MagicMock()
        proc.poll.return_value = None
        proc.pid = 12345
        proc.returncode = None
        mock.return_value = proc
        yield proc


@pytest.fixture
def mock_requests_post() -> Generator[MagicMock, None, None]:
    """Mock requests.post for RPC tests.

    Provides a mock that returns successful JSON-RPC responses.

    Yields:
        Configured MagicMock for requests.post.
    """
    with patch("comparison_runner.rpc_client.requests.post") as mock:
        response = MagicMock()
        response.status_code = 200
        response.json.return_value = {"jsonrpc": "2.0", "result": True, "id": 1}
        mock.return_value = response
        yield mock


@pytest.fixture
def mock_subprocess_run() -> Generator[MagicMock, None, None]:
    """Mock subprocess.run for test executor.

    Provides a mock that returns successful test execution.

    Yields:
        Configured MagicMock for subprocess.run.
    """
    with patch("comparison_runner.test_executor.subprocess.run") as mock:
        result = MagicMock()
        result.returncode = 0
        result.stdout = "PASS"
        result.stderr = ""
        mock.return_value = result
        yield mock


@pytest.fixture
def clean_env() -> Generator[None, None, None]:
    """Ensure clean environment for tests that check env vars.

    Temporarily removes NITRO_PATH and NETHERMIND_PATH from environment.

    Yields:
        None (environment is cleaned).
    """
    env_vars = ["NITRO_PATH", "NETHERMIND_PATH"]
    saved = {k: os.environ.pop(k, None) for k in env_vars}

    yield

    for k, v in saved.items():
        if v is not None:
            os.environ[k] = v


@pytest.fixture
def env_with_nitro(fake_nitro_path: Path) -> Generator[None, None, None]:
    """Set NITRO_PATH environment variable for tests.

    Args:
        fake_nitro_path: Fixture providing fake Nitro path.

    Yields:
        None (NITRO_PATH is set).
    """
    saved = os.environ.get("NITRO_PATH")
    os.environ["NITRO_PATH"] = str(fake_nitro_path)

    yield

    if saved is not None:
        os.environ["NITRO_PATH"] = saved
    else:
        os.environ.pop("NITRO_PATH", None)


@pytest.fixture
def valid_port() -> int:
    """Valid unprivileged port for testing.

    Returns:
        Port number in valid range.
    """
    return 20551


@pytest.fixture
def valid_base_port() -> int:
    """Valid base port for multi-worker tests.

    Returns:
        Base port allowing 8 workers without overflow.
    """
    return 30000


def pytest_configure(config: pytest.Config) -> None:
    """Register custom markers."""
    config.addinivalue_line(
        "markers",
        "slow: marks tests as slow (deselect with '-m \"not slow\"')",
    )
    config.addinivalue_line(
        "markers",
        "integration: marks tests requiring external processes",
    )


def pytest_collection_modifyitems(config: pytest.Config, items: list[pytest.Item]) -> None:
    """Skip slow/integration tests unless explicitly requested."""
    if config.getoption("-m"):
        return

    skip_slow = pytest.mark.skip(reason="slow test, use -m slow to run")
    skip_integration = pytest.mark.skip(reason="integration test, use -m integration to run")

    for item in items:
        if "slow" in item.keywords:
            item.add_marker(skip_slow)
        if "integration" in item.keywords:
            item.add_marker(skip_integration)
