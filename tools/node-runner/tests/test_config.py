"""Tests for configuration models and CLI argument parsing."""

from __future__ import annotations

from pathlib import Path

import pytest
from pydantic import ValidationError

from node_runner.config import (
    DEFAULT_JWT_PATH,
    NETWORKS,
    LogLevel,
    Mode,
    Network,
    PortConfig,
    RunnerConfig,
)


class TestEnums:
    def test_network_values(self) -> None:
        assert Network.SEPOLIA.value == "sepolia"
        assert Network.MAINNET.value == "mainnet"

    def test_mode_values(self) -> None:
        assert Mode.COMPARISON.value == "comparison"
        assert Mode.NITRO_NM.value == "nitro-nm"
        assert Mode.NITRO_NITRO.value == "nitro-nitro"

    def test_log_level_values(self) -> None:
        assert LogLevel.ERROR.value == "error"
        assert LogLevel.WARN.value == "warn"
        assert LogLevel.INFO.value == "info"
        assert LogLevel.DEBUG.value == "debug"
        assert LogLevel.TRACE.value == "trace"


class TestNetworkConfig:
    def test_sepolia_config(self) -> None:
        cfg = NETWORKS[Network.SEPOLIA]
        assert cfg.chain_id == 421614
        assert cfg.parent_chain_id == 11155111
        assert cfg.nethermind_config_name == "arbitrum-sepolia"

    def test_mainnet_config(self) -> None:
        cfg = NETWORKS[Network.MAINNET]
        assert cfg.chain_id == 42161
        assert cfg.parent_chain_id == 1
        assert cfg.nethermind_config_name == "arbitrum-mainnet"

    def test_all_networks_have_configs(self) -> None:
        for network in Network:
            assert network in NETWORKS

    def test_frozen(self) -> None:
        cfg = NETWORKS[Network.SEPOLIA]
        with pytest.raises(AttributeError):
            cfg.chain_id = 999  # type: ignore[misc]


class TestPortConfig:
    def test_defaults(self) -> None:
        ports = PortConfig()
        assert ports.nitro_el_ws == 20552
        assert ports.nitro_el_http == 8547
        assert ports.nitro_el_auth == 8551
        assert ports.nitro_cl_ws == 8559
        assert ports.nitro_cl_http == 8558
        assert ports.nethermind_http == 20545
        assert ports.nethermind_engine == 20551

    def test_frozen(self) -> None:
        ports = PortConfig()
        with pytest.raises(ValidationError):
            ports.nitro_el_ws = 9999  # type: ignore[misc]

    def test_no_port_conflicts(self) -> None:
        """All default ports must be unique to avoid bind conflicts."""
        ports = PortConfig()
        values = [
            ports.nitro_el_ws,
            ports.nitro_el_http,
            ports.nitro_el_auth,
            ports.nitro_cl_ws,
            ports.nitro_cl_http,
            ports.nethermind_http,
            ports.nethermind_engine,
        ]
        assert len(values) == len(set(values)), "Port conflict detected in defaults"


class TestRunnerConfig:
    @pytest.fixture()
    def config(self, tmp_path: Path) -> RunnerConfig:
        """Create a minimal valid RunnerConfig for testing."""
        nitro = tmp_path / "nitro"
        nitro.mkdir()
        nm = tmp_path / "nethermind"
        nm.mkdir()
        return RunnerConfig(
            mode=Mode.COMPARISON,
            network=Network.SEPOLIA,
            nitro_path=nitro,
            nethermind_path=nm,
            data_dir=tmp_path / "data",
        )

    def test_nitro_binary_path(self, config: RunnerConfig) -> None:
        assert config.nitro_binary == config.nitro_path / "target" / "bin" / "nitro"

    def test_nethermind_build_dir(self, config: RunnerConfig) -> None:
        expected = (
            config.nethermind_path
            / "src" / "Nethermind" / "src" / "Nethermind"
            / "artifacts" / "bin" / "Nethermind.Runner" / "debug"
        )
        assert config.nethermind_build_dir == expected

    def test_network_config_property(self, config: RunnerConfig) -> None:
        assert config.network_config == NETWORKS[Network.SEPOLIA]
        assert config.network_config.chain_id == 421614

    def test_l1_rpc_default(self, config: RunnerConfig) -> None:
        assert config.l1_rpc == NETWORKS[Network.SEPOLIA].l1_rpc

    def test_l1_rpc_override(self, tmp_path: Path) -> None:
        nitro = tmp_path / "nitro"
        nitro.mkdir()
        nm = tmp_path / "nm"
        nm.mkdir()
        cfg = RunnerConfig(
            mode=Mode.COMPARISON,
            network=Network.SEPOLIA,
            nitro_path=nitro,
            nethermind_path=nm,
            l1_rpc_override="wss://custom.rpc",
        )
        assert cfg.l1_rpc == "wss://custom.rpc"

    def test_l1_beacon_override(self, tmp_path: Path) -> None:
        nitro = tmp_path / "nitro"
        nitro.mkdir()
        nm = tmp_path / "nm"
        nm.mkdir()
        cfg = RunnerConfig(
            mode=Mode.COMPARISON,
            network=Network.SEPOLIA,
            nitro_path=nitro,
            nethermind_path=nm,
            l1_beacon_override="https://custom.beacon",
        )
        assert cfg.l1_beacon == "https://custom.beacon"

    def test_data_path_namespaced(self, config: RunnerConfig) -> None:
        path = config.data_path("nitro-el")
        assert path == config.data_dir / "sepolia" / "nitro-el"

    def test_crash_report_dir(self, config: RunnerConfig) -> None:
        assert config.crash_report_dir() == config.data_dir / "crash_reports"

    def test_error_log_dir(self, config: RunnerConfig) -> None:
        assert config.error_log_dir() == config.data_dir / "errors"

    @pytest.mark.parametrize(
        ("mode", "needs_el", "needs_nm"),
        [
            (Mode.COMPARISON, True, True),
            (Mode.NITRO_NM, False, True),
            (Mode.NITRO_NITRO, True, False),
        ],
    )
    def test_needs_process_flags(
        self,
        tmp_path: Path,
        mode: Mode,
        needs_el: bool,
        needs_nm: bool,
    ) -> None:
        nitro = tmp_path / "nitro"
        nitro.mkdir()
        nm = tmp_path / "nm"
        nm.mkdir()
        cfg = RunnerConfig(
            mode=mode,
            network=Network.SEPOLIA,
            nitro_path=nitro,
            nethermind_path=nm,
        )
        assert cfg.needs_nitro_el is needs_el
        assert cfg.needs_nethermind is needs_nm

    def test_frozen(self, config: RunnerConfig) -> None:
        with pytest.raises(ValidationError):
            config.mode = Mode.NITRO_NM  # type: ignore[misc]

    def test_default_jwt_path(self, config: RunnerConfig) -> None:
        assert config.jwt_secret == DEFAULT_JWT_PATH
