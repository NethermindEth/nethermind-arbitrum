"""TUI widgets for node-runner.

Provides ProcessLogPane: a self-contained panel showing a process's logs
with a status header and auto-scrolling RichLog.
"""

from __future__ import annotations

from typing import TYPE_CHECKING

from rich.text import Text
from textual.reactive import reactive
from textual.widget import Widget
from textual.widgets import RichLog, Static

from ..models import LogEntry, ProcessStatus

if TYPE_CHECKING:
    from textual.app import ComposeResult


_STATUS_STYLE: dict[ProcessStatus, tuple[str, str]] = {
    ProcessStatus.PENDING: ("PENDING", "status-starting"),
    ProcessStatus.INITIALIZING: ("INIT", "status-starting"),
    ProcessStatus.STARTING: ("STARTING", "status-starting"),
    ProcessStatus.HEALTHY: ("HEALTHY", "status-healthy"),
    ProcessStatus.UNHEALTHY: ("UNHEALTHY", "status-error"),
    ProcessStatus.STOPPING: ("STOPPING", "status-starting"),
    ProcessStatus.STOPPED: ("STOPPED", "status-error"),
    ProcessStatus.CRASHED: ("CRASHED", "status-error"),
}

_LEVEL_STYLE: dict[str, str] = {
    "ERROR": "bold red",
    "FATAL": "bold red",
    "CRIT": "bold red",
    "WARN": "yellow",
    "WARNING": "yellow",
    "INFO": "",
    "DEBUG": "dim",
    "TRACE": "dim italic",
}


class ProcessHeader(Static):
    """Single-line status header for a process pane."""

    process_name: reactive[str] = reactive("")
    status: reactive[ProcessStatus] = reactive(ProcessStatus.PENDING)
    error_count: reactive[int] = reactive(0)
    warning_count: reactive[int] = reactive(0)

    def __init__(
        self,
        process_name: str,
        **kwargs: object,
    ) -> None:
        super().__init__(**kwargs)
        self.process_name = process_name

    def render(self) -> Text:
        label, css_class = _STATUS_STYLE.get(
            self.status, ("UNKNOWN", "status-error")
        )

        text = Text()
        text.append(f" {self.process_name} ", style="bold")
        text.append("[")

        status_style = {
            "status-healthy": "green",
            "status-starting": "yellow",
            "status-error": "red",
        }.get(css_class, "")
        text.append(label, style=status_style)
        text.append("]")

        if self.error_count > 0 or self.warning_count > 0:
            text.append("  ")
            if self.error_count > 0:
                text.append(f"E:{self.error_count}", style="bold red")
                text.append(" ")
            if self.warning_count > 0:
                text.append(f"W:{self.warning_count}", style="yellow")

        return text


class ProcessLogPane(Widget):
    """A pane displaying a process's log output with a status header.

    Attributes:
        process_name: The name of the process this pane tracks.
    """

    DEFAULT_CSS = """
    ProcessLogPane {
        layout: vertical;
    }
    """

    def __init__(
        self,
        process_name: str,
        *,
        id: str | None = None,
        classes: str | None = None,
    ) -> None:
        super().__init__(id=id, classes=classes)
        self.process_name = process_name
        self._errors_only = False
        self._log_widget: RichLog | None = None
        self._header_widget: ProcessHeader | None = None

    def compose(self) -> ComposeResult:
        self._header_widget = ProcessHeader(
            self.process_name,
            classes="pane-header",
        )
        yield self._header_widget
        self._log_widget = RichLog(
            highlight=False,
            markup=False,
            wrap=True,
            auto_scroll=True,
            classes="log-view",
        )
        yield self._log_widget

    def write_entry(self, entry: LogEntry) -> None:
        """Append a log entry to this pane."""
        if self._log_widget is None:
            return

        if self._errors_only and not entry.is_notable:
            return

        text = _styled_log_line(entry)
        self._log_widget.write(text)

    def update_status(
        self,
        status: ProcessStatus,
        error_count: int = 0,
        warning_count: int = 0,
    ) -> None:
        """Update the header status indicators."""
        if self._header_widget is not None:
            self._header_widget.status = status
            self._header_widget.error_count = error_count
            self._header_widget.warning_count = warning_count

    def toggle_errors_only(self) -> bool:
        """Toggle the errors-only filter. Returns the new state."""
        self._errors_only = not self._errors_only
        return self._errors_only

    def clear_log(self) -> None:
        """Clear all log lines from the display."""
        if self._log_widget is not None:
            self._log_widget.clear()

    @property
    def errors_only(self) -> bool:
        return self._errors_only


def _styled_log_line(entry: LogEntry, *, show_process: bool = False) -> Text:
    """Create a Rich Text object with appropriate styling for a log entry."""
    style = _LEVEL_STYLE.get(entry.level, "")
    text = Text()

    if show_process:
        text.append(f"[{entry.process_name}] ", style="bold cyan")

    text.append(entry.message, style=style)
    return text


class CombinedLogPane(Widget):
    """A pane that shows interleaved logs from all processes."""

    DEFAULT_CSS = """
    CombinedLogPane {
        layout: vertical;
    }
    """

    def __init__(self, **kwargs: object) -> None:
        super().__init__(**kwargs)
        self._errors_only = False
        self._log_widget: RichLog | None = None

    def compose(self) -> ComposeResult:
        header = Static(
            " All Processes (combined)",
            classes="pane-header",
        )
        yield header
        self._log_widget = RichLog(
            highlight=False,
            markup=False,
            wrap=True,
            auto_scroll=True,
            classes="log-view",
        )
        yield self._log_widget

    def write_entry(self, entry: LogEntry) -> None:
        """Append a log entry with process prefix."""
        if self._log_widget is None:
            return

        if self._errors_only and not entry.is_notable:
            return

        text = _styled_log_line(entry, show_process=True)
        self._log_widget.write(text)

    def toggle_errors_only(self) -> bool:
        self._errors_only = not self._errors_only
        return self._errors_only

    def clear_log(self) -> None:
        if self._log_widget is not None:
            self._log_widget.clear()

    @property
    def errors_only(self) -> bool:
        return self._errors_only
