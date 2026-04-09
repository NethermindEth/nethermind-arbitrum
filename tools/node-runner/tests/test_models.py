"""Tests for data models: ProcessState, LogEntry, ProcessStatus."""

from __future__ import annotations

from datetime import datetime

import pytest

from node_runner.config import ERROR_BUFFER_SIZE, LOG_BUFFER_SIZE
from node_runner.models import LogEntry, ProcessState, ProcessStatus


class TestLogEntry:
    @pytest.fixture()
    def error_entry(self) -> LogEntry:
        return LogEntry(
            timestamp=datetime.now(),
            process_name="test",
            level="ERROR",
            message="something failed",
            raw="ERROR something failed\n",
        )

    @pytest.fixture()
    def warn_entry(self) -> LogEntry:
        return LogEntry(
            timestamp=datetime.now(),
            process_name="test",
            level="WARN",
            message="something suspicious",
            raw="WARN something suspicious\n",
        )

    @pytest.fixture()
    def info_entry(self) -> LogEntry:
        return LogEntry(
            timestamp=datetime.now(),
            process_name="test",
            level="INFO",
            message="all good",
            raw="INFO all good\n",
        )

    def test_is_error(self, error_entry: LogEntry) -> None:
        assert error_entry.is_error is True
        assert error_entry.is_warning is False
        assert error_entry.is_notable is True

    def test_is_warning(self, warn_entry: LogEntry) -> None:
        assert warn_entry.is_error is False
        assert warn_entry.is_warning is True
        assert warn_entry.is_notable is True

    def test_info_not_notable(self, info_entry: LogEntry) -> None:
        assert info_entry.is_error is False
        assert info_entry.is_warning is False
        assert info_entry.is_notable is False

    def test_fatal_is_error(self) -> None:
        entry = LogEntry(
            timestamp=datetime.now(),
            process_name="test",
            level="FATAL",
            message="crash",
            raw="FATAL crash\n",
        )
        assert entry.is_error is True

    def test_crit_is_error(self) -> None:
        entry = LogEntry(
            timestamp=datetime.now(),
            process_name="test",
            level="CRIT",
            message="critical",
            raw="CRIT critical\n",
        )
        assert entry.is_error is True

    def test_warning_level_is_warning(self) -> None:
        entry = LogEntry(
            timestamp=datetime.now(),
            process_name="test",
            level="WARNING",
            message="warning",
            raw="WARNING warning\n",
        )
        assert entry.is_warning is True


class TestProcessStatus:
    def test_all_values(self) -> None:
        expected = {
            "pending", "initializing", "starting", "healthy",
            "unhealthy", "stopping", "stopped", "crashed",
        }
        actual = {s.value for s in ProcessStatus}
        assert actual == expected


class TestProcessState:
    def test_default_state(self) -> None:
        state = ProcessState(name="test")
        assert state.status == ProcessStatus.PENDING
        assert state.pid is None
        assert state.start_time is None
        assert state.exit_code is None
        assert len(state.log_buffer) == 0
        assert len(state.error_log) == 0

    def test_is_terminal(self) -> None:
        state = ProcessState(name="test")
        assert state.is_terminal is False

        state.status = ProcessStatus.STOPPED
        assert state.is_terminal is True

        state.status = ProcessStatus.CRASHED
        assert state.is_terminal is True

        state.status = ProcessStatus.HEALTHY
        assert state.is_terminal is False

    def test_log_buffer_ring_behavior(self) -> None:
        state = ProcessState(name="test")
        assert state.log_buffer.maxlen == LOG_BUFFER_SIZE

        # Fill beyond capacity
        for i in range(LOG_BUFFER_SIZE + 100):
            entry = LogEntry(
                timestamp=datetime.now(),
                process_name="test",
                level="INFO",
                message=f"line {i}",
                raw=f"INFO line {i}\n",
            )
            state.log_buffer.append(entry)

        assert len(state.log_buffer) == LOG_BUFFER_SIZE
        # Oldest entries should be evicted
        assert state.log_buffer[0].message == "line 100"

    def test_error_buffer_ring_behavior(self) -> None:
        state = ProcessState(name="test")
        assert state.error_log.maxlen == ERROR_BUFFER_SIZE

        for i in range(ERROR_BUFFER_SIZE + 50):
            entry = LogEntry(
                timestamp=datetime.now(),
                process_name="test",
                level="ERROR",
                message=f"error {i}",
                raw=f"ERROR error {i}\n",
            )
            state.error_log.append(entry)

        assert len(state.error_log) == ERROR_BUFFER_SIZE
        assert state.error_log[0].message == "error 50"

    def test_error_count(self) -> None:
        state = ProcessState(name="test")
        now = datetime.now()

        state.error_log.append(LogEntry(now, "test", "ERROR", "e1", "e1\n"))
        state.error_log.append(LogEntry(now, "test", "WARN", "w1", "w1\n"))
        state.error_log.append(LogEntry(now, "test", "ERROR", "e2", "e2\n"))

        assert state.error_count == 2
        assert state.warning_count == 1

    def test_warning_count(self) -> None:
        state = ProcessState(name="test")
        now = datetime.now()

        state.error_log.append(LogEntry(now, "test", "WARN", "w1", "w1\n"))
        state.error_log.append(LogEntry(now, "test", "WARNING", "w2", "w2\n"))

        assert state.warning_count == 2
        assert state.error_count == 0
