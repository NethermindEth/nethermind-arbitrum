"""Nethermind Execution Layer process.

Runs Nethermind via `dotnet nethermind.dll` with Arbitrum configuration.
"""

from __future__ import annotations

import asyncio
import json
import logging
from pathlib import Path
from typing import TYPE_CHECKING

from ..config import HEALTH_CHECK_INTERVAL_S
from .base import BaseProcess

if TYPE_CHECKING:
    from ..config import RunnerConfig

logger = logging.getLogger(__name__)


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
        return self.config.ports.nethermind_http

    async def wait_for_healthy(self, timeout: float = 60.0) -> bool:
        """Wait until Nethermind responds to a JSON-RPC call.

        A TCP connection to the engine port succeeds before Nethermind has
        finished initializing its state. We probe the HTTP RPC port with
        net_version to confirm the node is actually ready.
        """
        port = self.config.ports.nethermind_http
        url = f"http://127.0.0.1:{port}"
        payload = json.dumps({"jsonrpc": "2.0", "method": "net_version", "params": [], "id": 1}).encode()
        headers = b"POST / HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Type: application/json\r\nContent-Length: " + str(len(payload)).encode() + b"\r\nConnection: close\r\n\r\n"

        loop = asyncio.get_running_loop()
        deadline = loop.time() + timeout
        while loop.time() < deadline:
            if self._proc is not None and self._proc.returncode is not None:
                from ..models import ProcessStatus
                self.state.status = ProcessStatus.CRASHED
                self.state.exit_code = self._proc.returncode
                return False

            try:
                reader, writer = await asyncio.wait_for(
                    asyncio.open_connection("127.0.0.1", port), timeout=1.0
                )
                writer.write(headers + payload)
                await writer.drain()
                response = await asyncio.wait_for(reader.read(4096), timeout=2.0)
                writer.close()
                await writer.wait_closed()
                if b'"result"' in response:
                    from ..models import ProcessStatus
                    self.state.status = ProcessStatus.HEALTHY
                    logger.info("Nethermind RPC is ready on port %d", port)
                    return True
            except (OSError, TimeoutError, asyncio.TimeoutError):
                pass

            await asyncio.sleep(HEALTH_CHECK_INTERVAL_S)

        from ..models import ProcessStatus
        self.state.status = ProcessStatus.UNHEALTHY
        return False

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
