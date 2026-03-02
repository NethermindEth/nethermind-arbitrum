"""Tests for configuration, logging, and exceptions modules.

Tests cover:
- Pydantic configuration validation
- Port range enforcement
- Logging setup and formatting
- Exception hierarchy and context capture
"""

from __future__ import annotations

import logging
from pathlib import Path

import pytest
from pydantic import ValidationError

from comparison_runner.config import (
    DEFAULT_BASE_PORT,
    DEFAULT_STARTUP_TIMEOUT_S,
    DEFAULT_TIMEOUT_S,
    MAX_PORT,
    MAX_WORKERS,
    MEMORY_PER_WORKER_GB,
    MIN_PORT,
    SYSTEM_RESERVE_GB,
    ComparisonConfig,
    PortAllocator,
)
from comparison_runner.exceptions import (
    ComparisonTestError,
    ConfigurationError,
    NethermindStartupError,
    ProcessTerminationError,
    RPCError,
    TestExecutionError,
    WorkerPoolError,
)
from comparison_runner.logging_utils import (
    get_logger,
    log_progress,
    log_test_status,
    log_worker_status,
    reset_logger,
    setup_logging,
)
from comparison_runner.models import TestResult, TestStatus


class TestComparisonConfig:
    """Tests for ComparisonConfig Pydantic model."""

    def test_default_values(self) -> None:
        """Config should have sensible defaults."""
        config = ComparisonConfig()
        assert config.base_port == DEFAULT_BASE_PORT
        assert config.max_workers == MAX_WORKERS
        assert config.test_timeout_s == DEFAULT_TIMEOUT_S
        assert config.startup_timeout_s == DEFAULT_STARTUP_TIMEOUT_S
        assert config.verbose is False

    def test_valid_port_accepted(self) -> None:
        """Valid unprivileged ports should be accepted."""
        config = ComparisonConfig(base_port=20551)
        assert config.base_port == 20551

        config = ComparisonConfig(base_port=MIN_PORT)
        assert config.base_port == MIN_PORT

        config = ComparisonConfig(base_port=MAX_PORT)
        assert config.base_port == MAX_PORT

    def test_privileged_port_rejected(self) -> None:
        """Ports below 1024 should be rejected."""
        with pytest.raises(ValidationError) as exc_info:
            ComparisonConfig(base_port=80)

        errors = exc_info.value.errors()
        assert len(errors) == 1
        assert "1024" in str(errors[0]["msg"])

    def test_port_zero_accepted_for_ephemeral(self) -> None:
        """Port 0 should be accepted for ephemeral allocation."""
        config = ComparisonConfig(base_port=0)
        assert config.base_port == 0
        # All workers get port 0 for ephemeral allocation
        assert config.get_worker_port(0) == 0
        assert config.get_worker_port(5) == 0

    def test_port_too_high_rejected(self) -> None:
        """Ports above 65535 should be rejected."""
        with pytest.raises(ValidationError):
            ComparisonConfig(base_port=70000)

    def test_config_is_frozen(self) -> None:
        """Config should be immutable after creation."""
        config = ComparisonConfig()
        # Pydantic v1 raises TypeError, v2 raises ValidationError
        with pytest.raises((TypeError, ValidationError)):
            config.base_port = 30000  # type: ignore[misc]

    def test_extra_fields_forbidden(self) -> None:
        """Unknown fields should be rejected."""
        with pytest.raises(ValidationError):
            ComparisonConfig(unknown_field="value")  # type: ignore[call-arg]

    def test_worker_count_validation(self) -> None:
        """Worker count should be within valid range."""
        # Valid
        config = ComparisonConfig(max_workers=4)
        assert config.max_workers == 4

        # Too low
        with pytest.raises(ValidationError):
            ComparisonConfig(max_workers=0)

        # Too high
        with pytest.raises(ValidationError):
            ComparisonConfig(max_workers=100)

    def test_get_worker_port(self) -> None:
        """Worker port calculation should be correct."""
        config = ComparisonConfig(base_port=20000)
        assert config.get_worker_port(0) == 20000
        assert config.get_worker_port(1) == 20001
        assert config.get_worker_port(7) == 20007

    def test_get_worker_port_overflow(self) -> None:
        """Worker port should raise on overflow."""
        config = ComparisonConfig(base_port=65530)
        with pytest.raises(ValueError, match="exceeds maximum"):
            config.get_worker_port(10)

    def test_validate_worker_ports(self) -> None:
        """Port validation for multiple workers."""
        config = ComparisonConfig(base_port=20000)
        # Should not raise
        config.validate_worker_ports(8)

        config_high = ComparisonConfig(base_port=65530)
        with pytest.raises(ValueError, match="maximum port"):
            config_high.validate_worker_ports(10)

    def test_paths_optional(self) -> None:
        """Path fields should be optional."""
        config = ComparisonConfig()
        assert config.nitro_path is None
        assert config.nethermind_path is None

        config_with_paths = ComparisonConfig(
            nitro_path=Path("/tmp/nitro"),
            nethermind_path=Path("/tmp/nethermind"),
        )
        assert config_with_paths.nitro_path == Path("/tmp/nitro")


