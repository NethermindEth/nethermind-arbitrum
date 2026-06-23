"""Nitro EL initialization (one-shot genesis creation).

For mainnet, downloads a pre-built genesis snapshot via --init.latest=genesis.
For other networks (e.g., Sepolia), creates genesis from the real L1 init
message via --init.empty=true with the parent-chain-reader enabled (default).
Must complete before starting the EL.
"""

from __future__ import annotations

import asyncio
from pathlib import Path
from typing import TYPE_CHECKING

from ..config import INIT_TIMEOUT_S, Network
from ..exceptions import InitError
from ..models import ProcessStatus
from .base import BaseProcess

if TYPE_CHECKING:
    from ..config import RunnerConfig


class InitELProcess(BaseProcess):
    """One-shot Nitro init-el process.

    Creates genesis and exits. Not a long-running process.
    Flags derived from arbitrum-nitro/Makefile:217-237 (init-el target).
    """

    def __init__(self, config: RunnerConfig) -> None:
        super().__init__(config)

    @property
    def name(self) -> str:
        return "init-el"

    @property
    def health_check_port(self) -> int | None:
        return None  # One-shot, no health check port

    def working_directory(self) -> Path:
        return self.config.nitro_path

    def build_command(self) -> list[str]:
        nc = self.config.network_config
        data_path = self.config.data_path("nitro-el")

        cmd = [
            str(self.config.nitro_binary),
            f"--chain.id={nc.chain_id}",
            f"--parent-chain.id={nc.parent_chain_id}",
            f"--persistent.global-config={data_path}",
            f"--parent-chain.connection.url={self.config.l1_rpc}",
            f"--parent-chain.blob-client.beacon-url={self.config.l1_beacon}",
            "--init.then-quit=true",
            "--init.validate-genesis-assertion=false",
            "--node.sequencer=false",
            "--node.batch-poster.enable=false",
            "--node.staker.enable=false",
            "--node.feed.input.url=",
            f"--auth.jwtsecret={self.config.jwt_secret}",
        ]

        if self.config.network == Network.MAINNET:
            # Mainnet has a pre-built genesis snapshot available
            cmd.append("--init.latest=genesis")
        else:
            # No snapshot for testnets — create genesis from L1 init message.
            # parent-chain-reader defaults to enabled, so Nitro reads the real
            # init message from L1's delayed bridge instead of creating a fake
            # one via json.Marshal(chainConfig).
            cmd.append("--init.empty=true")

        return cmd

    async def run_to_completion(self, timeout: float = INIT_TIMEOUT_S) -> None:
        """Start the init process and wait for it to exit successfully.

        Args:
            timeout: Maximum seconds to wait for completion

        Raises:
            InitError: If init fails or times out
        """
        self.state.status = ProcessStatus.INITIALIZING
        await self.start()

        assert self._proc is not None

        try:
            exit_code = await asyncio.wait_for(self._proc.wait(), timeout=timeout)
        except TimeoutError:
            await self.stop()
            raise InitError(
                f"EL initialization timed out after {timeout}s. "
                f"Check L1 RPC connectivity: {self.config.l1_rpc}",
            ) from None

        self.state.exit_code = exit_code
        self._cancel_log_task()

        if exit_code != 0:
            # Grab last few error lines for diagnostics
            errors = [e.message for e in self.state.error_log][-10:]
            error_context = "\n  ".join(errors) if errors else "(no error output captured)"
            raise InitError(
                f"EL initialization failed with exit code {exit_code}.\n"
                f"Recent errors:\n  {error_context}",
                exit_code=exit_code,
            )

        self.state.status = ProcessStatus.STOPPED
