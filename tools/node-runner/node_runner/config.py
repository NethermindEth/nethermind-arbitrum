"""Configuration models for node-runner.

Defines networks, modes, and runtime configuration using frozen Pydantic models.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from pathlib import Path
from typing import Final

from pydantic import BaseModel, ConfigDict, Field


class Network(Enum):
    """Supported Arbitrum L2 networks."""

    SEPOLIA = "sepolia"
    MAINNET = "mainnet"


class Mode(Enum):
    """Node runner operating modes."""

    COMPARISON = "comparison"
    NITRO_NM = "nitro-nm"
    NITRO_NITRO = "nitro-nitro"


class LogLevel(Enum):
    """Log levels supported by both Nitro and Nethermind."""

    ERROR = "error"
    WARN = "warn"
    INFO = "info"
    DEBUG = "debug"
    TRACE = "trace"


@dataclass(frozen=True)
class NetworkConfig:
    """Network-specific constants."""

    chain_id: int
    parent_chain_id: int
    l1_rpc: str
    l1_beacon: str
    nethermind_config_name: str


NETWORKS: Final[dict[Network, NetworkConfig]] = {
    Network.SEPOLIA: NetworkConfig(
        chain_id=421614,
        parent_chain_id=11155111,
        l1_rpc="wss://sepolia.drpc.org",
        l1_beacon="https://ethereum-sepolia-beacon-api.publicnode.com",
        nethermind_config_name="arbitrum-sepolia",
    ),
    Network.MAINNET: NetworkConfig(
        chain_id=42161,
        parent_chain_id=1,
        l1_rpc="wss://ethereum.drpc.org",
        l1_beacon="https://ethereum-beacon-api.publicnode.com",
        nethermind_config_name="arbitrum-mainnet",
    ),
}


class PortConfig(BaseModel):
    """Port assignments for each component.

    These are fixed and chosen to avoid conflicts with the comparison_runner tool.
    """

    nitro_el_ws: int = 20552
    nitro_el_http: int = 8547
    nitro_el_auth: int = 8551
    nitro_cl_ws: int = 8559
    nitro_cl_http: int = 8558
    nethermind_http: int = 20545
    nethermind_engine: int = 20551

    model_config = ConfigDict(frozen=True)


DEFAULT_JWT_PATH: Final[Path] = Path.home() / ".arbitrum" / "jwt.hex"

LOG_BUFFER_SIZE: Final[int] = 10_000
"""Maximum log lines kept in memory per process."""

ERROR_BUFFER_SIZE: Final[int] = 1_000
"""Maximum error/warning lines kept per process."""

CRASH_REPORT_LINES: Final[int] = 1_000
"""Lines to save per process in crash report."""

HEALTH_CHECK_INTERVAL_S: Final[float] = 1.0
"""Seconds between health check probes."""

HEALTH_CHECK_TIMEOUT_S: Final[float] = 60.0
"""Default timeout waiting for a process to become healthy."""

INIT_TIMEOUT_S: Final[float] = 300.0
"""Timeout for init-el process (genesis download can be slow)."""

SHUTDOWN_GRACE_S: Final[float] = 10.0
"""Seconds to wait for graceful shutdown before SIGKILL."""


class RunnerConfig(BaseModel):
    """Runtime configuration for a node-runner session.

    Immutable after construction. All paths are resolved at creation time.
    """

    mode: Mode
    network: Network
    log_level: LogLevel = LogLevel.INFO
    clean: bool = False
    force_init: bool = False
    verification: int | None = None
    nitro_path: Path
    nethermind_path: Path
    data_dir: Path = Field(default_factory=lambda: Path(".data"))
    jwt_secret: Path = Field(default_factory=lambda: DEFAULT_JWT_PATH)
    l1_rpc_override: str | None = None
    l1_beacon_override: str | None = None
    ports: PortConfig = Field(default_factory=PortConfig)

    model_config = ConfigDict(frozen=True)

    @property
    def nitro_binary(self) -> Path:
        """Path to the compiled nitro binary."""
        return self.nitro_path / "target" / "bin" / "nitro"

    @property
    def nethermind_build_dir(self) -> Path:
        """Path to the Nethermind build output containing nethermind.dll."""
        return (
            self.nethermind_path
            / "src"
            / "Nethermind"
            / "src"
            / "Nethermind"
            / "artifacts"
            / "bin"
            / "Nethermind.Runner"
            / "debug"
        )

    @property
    def network_config(self) -> NetworkConfig:
        """Get the network-specific configuration."""
        return NETWORKS[self.network]

    @property
    def l1_rpc(self) -> str:
        """Effective L1 RPC URL (override or network default)."""
        return self.l1_rpc_override or self.network_config.l1_rpc

    @property
    def l1_beacon(self) -> str:
        """Effective L1 Beacon URL (override or network default)."""
        return self.l1_beacon_override or self.network_config.l1_beacon

    def data_path(self, component: str) -> Path:
        """Return data directory for a component, namespaced by network.

        Args:
            component: Component name (e.g., 'nitro-el', 'nethermind', 'nitro-cl')
        """
        return self.data_dir / self.network.value / component

    def crash_report_dir(self) -> Path:
        """Directory for crash report output."""
        return self.data_dir / "crash_reports"

    def error_log_dir(self) -> Path:
        """Directory for persistent error-only log files."""
        return self.data_dir / "errors"

    @property
    def needs_nitro_el(self) -> bool:
        """Whether this mode requires a Nitro EL process."""
        return self.mode in (Mode.COMPARISON, Mode.NITRO_NITRO)

    @property
    def needs_nethermind(self) -> bool:
        """Whether this mode requires a Nethermind process."""
        return self.mode in (Mode.COMPARISON, Mode.NITRO_NM)