class TestConstants:
    """Tests for module-level constants."""

    def test_memory_constants_positive(self) -> None:
        """Memory constants should be positive."""
        assert MEMORY_PER_WORKER_GB > 0
        assert SYSTEM_RESERVE_GB > 0

    def test_port_range_valid(self) -> None:
        """Port range should be valid."""
        assert MIN_PORT == 1024
        assert MAX_PORT == 65535
        assert MIN_PORT < MAX_PORT

    def test_max_workers_reasonable(self) -> None:
        """Max workers should be reasonable."""
        assert 1 <= MAX_WORKERS <= 16


class TestLogging:
    """Tests for logging utilities."""

    def setup_method(self) -> None:
        """Reset logger before each test."""
        reset_logger()

    def teardown_method(self) -> None:
        """Clean up logger after each test."""
        reset_logger()

    def test_setup_logging_returns_logger(self) -> None:
        """setup_logging should return a logger instance."""
        logger = setup_logging()
        assert isinstance(logger, logging.Logger)
        assert logger.name == "comparison_runner"

    def test_setup_logging_idempotent(self) -> None:
        """Multiple calls should return same logger."""
        logger1 = setup_logging()
        logger2 = setup_logging()
        assert logger1 is logger2

    def test_get_logger_creates_if_needed(self) -> None:
        """get_logger should create logger if not exists."""
        logger = get_logger()
        assert isinstance(logger, logging.Logger)

    def test_logger_format(self) -> None:
        """Logger should use correct format."""
        logger = setup_logging()
        assert len(logger.handlers) == 1
        handler = logger.handlers[0]
        assert isinstance(handler.formatter, logging.Formatter)
        assert handler.formatter.datefmt == "%H:%M:%S"

    def test_log_test_status_passed(self) -> None:
        """log_test_status should log passed tests at INFO."""
        result = TestResult(
            name="TestTransfer",
            status=TestStatus.PASSED,
            duration_s=1.23,
        )
        # Just ensure it doesn't raise
        log_test_status(result)

    def test_log_test_status_failed(self) -> None:
        """log_test_status should log failed tests at ERROR."""
        result = TestResult(
            name="TestTransfer",
            status=TestStatus.FAILED,
            duration_s=1.23,
            worker_id=2,
        )
        log_test_status(result)

    def test_log_worker_status(self) -> None:
        """log_worker_status should include worker prefix."""
        log_worker_status(1, "Starting test")

    def test_log_progress(self) -> None:
        """log_progress should format correctly."""
        log_progress(completed=5, total=10, passed=4, failed=1)


class TestExceptions:
    """Tests for custom exception hierarchy."""

    def test_all_inherit_from_base(self) -> None:
        """All custom exceptions should inherit from ComparisonTestError."""
        assert issubclass(ConfigurationError, ComparisonTestError)
        assert issubclass(NethermindStartupError, ComparisonTestError)
        assert issubclass(TestExecutionError, ComparisonTestError)
        assert issubclass(RPCError, ComparisonTestError)
        assert issubclass(WorkerPoolError, ComparisonTestError)
        assert issubclass(ProcessTerminationError, ComparisonTestError)

    def test_base_exception_catchable(self) -> None:
        """Should be able to catch all with base exception."""
        exceptions = [
            ConfigurationError("bad config"),
            NethermindStartupError("failed to start"),
            TestExecutionError("test failed"),
            RPCError("rpc error"),
            WorkerPoolError("pool error"),
        ]

        for exc in exceptions:
            with pytest.raises(ComparisonTestError):
                raise exc

    def test_configuration_error_field(self) -> None:
        """ConfigurationError should capture field name."""
        exc = ConfigurationError("Invalid port", field="base_port")
        assert exc.field == "base_port"
        assert "Invalid port" in str(exc)

    def test_nethermind_startup_error_context(self) -> None:
        """NethermindStartupError should capture context."""
        exc = NethermindStartupError(
            "Timeout waiting for RPC",
            port=20551,
            log_dir=Path("/tmp/logs"),
            worker_id=1,
        )
        assert exc.port == 20551
        assert exc.log_dir == Path("/tmp/logs")
        assert exc.worker_id == 1
        assert "port=20551" in str(exc)
        assert "worker=1" in str(exc)

    def test_test_execution_error_context(self) -> None:
        """TestExecutionError should capture test context."""
        exc = TestExecutionError(
            "Test timed out",
            test_name="TestTransfer",
            exit_code=-1,
            worker_id=2,
        )
        assert exc.test_name == "TestTransfer"
        assert exc.exit_code == -1
        assert "test=TestTransfer" in str(exc)

    def test_rpc_error_context(self) -> None:
        """RPCError should capture RPC context."""
        exc = RPCError(
            "Connection refused",
            method="debug_reinitialize",
            port=20551,
        )
        assert exc.method == "debug_reinitialize"
        assert exc.port == 20551
        assert "method=debug_reinitialize" in str(exc)

    def test_worker_pool_error_context(self) -> None:
        """WorkerPoolError should capture pool state."""
        exc = WorkerPoolError(
            "All workers crashed",
            active_workers=0,
            total_workers=4,
        )
        assert exc.active_workers == 0
        assert exc.total_workers == 4

    def test_process_termination_error_context(self) -> None:
        """ProcessTerminationError should capture process info."""
        exc = ProcessTerminationError(
            "Process did not terminate",
            pid=12345,
            signal_sent="SIGTERM",
        )
        assert exc.pid == 12345
        assert exc.signal_sent == "SIGTERM"


