"""Nitro Execution Layer process (execution-only mode).

Runs the Nitro binary as a standalone EL without L1 listener.
Exposes RPC on WS and HTTP for CL to connect to.
"""

from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING

from .base import BaseProcess

if TYPE_CHECKING:
    from ..config import RunnerConfig


class NitroELProcess(BaseProcess):
    """Nitro node in execution-only mode.

    Flags derived from arbitrum-nitro/Makefile:240-267 (run-el target).
    """

    def __init__(self, config: RunnerConfig) -> None:
        super().__init__(config)

    @property
    def name(self) -> str:
        return "nitro-el"

    @property
    def health_check_port(self) -> int:
        return self.config.ports.nitro_el_ws

    def working_directory(self) -> Path:
        return self.config.nitro_path

    def build_command(self) -> list[str]:
        nc = self.config.network_config
        data_path = self.config.data_path("nitro-el")
        ports = self.config.ports

        return [
            str(self.config.nitro_binary),
            f"--chain.id={nc.chain_id}",
            f"--parent-chain.id={nc.parent_chain_id}",
            f"--persistent.global-config={data_path}",
            "--init.empty=true",
            "--init.validate-genesis-assertion=false",
            # Execution-only: no L1 interaction
            "--node.dangerous.no-l1-listener=true",
            "--node.parent-chain-reader.enable=false",
            "--node.sequencer=false",
            "--node.batch-poster.enable=false",
            "--node.staker.enable=false",
            "--node.feed.input.url=",
            # RPC server for CL communication
            "--execution.rpc-server.enable=true",
            "--execution.rpc-server.authenticated=false",
            "--execution.rpc-server.public=true",
            # Auth RPC
            f"--auth.jwtsecret={self.config.jwt_secret}",
            "--auth.addr=0.0.0.0",
            f"--auth.port={ports.nitro_el_auth}",
            # WebSocket
            "--ws.addr=0.0.0.0",
            f"--ws.port={ports.nitro_el_ws}",
            "--ws.api=net,web3,eth,arb,nitroexecution",
            # HTTP
            "--http.addr=0.0.0.0",
            f"--http.port={ports.nitro_el_http}",
            "--http.api=net,web3,eth,arb,nitroexecution",
            # Log level
            f"--log-level={self.config.log_level.value}",
        ]
