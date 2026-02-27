"""Property-based tests using Hypothesis.

This module provides property-based tests that verify invariants
across a wide range of inputs, catching edge cases that example-based
tests might miss.

Properties tested:
- Port allocation uniqueness and validity
- Work queue never loses tests
- Config validation rejects invalid inputs
- TestResult invariants
"""

from __future__ import annotations

import threading

import pytest
from hypothesis import assume, given, settings
from hypothesis import strategies as st
from pydantic import ValidationError

from comparison_runner.config import (
    MAX_PORT,
    MAX_WORKERS,
    MIN_PORT,
    ComparisonConfig,
)
from comparison_runner.models import TestResult, TestStatus
from comparison_runner.process_manager import allocate_port
from comparison_runner.worker_pool import WorkQueue


class TestPortAllocationProperties:
    """Property-based tests for port allocation."""

    @given(
        worker_id=st.integers(min_value=0, max_value=100),
        base_port=st.integers(min_value=MIN_PORT, max_value=60000),
    )
    def test_ports_unique_for_different_workers(self, worker_id: int, base_port: int) -> None:
        """Port allocation produces unique ports for different workers.

        Given: Two different worker IDs with same base port
        When: Allocating ports for both
        Then: Ports are different
        """
        # Sequential: worker N uses base_port + N
        assume(base_port + worker_id + 1 <= MAX_PORT)

        port1 = allocate_port(worker_id, base_port)
        port2 = allocate_port(worker_id + 1, base_port)

        assert port1 != port2

    @given(
        worker_id=st.integers(min_value=0, max_value=MAX_WORKERS),
        base_port=st.integers(min_value=MIN_PORT, max_value=60000),
    )
    def test_port_in_valid_range(self, worker_id: int, base_port: int) -> None:
        """Allocated ports are always in valid unprivileged range.

        Given: Any valid worker ID and base port
        When: Allocating a port
        Then: Port is in valid range (1024-65535)
        """
        # Sequential: worker N uses base_port + N
        assume(base_port + worker_id <= MAX_PORT)

        port = allocate_port(worker_id, base_port)

        assert MIN_PORT <= port <= MAX_PORT

    @given(
        base_port=st.integers(min_value=MIN_PORT, max_value=50000),
        num_workers=st.integers(min_value=2, max_value=MAX_WORKERS),
    )
    def test_no_port_collisions_in_pool(self, base_port: int, num_workers: int) -> None:
        """No port collisions when allocating for multiple workers.

        Given: A base port and number of workers
        When: Allocating ports for all workers
        Then: All ports are unique
        """
        # Sequential: last worker (N-1) uses base_port + (N-1)
        assume(base_port + num_workers - 1 <= MAX_PORT)

        ports = [allocate_port(i, base_port) for i in range(num_workers)]

        assert len(ports) == len(set(ports)), "Duplicate ports detected"

    @given(
        worker_id=st.integers(min_value=0, max_value=100),
        base_port=st.integers(min_value=MIN_PORT, max_value=60000),
    )
    def test_port_allocation_deterministic(self, worker_id: int, base_port: int) -> None:
        """Same inputs always produce same port.

        Given: Same worker ID and base port
        When: Calling allocate_port multiple times
        Then: Same port is returned
        """
        # Sequential: worker N uses base_port + N
        assume(base_port + worker_id <= MAX_PORT)

        port1 = allocate_port(worker_id, base_port)
        port2 = allocate_port(worker_id, base_port)

        assert port1 == port2


