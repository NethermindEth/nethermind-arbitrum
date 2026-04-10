"""Nitro Consensus Layer process.

Runs the Nitro binary as CL, connecting to an external EL.
Mode-aware: builds different flags for comparison, nitro-nm, and nitro-nitro.
"""

from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING

from ..config import Mode
from .base import BaseProcess

if TYPE_CHECKING:
    from ..config import RunnerConfig


class NitroCLProcess(BaseProcess):
    """Nitro node as consensus layer connecting to external execution layer(s).

    Flags derived from arbitrum-nitro/Makefile:269-323 (run-cl and run-cl-comparison).
    """

    def __init__(self, config: RunnerConfig, mode: Mode) -> None:
        super().__init__(config)
        self._mode = mode

    @property
    def name(self) -> str:
        return "nitro-cl"

    @property
    def health_check_port(self) -> int:
        return self.config.ports.nitro_cl_http

    def working_directory(self) -> Path:
        return self.config.nitro_path

    def build_command(self) -> list[str]:
        nc = self.config.network_config
        data_path = self.config.data_path("nitro-cl")
        ports = self.config.ports

        cmd = [
            str(self.config.nitro_binary),
            f"--chain.id={nc.chain_id}",
            f"--parent-chain.id={nc.parent_chain_id}",
            f"--persistent.global-config={data_path}",
            # L1 connection
            f"--parent-chain.connection.url={self.config.l1_rpc}",
            f"--parent-chain.blob-client.beacon-url={self.config.l1_beacon}",
            # Init
            "--init.empty=true",
            "--init.validate-genesis-assertion=false",
            # Disable features not needed for sync-only
            "--node.sequencer=false",
            "--node.batch-poster.enable=false",
            "--node.staker.enable=false",
            "--node.feed.input.url=",
            "--execution.forwarding-target=null",
            # Block validation
            "--node.block-validator.enable=true",
            "--node.block-validator.validation-server.url=self-auth",
            "--node.block-validator.current-module-root=latest",
            "--validation.wasm.enable-wasmroots-check=false",
            f"--validation.wasm.root-path={self.config.nitro_path / 'target' / 'machines'}",
            f"--validation.jit.jit-path={self.config.nitro_path / 'target' / 'bin' / 'jit'}",
            "--validation.use-jit=true",
            # Auth
            f"--auth.jwtsecret={self.config.jwt_secret}",
            # CL RPC endpoints
            "--ws.addr=0.0.0.0",
            f"--ws.port={ports.nitro_cl_ws}",
            "--http.addr=0.0.0.0",
            f"--http.port={ports.nitro_cl_http}",
            # Log level
            f"--log-level={self.config.log_level.value}",
        ]

        # Mode-specific EL connection
        match self._mode:
            case Mode.COMPARISON:
                cmd.extend(self._comparison_flags(ports))
            case Mode.NITRO_NM:
                cmd.extend(self._nitro_nm_flags(ports))
            case Mode.NITRO_NITRO:
                cmd.extend(self._nitro_nitro_flags(ports))

        return cmd

    def _comparison_flags(self, ports: object) -> list[str]:
        """Flags for comparison mode: Nitro EL as primary, Nethermind as secondary."""
        return [
            f"--node.execution-rpc-client.url=ws://localhost:{self.config.ports.nitro_el_ws}",
            "--node.comparison-execution.enable=true",
            f"--node.comparison-execution.secondary-rpc-client.url=http://localhost:{self.config.ports.nethermind_engine}",
            f"--node.comparison-execution.secondary-rpc-client.jwtsecret={self.config.jwt_secret}",
        ]

    def _nitro_nm_flags(self, ports: object) -> list[str]:
        """Flags for nitro-nm mode: Nethermind as sole EL."""
        return [
            f"--node.execution-rpc-client.url=http://localhost:{self.config.ports.nethermind_engine}",
            f"--node.execution-rpc-client.jwtsecret={self.config.jwt_secret}",
        ]

    def _nitro_nitro_flags(self, ports: object) -> list[str]:
        """Flags for nitro-nitro mode: Nitro EL as sole EL."""
        return [
            f"--node.execution-rpc-client.url=ws://localhost:{self.config.ports.nitro_el_ws}",
        ]
