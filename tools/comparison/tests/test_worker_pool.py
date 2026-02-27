"""Tests for the worker pool module."""

from __future__ import annotations

import threading
from pathlib import Path
from unittest.mock import patch

import pytest

from comparison_runner.exceptions import ConfigurationError, WorkerPoolError
from comparison_runner.worker_pool import (
    InstancePool,
    PoolConfig,
    WorkQueue,
)


class TestWorkQueue:
    """Tests for WorkQueue class."""

    def test_init_with_tests(self) -> None:
        """Should initialize with test list."""
        wq = WorkQueue(["TestA", "TestB", "TestC"])
        assert wq.total() == 3
        assert wq.remaining() == 3

    def test_get_returns_tests_in_order(self) -> None:
        """Should return tests in FIFO order."""
        wq = WorkQueue(["TestA", "TestB", "TestC"])
        assert wq.get() == "TestA"
        assert wq.get() == "TestB"
        assert wq.get() == "TestC"

    def test_get_returns_none_when_empty(self) -> None:
        """Should return None when queue is empty."""
        wq = WorkQueue(["TestA"])
        wq.get()
        assert wq.get() is None

    def test_remaining_decreases(self) -> None:
        """Remaining should decrease as tests are taken."""
        wq = WorkQueue(["TestA", "TestB", "TestC"])
        assert wq.remaining() == 3
        wq.get()
        assert wq.remaining() == 2

    def test_total_stays_constant(self) -> None:
        """Total should stay constant."""
        wq = WorkQueue(["TestA", "TestB", "TestC"])
        wq.get()
        wq.get()
        assert wq.total() == 3

    def test_is_empty(self) -> None:
        """is_empty should reflect queue state."""
        wq = WorkQueue(["TestA"])
        assert wq.is_empty() is False
        wq.get()
        assert wq.is_empty() is True

    def test_thread_safety(self) -> None:
        """Should be thread-safe for concurrent access."""
        tests = [f"Test{i}" for i in range(100)]
        wq = WorkQueue(tests)
        results: list[str] = []
        lock = threading.Lock()

        def worker() -> None:
            while True:
                test = wq.get()
                if test is None:
                    break
                with lock:
                    results.append(test)
                wq.mark_done()

        threads = [threading.Thread(target=worker) for _ in range(4)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        # All tests should be consumed exactly once
        assert len(results) == 100
        assert set(results) == set(tests)

    def test_empty_queue(self) -> None:
        """Should handle empty test list."""
        wq = WorkQueue([])
        assert wq.total() == 0
        assert wq.remaining() == 0
        assert wq.get() is None


class TestPoolConfig:
    """Tests for PoolConfig class."""

    def test_valid_config(self) -> None:
        """Should accept valid configuration."""
        config = PoolConfig(max_workers=4, base_port=20551)
        assert config.max_workers == 4
        assert config.base_port == 20551

    def test_zero_workers_raises(self) -> None:
        """Should reject zero workers."""
        with pytest.raises(ConfigurationError) as exc_info:
            PoolConfig(max_workers=0)
        assert "max_workers must be >= 1" in str(exc_info.value)

    def test_negative_workers_raises(self) -> None:
        """Should reject negative workers."""
        with pytest.raises(ConfigurationError) as exc_info:
            PoolConfig(max_workers=-1)
        assert "max_workers must be >= 1" in str(exc_info.value)

    def test_privileged_port_raises(self) -> None:
        """Should reject privileged ports."""
        with pytest.raises(ConfigurationError) as exc_info:
            PoolConfig(max_workers=4, base_port=80)
        assert "privileged" in str(exc_info.value)

    def test_port_overflow_raises(self) -> None:
        """Should reject config where ports would overflow."""
        with pytest.raises(ConfigurationError) as exc_info:
            PoolConfig(max_workers=100, base_port=65500)
        assert "exceed" in str(exc_info.value)


class TestInstancePool:
    """Tests for InstancePool class."""

    def test_init(self, tmp_path: Path) -> None:
        """Should initialize with config."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, log_dir=tmp_path)
        assert len(pool) == 0

    def test_get_or_create_creates_instance(self, tmp_path: Path) -> None:
        """Should create instance on first access."""
        config = PoolConfig(max_workers=4, base_port=20551)
        pool = InstancePool(config, root_dir=tmp_path)

        instance = pool.get_or_create(0)
        assert instance.worker_id == 0
        assert instance.nethermind_port == 20551
        assert len(pool) == 1

    def test_get_or_create_returns_same_instance(self, tmp_path: Path) -> None:
        """Should return same instance on subsequent calls."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path)

        instance1 = pool.get_or_create(0)
        instance2 = pool.get_or_create(0)
        assert instance1 is instance2

    def test_get_or_create_invalid_worker_id(self, tmp_path: Path) -> None:
        """Should raise for invalid worker_id."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path)

        with pytest.raises(ValueError):
            pool.get_or_create(-1)

        with pytest.raises(ValueError):
            pool.get_or_create(4)  # max_workers is 4, so valid range is 0-3

    def test_get_port(self, tmp_path: Path) -> None:
        """Should return stored port for initialized worker."""
        config = PoolConfig(max_workers=4, base_port=20551)
        pool = InstancePool(config, root_dir=tmp_path)

        # Must initialize workers first
        pool.get_or_create(0)
        pool.get_or_create(1)
        pool.get_or_create(2)

        # Ports are sequential: worker N gets base_port + N
        assert pool.get_port(0) == 20551  # base_port + 0
        assert pool.get_port(1) == 20552  # base_port + 1
        assert pool.get_port(2) == 20553  # base_port + 2

    def test_get_port_uninitialized_raises(self, tmp_path: Path) -> None:
        """Should raise RuntimeError for uninitialized worker."""
        config = PoolConfig(max_workers=4, base_port=20551)
        pool = InstancePool(config, root_dir=tmp_path)

        with pytest.raises(RuntimeError, match="not initialized"):
            pool.get_port(0)

    def test_context_manager(self, tmp_path: Path) -> None:
        """Should call shutdown_all on exit."""
        config = PoolConfig(max_workers=4)

        with patch.object(InstancePool, "shutdown_all") as mock_shutdown:
            with InstancePool(config, root_dir=tmp_path) as pool:
                pool.get_or_create(0)

            mock_shutdown.assert_called_once()

    def test_context_manager_returns_self(self, tmp_path: Path) -> None:
        """Context manager should return self."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path)

        with pool as p:
            assert p is pool

    def test_start_worker_without_build_dir(self, tmp_path: Path) -> None:
        """Should raise when build_dir not configured."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path, build_dir=None)

        with pytest.raises(WorkerPoolError) as exc_info:
            pool.start_worker(0)
        assert "build_dir not configured" in str(exc_info.value)

    def test_active_count_initially_zero(self, tmp_path: Path) -> None:
        """Active count should be zero initially."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path)
        assert pool.active_count() == 0

    def test_healthy_count_initially_zero(self, tmp_path: Path) -> None:
        """Healthy count should be zero initially."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path)
        assert pool.healthy_count() == 0

    def test_check_health_nonexistent_worker(self, tmp_path: Path) -> None:
        """check_health should return False for nonexistent worker."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path)
        assert pool.check_health(0) is False

    def test_shutdown_all_clears_instances(self, tmp_path: Path) -> None:
        """shutdown_all should clear all instances."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path)

        pool.get_or_create(0)
        pool.get_or_create(1)
        assert len(pool) == 2

        pool.shutdown_all()
        assert len(pool) == 0


class TestTypingSelf:
    """Tests for typing.Self usage in InstancePool."""

    def test_enter_returns_correct_type(self, tmp_path: Path) -> None:
        """__enter__ should return Self (InstancePool)."""
        config = PoolConfig(max_workers=4)
        pool = InstancePool(config, root_dir=tmp_path)

        # This tests that the type annotation is correct
        with pool as p:
            # p should have all InstancePool methods
            assert hasattr(p, "get_or_create")
            assert hasattr(p, "start_worker")
            assert hasattr(p, "shutdown_all")
