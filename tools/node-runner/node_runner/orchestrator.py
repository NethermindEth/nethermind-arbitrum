"""Process orchestration: startup sequencing, monitoring, and shutdown.

Manages the dependency graph between processes for each mode and handles
the full lifecycle from initialization through graceful shutdown.
"""

from __future__ import annotations

import asyncio
import contextlib
import logging
import shutil
from typing import TYPE_CHECKING

from .config import HEALTH_CHECK_TIMEOUT_S, Mode
from .exceptions import HealthCheckError, StartupError
from .processes.base import BaseProcess
from .processes.init_el import InitELProcess
from .processes.nethermind import NethermindProcess
from .processes.nitro_cl import NitroCLProcess
from .processes.nitro_el import NitroELProcess

if TYPE_CHECKING:
    from .config import RunnerConfig

logger = logging.getLogger(__name__)


class Orchestrator:
    """Manages the lifecycle of all processes for a node-runner session.

    Responsibilities:
    - Build the correct process graph per mode
    - Start processes in dependency order with health gates
    - Monitor processes for crashes
    - Stop all processes in reverse order on shutdown
    """

    def __init__(self, config: RunnerConfig) -> None:
        self.config = config
        self._processes: list[BaseProcess] = []
        self._shutdown_event = asyncio.Event()
        self._started = False

    @property
    def processes(self) -> list[BaseProcess]:
        """Currently managed processes (ordered by startup dependency)."""
        return list(self._processes)

    @property
    def is_shutdown_requested(self) -> bool:
        return self._shutdown_event.is_set()

    def _build_process_list(self) -> list[BaseProcess]:
        """Build ordered process list based on configured mode.

        Processes are ordered so that each process at index i depends on
        all processes at indices 0..i-1 being healthy first.
        """
        match self.config.mode:
            case Mode.COMPARISON:
                return [
                    NitroELProcess(self.config),
                    NethermindProcess(self.config),
                    NitroCLProcess(self.config, Mode.COMPARISON),
                ]
            case Mode.NITRO_NM:
                return [
                    NethermindProcess(self.config),
                    NitroCLProcess(self.config, Mode.NITRO_NM),
                ]
            case Mode.NITRO_NITRO:
                return [
                    NitroELProcess(self.config),
                    NitroCLProcess(self.config, Mode.NITRO_NITRO),
                ]

    def _needs_init(self) -> bool:
        """Check whether Nitro EL initialization is required.

        For mainnet, init downloads a pre-built genesis snapshot via
        --init.latest=genesis. For other networks (e.g., Sepolia), init
        creates genesis from the real L1 init message via --init.empty=true
        with the parent-chain-reader enabled (default). This ensures the
        genesis hash matches the CL's genesis derived from L1.

        Init is needed if:
        - Mode requires a Nitro EL
        - User passed --init (force_init) OR the data directory is empty
        """
        if not self.config.needs_nitro_el:
            return False

        if self.config.force_init:
            return True

        el_data = self.config.data_path("nitro-el")
        if not el_data.exists():
            return True

        # Check for any content (even an empty dir means no init was done)
        return not any(el_data.iterdir())

    async def clean_data(self) -> None:
        """Remove all data directories for the current network."""
        base = self.config.data_dir / self.config.network.value
        if base.exists():
            logger.info("Cleaning data directory: %s", base)
            shutil.rmtree(base)
        base.mkdir(parents=True, exist_ok=True)

    async def run_init(self) -> None:
        """Run Nitro EL genesis initialization.

        Creates genesis state. On mainnet, downloads a pre-built snapshot.
        On other networks, reads the real init message from L1. Blocking one-shot.

        Raises:
            InitError: If initialization fails
        """
        logger.info("Initializing Nitro EL (downloading genesis from L1)...")
        init_proc = InitELProcess(self.config)
        self._processes = [init_proc]  # preserve for TUI + crash report on failure
        await init_proc.run_to_completion()
        self._processes = []  # clear on success; start_all rebuilds the list
        logger.info("Nitro EL initialization complete")

    async def start_all(self) -> None:
        """Start all processes in dependency order with health gates.

        Raises:
            StartupError: If a process fails to start
            HealthCheckError: If a process doesn't become healthy in time
            InitError: If EL initialization fails
        """
        if self.config.clean:
            await self.clean_data()

        if self._needs_init():
            await self.run_init()

        self._processes = self._build_process_list()
        self._started = True

        for proc in self._processes:
            if self._shutdown_event.is_set():
                break

            logger.info("Starting %s...", proc.name)

            try:
                await proc.start()
            except Exception as e:
                raise StartupError(
                    f"Failed to start {proc.name}: {e}",
                    process_name=proc.name,
                ) from e

            port = proc.health_check_port
            if port is not None:
                logger.info(
                    "Waiting for %s to become healthy (port %d)...",
                    proc.name,
                    port,
                )

            healthy = await proc.wait_for_healthy(timeout=HEALTH_CHECK_TIMEOUT_S)
            if not healthy:
                raise HealthCheckError(
                    f"{proc.name} did not become healthy within {HEALTH_CHECK_TIMEOUT_S}s",
                    process_name=proc.name,
                    port=port or 0,
                    timeout=HEALTH_CHECK_TIMEOUT_S,
                )

            logger.info("%s is healthy (PID %d)", proc.name, proc.state.pid or 0)

    async def stop_all(self) -> None:
        """Stop all processes in reverse startup order.

        CL is stopped first (it depends on ELs), then ELs.
        """
        if not self._processes:
            return

        logger.info("Stopping all processes...")

        for proc in reversed(self._processes):
            if proc.state.is_terminal:
                continue
            logger.info("Stopping %s (PID %d)...", proc.name, proc.state.pid or 0)
            clean = await proc.stop()
            if clean:
                logger.info("%s stopped cleanly", proc.name)
            else:
                logger.warning("%s required force-kill", proc.name)

    async def monitor(self) -> None:
        """Monitor processes for crashes until shutdown is requested.

        Runs in a loop checking process status. Does not restart crashed processes
        (that would require re-init which is too heavy for automatic recovery).
        Instead, it updates state so the TUI can display crash information.
        """
        while not self._shutdown_event.is_set():
            for proc in self._processes:
                proc.check_for_crash()

            with contextlib.suppress(TimeoutError):
                await asyncio.wait_for(
                    self._shutdown_event.wait(),
                    timeout=2.0,
                )

    def request_shutdown(self) -> None:
        """Signal that shutdown has been requested (e.g., Ctrl+C from TUI)."""
        self._shutdown_event.set()

    def summary(self) -> dict[str, str]:
        """Return a summary of all process states for display."""
        return {
            proc.name: proc.state.status.value
            for proc in self._processes
        }
