"""Nethermind Execution Layer process.

Runs Nethermind via `dotnet nethermind.dll` with Arbitrum configuration.
"""

from __future__ import annotations

from pathlib import Path
from typing import TYPE_CHECKING

from .base import BaseProcess

if TYPE_CHECKING:
    from ..config import RunnerConfig


class NethermindProcess(BaseProcess):
    """Nethermind node as Arbitrum execution layer.

    Command pattern derived from nethermind-arbitrum/Makefile:23-27 (run-nethermind macro).
    Uses network-specific config (arbitrum-sepolia, arbitrum-mainnet).
    """

    def __init__(self, config: RunnerConfig) -> None:
        super().__init__(config)

    @property
    def name(self) -> str:
        return "nethermind"

    @property
    def health_check_port(self) -> int:
        return self.config.ports.nethermind_engine

    def working_directory(self) -> Path:
        return self.config.nethermind_build_dir

    def build_command(self) -> list[str]:
        nc = self.config.network_config
        data_path = self.config.data_path("nethermind")

        cmd = [
            "dotnet",
            "nethermind.dll",
            "-c",
            nc.nethermind_config_name,
            "--data-dir",
            str(data_path),
            f"--JsonRpc.JwtSecretFile={self.config.jwt_secret}",
            f"--JsonRpc.Port={self.config.ports.nethermind_http}",
            f"--JsonRpc.EnginePort={self.config.ports.nethermind_engine}",
            "--JsonRpc.Host=0.0.0.0",
            "--JsonRpc.EngineHost=0.0.0.0",
            f"--log={self.config.log_level.value}",
        ]

        if self.config.verification is not None:
            cmd.append("--VerifyBlockHash.Enabled=true")
            if self.config.verification > 0:
                cmd.append(
                    f"--VerifyBlockHash.VerifyEveryNBlocks={self.config.verification}"
                )

        return cmd
