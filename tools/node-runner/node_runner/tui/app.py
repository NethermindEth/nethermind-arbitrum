"""Main Textual application for node-runner.

Composes the TUI layout, starts processes via the orchestrator,
streams logs to panes, and handles graceful shutdown.
"""

from __future__ import annotations

import asyncio
import logging
import shutil
import subprocess
from pathlib import Path
from typing import TYPE_CHECKING

from textual.app import App, ComposeResult
from textual.binding import Binding
from textual.containers import Container
from textual.widgets import Footer, Static

from ..crash_report import write_crash_report, write_error_logs
from ..orchestrator import Orchestrator
from .widgets import CombinedLogPane, ProcessLogPane

# Route init process logs to the corresponding runtime pane
_PANE_FALLBACK: dict[str, str] = {
    "init-el": "nitro-el",
}

if TYPE_CHECKING:
    from ..config import RunnerConfig

logger = logging.getLogger(__name__)

_CSS_PATH = Path(__file__).parent / "styles.tcss"


class NodeRunnerApp(App[None]):
    """Textual application for running and monitoring Arbitrum node processes.

    Layout:
    - Status bar (top): mode, network, process health
    - Process panes (center): split or combined view
    - Help bar (bottom): key bindings
    """

    CSS_PATH = _CSS_PATH
    TITLE = "node-runner"

    BINDINGS = [
        Binding("q", "quit_app", "Quit", priority=True),
        Binding("x", "stop_nodes", "Stop nodes"),
        Binding("s", "split_view", "Split view"),
        Binding("c", "combined_view", "Combined view"),
        Binding("f", "toggle_filter", "Errors only"),
        Binding("y", "copy_log", "Copy log"),
        Binding("1", "focus_pane(1)", "Pane 1", show=False),
        Binding("2", "focus_pane(2)", "Pane 2", show=False),
        Binding("3", "focus_pane(3)", "Pane 3", show=False),
    ]

    def __init__(self, config: RunnerConfig) -> None:
        super().__init__()
        self.config = config
        self._orchestrator = Orchestrator(config)
        self._panes: dict[str, ProcessLogPane] = {}
        self._combined_pane: CombinedLogPane | None = None
        self._log_pointers: dict[str, int] = {}
        self._stream_task: asyncio.Task[None] | None = None
        self._monitor_task: asyncio.Task[None] | None = None
        self._in_split_view = True
        self._focused_pane_name: str | None = None

    def compose(self) -> ComposeResult:
        # Status bar
        mode_text = self.config.mode.value.upper()
        network_text = self.config.network.value.capitalize()
        yield Static(
            f" {mode_text} | {network_text} | Starting...",
            id="status-bar",
        )

        # Build process panes based on mode.
        # Verification EL is a background process — no dedicated pane,
        # its logs only appear in combined view.
        proc_list = self._orchestrator._build_process_list()
        pane_count = len(proc_list)

        container_class = "three-pane" if pane_count == 3 else "two-pane"
        with Container(id="split-container", classes=container_class):
            for i, proc in enumerate(proc_list):
                # In 3-pane grid, last pane spans full width
                classes = "span-full" if (pane_count == 3 and i == 2) else ""
                pane = ProcessLogPane(
                    proc.name,
                    id=f"pane-{proc.name}",
                    classes=classes,
                )
                self._panes[proc.name] = pane
                yield pane

        # Combined view (hidden by default)
        with Container(id="combined-container"):
            self._combined_pane = CombinedLogPane(id="combined-pane")
            yield self._combined_pane

        yield Footer()

    async def on_mount(self) -> None:
        """Start processes and background workers after TUI mounts."""
        self.run_worker(self._startup_and_monitor(), exclusive=True)

    async def _startup_and_monitor(self) -> None:
        """Orchestrate startup, then stream logs and monitor."""
        status_bar = self.query_one("#status-bar", Static)

        try:
            await self._orchestrator.start_all()
        except Exception as e:
            mode_text = self.config.mode.value.upper()
            network_text = self.config.network.value.capitalize()
            status_bar.update(
                f" {mode_text} | {network_text} | STARTUP FAILED: {e}"
            )
            logger.error("Startup failed: %s", e)
            # Still stream whatever logs were captured
            self._stream_task = asyncio.create_task(self._stream_logs())
            return

        # Update status bar
        mode_text = self.config.mode.value.upper()
        network_text = self.config.network.value.capitalize()
        procs = self._orchestrator.processes
        status_parts = [f"{p.name}:{p.state.status.value}" for p in procs]
        status_bar.update(
            f" {mode_text} | {network_text} | {' | '.join(status_parts)}"
        )

        # Start background tasks
        self._stream_task = asyncio.create_task(self._stream_logs())
        self._monitor_task = asyncio.create_task(self._monitor_loop())

    async def _stream_logs(self) -> None:
        """Poll log buffers and push new entries to TUI panes."""
        # Initialize pointers to current buffer positions
        for proc in self._orchestrator.processes:
            self._log_pointers[proc.name] = 0

        while True:
            for proc in self._orchestrator.processes:
                buf = proc.state.log_buffer
                pointer = self._log_pointers.get(proc.name, 0)
                buf_list = list(buf)
                new_entries = buf_list[pointer:]

                if new_entries:
                    pane = self._panes.get(proc.name)
                    if pane is None:
                        pane = self._panes.get(_PANE_FALLBACK.get(proc.name, ""))
                    for entry in new_entries:
                        if pane is not None:
                            pane.write_entry(entry)
                        if self._combined_pane is not None:
                            self._combined_pane.write_entry(entry)

                    self._log_pointers[proc.name] = len(buf_list)

                # Update pane header status
                pane = self._panes.get(proc.name)
                if pane is None:
                    pane = self._panes.get(_PANE_FALLBACK.get(proc.name, ""))
                if pane is not None:
                    pane.update_status(
                        proc.state.status,
                        proc.state.error_count,
                        proc.state.warning_count,
                    )

            await asyncio.sleep(0.1)

    async def _monitor_loop(self) -> None:
        """Monitor for process crashes and update status bar."""
        status_bar = self.query_one("#status-bar", Static)

        while not self._orchestrator.is_shutdown_requested:
            for proc in self._orchestrator.processes:
                proc.check_for_crash()

            # Update status bar
            mode_text = self.config.mode.value.upper()
            network_text = self.config.network.value.capitalize()
            procs = self._orchestrator.processes
            status_parts = []
            for p in procs:
                status_parts.append(f"{p.name}:{p.state.status.value}")
            status_bar.update(
                f" {mode_text} | {network_text} | {' | '.join(status_parts)}"
            )

            await asyncio.sleep(2.0)

    async def _shutdown(self) -> None:
        """Graceful shutdown: stop processes, write reports."""
        # Cancel background tasks
        if self._stream_task and not self._stream_task.done():
            self._stream_task.cancel()
        if self._monitor_task and not self._monitor_task.done():
            self._monitor_task.cancel()

        # Signal orchestrator
        self._orchestrator.request_shutdown()

        # Update status bar
        try:
            status_bar = self.query_one("#status-bar", Static)
            status_bar.update(" Shutting down...")
        except Exception:
            pass

        # Stop all processes
        await self._orchestrator.stop_all()

        # Write crash report and error logs
        processes = self._orchestrator.processes
        if processes:
            write_crash_report(processes, self.config.crash_report_dir())
            write_error_logs(processes, self.config.error_log_dir())

    # ── Actions ──

    async def action_quit_app(self) -> None:
        """Graceful shutdown then exit."""
        await self._shutdown()
        self.exit()

    async def action_stop_nodes(self) -> None:
        """Stop all processes but keep the TUI open for log review."""
        if self._orchestrator.is_shutdown_requested:
            return

        # Stop the monitor loop (processes are going down)
        if self._monitor_task and not self._monitor_task.done():
            self._monitor_task.cancel()

        self._orchestrator.request_shutdown()

        status_bar = self.query_one("#status-bar", Static)
        status_bar.update(" Stopping nodes...")

        await self._orchestrator.stop_all()

        # Write reports while logs are still in memory
        processes = self._orchestrator.processes
        if processes:
            write_crash_report(processes, self.config.crash_report_dir())
            write_error_logs(processes, self.config.error_log_dir())

        # Update status bar and pane headers
        mode_text = self.config.mode.value.upper()
        network_text = self.config.network.value.capitalize()
        status_bar.update(f" {mode_text} | {network_text} | STOPPED (q to quit)")

        for proc in processes:
            pane = self._panes.get(proc.name)
            if pane is None:
                pane = self._panes.get(_PANE_FALLBACK.get(proc.name, ""))
            if pane is not None:
                pane.update_status(
                    proc.state.status, proc.state.error_count, proc.state.warning_count
                )

    def action_split_view(self) -> None:
        """Switch to split (per-process) view."""
        if self._in_split_view:
            return
        self._in_split_view = True
        split = self.query_one("#split-container")
        combined = self.query_one("#combined-container")
        split.styles.display = "block"
        combined.styles.display = "none"

    def action_combined_view(self) -> None:
        """Switch to combined (interleaved) view."""
        if not self._in_split_view:
            return
        self._in_split_view = False
        split = self.query_one("#split-container")
        combined = self.query_one("#combined-container")
        split.styles.display = "none"
        combined.styles.display = "block"

    def action_toggle_filter(self) -> None:
        """Toggle errors-only filter on all panes."""
        for pane in self._panes.values():
            pane.toggle_errors_only()
            pane.clear_log()
        if self._combined_pane is not None:
            self._combined_pane.toggle_errors_only()
            self._combined_pane.clear_log()

        # Re-stream all buffered entries through the filter
        for proc in self._orchestrator.processes:
            pane = self._panes.get(proc.name)
            for entry in proc.state.log_buffer:
                if pane is not None:
                    pane.write_entry(entry)
                if self._combined_pane is not None:
                    self._combined_pane.write_entry(entry)

    def action_focus_pane(self, pane_number: int) -> None:
        """Focus a specific process pane by number (1-indexed)."""
        pane_names = list(self._panes.keys())
        idx = pane_number - 1
        if 0 <= idx < len(pane_names):
            # Remove focus from all panes
            for pane in self._panes.values():
                pane.remove_class("focused-pane")
            # Focus selected pane
            name = pane_names[idx]
            self._focused_pane_name = name
            target = self._panes[name]
            target.add_class("focused-pane")
            target.focus()

    def action_copy_log(self) -> None:
        """Copy log content from the focused pane (or all) to system clipboard."""
        lines: list[str] = []

        if self._in_split_view and self._focused_pane_name:
            # Copy only the focused pane's process logs
            for proc in self._orchestrator.processes:
                if proc.name == self._focused_pane_name:
                    lines = [e.message for e in proc.state.log_buffer]
                    break
        else:
            # Combined view or no pane focused: copy all process logs
            for proc in self._orchestrator.processes:
                for entry in proc.state.log_buffer:
                    lines.append(f"[{entry.process_name}] {entry.message}")

        if not lines:
            self.notify("No log content to copy", severity="warning", timeout=2)
            return

        text = "\n".join(lines)

        clip_cmd = shutil.which("pbcopy") or shutil.which("xclip") or shutil.which("xsel")
        if clip_cmd is None:
            self.notify("No clipboard tool found", severity="error", timeout=3)
            return

        args = [clip_cmd]
        if "xclip" in clip_cmd:
            args += ["-selection", "clipboard"]
        elif "xsel" in clip_cmd:
            args += ["--clipboard", "--input"]

        try:
            subprocess.run(args, input=text.encode(), check=True)  # noqa: S603
        except subprocess.SubprocessError:
            self.notify("Failed to copy to clipboard", severity="error", timeout=3)
            return

        count = len(lines)
        label = self._focused_pane_name or "all"
        self.notify(f"Copied {count} lines ({label})", timeout=2)