class TestIntegration:
    """Integration tests for configuration and logging."""

    def setup_method(self) -> None:
        """Reset logger before each test."""
        reset_logger()

    def teardown_method(self) -> None:
        """Clean up logger after each test."""
        reset_logger()

    def test_config_with_logging(self) -> None:
        """Config and logging should work together."""
        config = ComparisonConfig(verbose=True)
        logger = setup_logging(level=logging.DEBUG if config.verbose else logging.INFO)
        assert logger.level == logging.DEBUG

    def test_exception_in_workflow(self) -> None:
        """Exceptions should work in typical workflow."""
        config = ComparisonConfig(base_port=20551)

        try:
            # Simulate port validation failure
            if config.base_port < 1024:
                raise ConfigurationError(
                    f"Port {config.base_port} is privileged",
                    field="base_port",
                )
        except ConfigurationError as e:
            assert e.field == "base_port"
            raise
        except ComparisonTestError:
            pytest.fail("Should have caught ConfigurationError specifically")


class TestPortAllocator:
    """Tests for PortAllocator class."""

    def test_initial_port_sequential(self) -> None:
        """initial_port should return sequential ports (base + worker_id)."""
        allocator = PortAllocator(base_port=20551, num_workers=4)
        assert allocator.initial_port(0) == 20551
        assert allocator.initial_port(1) == 20552
        assert allocator.initial_port(2) == 20553
        assert allocator.initial_port(3) == 20554

    def test_initial_port_custom_base(self) -> None:
        """initial_port should use custom base port."""
        allocator = PortAllocator(base_port=30000, num_workers=2)
        assert allocator.initial_port(0) == 30000
        assert allocator.initial_port(1) == 30001

    def test_initial_port_invalid_worker_id_negative(self) -> None:
        """initial_port should reject negative worker_id."""
        allocator = PortAllocator(base_port=20551, num_workers=4)
        with pytest.raises(ValueError, match="worker_id must be in range"):
            allocator.initial_port(-1)

    def test_initial_port_invalid_worker_id_too_high(self) -> None:
        """initial_port should reject worker_id >= num_workers."""
        allocator = PortAllocator(base_port=20551, num_workers=4)
        with pytest.raises(ValueError, match="worker_id must be in range"):
            allocator.initial_port(4)

    def test_overflow_port_starts_after_workers(self) -> None:
        """overflow_port should return ports beyond initial worker range."""
        allocator = PortAllocator(base_port=20551, num_workers=4)
        # Initial range is 20551-20554, overflow starts at 20555
        overflow = allocator.overflow_port()
        assert overflow >= 20555

    def test_overflow_port_increments(self) -> None:
        """Subsequent overflow_port calls should return increasing ports."""
        allocator = PortAllocator(base_port=20551, num_workers=4)
        port1 = allocator.overflow_port()
        port2 = allocator.overflow_port()
        port3 = allocator.overflow_port()
        assert port1 < port2 < port3

    def test_high_water_mark_tracks_overflow(self) -> None:
        """high_water_mark should track the highest allocated port."""
        allocator = PortAllocator(base_port=20551, num_workers=4)
        # Initial high water mark is 20554 (last worker port)
        assert allocator.high_water_mark == 20554

        # After overflow, it should increase
        overflow = allocator.overflow_port()
        assert allocator.high_water_mark == overflow

    def test_thread_safety_initial_port(self) -> None:
        """initial_port should be safe for concurrent access."""
        import threading

        allocator = PortAllocator(base_port=20551, num_workers=10)
        results: list[int] = []
        lock = threading.Lock()

        def worker(worker_id: int) -> None:
            port = allocator.initial_port(worker_id)
            with lock:
                results.append(port)

        threads = [threading.Thread(target=worker, args=(i,)) for i in range(10)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        # All ports should be unique and sequential
        assert sorted(results) == list(range(20551, 20561))

    def test_thread_safety_overflow_port(self) -> None:
        """overflow_port should be safe for concurrent access."""
        import threading

        allocator = PortAllocator(base_port=20551, num_workers=4)
        results: list[int] = []
        lock = threading.Lock()

        def worker() -> None:
            port = allocator.overflow_port()
            with lock:
                results.append(port)

        threads = [threading.Thread(target=worker) for _ in range(10)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()

        # All overflow ports should be unique
        assert len(results) == len(set(results)), "Duplicate overflow ports detected"
        # All should be >= 20555 (first overflow port)
        assert all(p >= 20555 for p in results)