class TestWorkQueueProperties:
    """Property-based tests for WorkQueue."""

    @given(tests=st.lists(st.text(min_size=1, max_size=50), min_size=0, max_size=100))
    def test_queue_preserves_all_tests(self, tests: list[str]) -> None:
        """Work queue never loses tests.

        Given: A list of tests
        When: Adding all to queue and draining
        Then: All tests are retrieved (in some order)
        """
        queue = WorkQueue(tests)
        retrieved: list[str] = []

        while (test := queue.get()) is not None:
            retrieved.append(test)
            queue.mark_done()

        assert sorted(retrieved) == sorted(tests)

    @given(tests=st.lists(st.text(min_size=1, max_size=50), min_size=0, max_size=100))
    def test_total_unchanged_after_drain(self, tests: list[str]) -> None:
        """Total count remains constant after draining queue.

        Given: A queue with N tests
        When: Draining all tests
        Then: total() still returns N
        """
        queue = WorkQueue(tests)

        # Drain queue
        while queue.get() is not None:
            queue.mark_done()

        assert queue.total() == len(tests)

    @given(tests=st.lists(st.text(min_size=1, max_size=50), min_size=1, max_size=100))
    def test_remaining_decreases_monotonically(self, tests: list[str]) -> None:
        """Remaining count decreases with each get().

        Given: A non-empty queue
        When: Getting items one by one
        Then: remaining() decreases monotonically
        """
        queue = WorkQueue(tests)
        previous_remaining = queue.remaining()

        while queue.get() is not None:
            current_remaining = queue.remaining()
            assert current_remaining <= previous_remaining
            previous_remaining = current_remaining
            queue.mark_done()

    @given(tests=st.lists(st.text(min_size=1, max_size=50), min_size=0, max_size=50))
    def test_empty_after_full_drain(self, tests: list[str]) -> None:
        """Queue is empty after draining all items.

        Given: Any queue
        When: Draining all items
        Then: is_empty() returns True
        """
        queue = WorkQueue(tests)

        while queue.get() is not None:
            queue.mark_done()

        assert queue.is_empty()

    @given(
        tests=st.lists(st.text(min_size=1, max_size=20), min_size=10, max_size=50),
        num_workers=st.integers(min_value=2, max_value=8),
    )
    @settings(max_examples=20)
    def test_concurrent_access_preserves_tests(self, tests: list[str], num_workers: int) -> None:
        """Concurrent workers drain queue without losing tests.

        Given: A queue and multiple worker threads
        When: All workers drain concurrently
        Then: All tests are processed exactly once
        """
        queue = WorkQueue(tests)
        results: list[str] = []
        results_lock = threading.Lock()

        def worker() -> None:
            while True:
                test = queue.get()
                if test is None:
                    break
                with results_lock:
                    results.append(test)
                queue.mark_done()

        threads = [threading.Thread(target=worker) for _ in range(num_workers)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        assert sorted(results) == sorted(tests)


class TestConfigValidationProperties:
    """Property-based tests for configuration validation."""

    @given(port=st.integers(min_value=MIN_PORT, max_value=MAX_PORT - MAX_WORKERS))
    def test_valid_ports_accepted(self, port: int) -> None:
        """Valid ports in range are accepted by config.

        Given: A port in valid range
        When: Creating config with that port
        Then: Config is created successfully
        """
        config = ComparisonConfig(base_port=port)
        assert config.base_port == port

    @given(port=st.integers(min_value=1, max_value=MIN_PORT - 1))
    def test_privileged_ports_rejected(self, port: int) -> None:
        """Privileged ports (1-1023) are rejected. Port 0 is valid (ephemeral).

        Given: A port in range 1-1023
        When: Creating config with that port
        Then: ValidationError is raised
        """
        with pytest.raises(ValidationError):
            ComparisonConfig(base_port=port)

    @given(port=st.integers(min_value=MAX_PORT + 1, max_value=100000))
    def test_high_ports_rejected(self, port: int) -> None:
        """Ports above 65535 are rejected.

        Given: A port above 65535
        When: Creating config with that port
        Then: ValidationError is raised
        """
        with pytest.raises(ValidationError):
            ComparisonConfig(base_port=port)

    @given(workers=st.integers(min_value=1, max_value=MAX_WORKERS))
    def test_valid_worker_counts_accepted(self, workers: int) -> None:
        """Valid worker counts are accepted.

        Given: Worker count in valid range (1-MAX_WORKERS)
        When: Creating config with that count
        Then: Config is created successfully
        """
        config = ComparisonConfig(max_workers=workers)
        assert config.max_workers == workers


class TestResultProperties:
    """Property-based tests for TestResult model."""

    @given(
        name=st.text(min_size=1, max_size=100),
        duration=st.floats(min_value=0, max_value=3600, allow_nan=False),
        passed=st.booleans(),
    )
    def test_result_preserves_data(self, name: str, duration: float, passed: bool) -> None:
        """TestResult preserves all input data.

        Given: Test name, duration, and pass status
        When: Creating TestResult
        Then: All data is preserved
        """
        status = TestStatus.PASSED if passed else TestStatus.FAILED
        result = TestResult(
            name=name,
            status=status,
            duration_s=duration,
        )

        assert result.name == name
        assert result.duration_s == duration
        assert result.status == status

    @given(
        name=st.text(min_size=1, max_size=50),
        worker_id=st.integers(min_value=-1, max_value=100),
    )
    def test_worker_id_preserved(self, name: str, worker_id: int) -> None:
        """Worker ID is correctly stored.

        Given: A test name and worker ID
        When: Creating TestResult with worker_id
        Then: Worker ID is preserved
        """
        result = TestResult(
            name=name,
            status=TestStatus.PASSED,
            duration_s=0.0,
            worker_id=worker_id,
        )

        assert result.worker_id == worker_id

    @given(statuses=st.lists(st.sampled_from(list(TestStatus)), min_size=1, max_size=10))
    def test_status_icons_unique(self, statuses: list[TestStatus]) -> None:
        """Each status has a unique icon.

        Given: Multiple TestStatus values
        When: Getting icons for each
        Then: Different statuses have different icons
        """
        unique_statuses = list(set(statuses))
        icons = [s.icon for s in unique_statuses]

        # Same status should have same icon
        for status in unique_statuses:
            assert status.icon == status.icon

        # Different statuses should have different icons
        if len(unique_statuses) > 1:
            assert len(set(icons)) == len(unique_statuses)
