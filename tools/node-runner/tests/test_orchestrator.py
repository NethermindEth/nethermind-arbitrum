"""Tests for the Orchestrator process graph and lifecycle."""

from __future__ import annotations

from pathlib import Path

import pytest

from node_runner.config import Mode, Network, RunnerConfig
from node_runner.orchestrator import Orchestrator
from node_runner.processes.nethermind import NethermindProcess
from node_runner.processes.nitro_cl import NitroCLProcess
from node_runner.processes.nitro_el import NitroELProcess

VERIFY_FLAG = "--VerifyBlockHash.Enabled=true"


@pytest.fixture()
def make_config(tmp_path: Path):
    """Factory for RunnerConfig with a given mode."""

    def _make(
        mode: Mode,
        *,
        force_init: bool = False,
        network: Network = Network.SEPOLIA,
        verification: int | None = None,
    ) -> RunnerConfig:
        nitro = tmp_path / "nitro"
        nitro.mkdir(exist_ok=True)
        nm = tmp_path / "nm"
        nm.mkdir(exist_ok=True)
        return RunnerConfig(
            mode=mode,
            network=network,
            nitro_path=nitro,
            nethermind_path=nm,
            data_dir=tmp_path / "data",
            force_init=force_init,
            verification=verification,
        )

    return _make


class TestProcessGraph:
    """Verify the correct process list is built per mode."""

    def test_comparison_mode_three_processes(self, make_config) -> None:
        config = make_config(Mode.COMPARISON)
        orch = Orchestrator(config)
        procs = orch._build_process_list()
        assert len(procs) == 3
        assert isinstance(procs[0], NitroELProcess)
        assert isinstance(procs[1], NethermindProcess)
        assert isinstance(procs[2], NitroCLProcess)

    def test_nitro_nm_mode_two_processes(self, make_config) -> None:
        config = make_config(Mode.NITRO_NM)
        orch = Orchestrator(config)
        procs = orch._build_process_list()
        assert len(procs) == 2
        assert isinstance(procs[0], NethermindProcess)
        assert isinstance(procs[1], NitroCLProcess)

    def test_nitro_nm_verification_same_process_count(self, make_config) -> None:
        """Verification doesn't change the process graph (it's a Nethermind-side flag)."""
        config = make_config(Mode.NITRO_NM, verification=0)
        orch = Orchestrator(config)
        procs = orch._build_process_list()
        assert len(procs) == 2
        assert isinstance(procs[0], NethermindProcess)
        assert isinstance(procs[1], NitroCLProcess)

    def test_nitro_nitro_mode_two_processes(self, make_config) -> None:
        config = make_config(Mode.NITRO_NITRO)
        orch = Orchestrator(config)
        procs = orch._build_process_list()
        assert len(procs) == 2
        assert isinstance(procs[0], NitroELProcess)
        assert isinstance(procs[1], NitroCLProcess)

    def test_comparison_startup_order(self, make_config) -> None:
        """ELs must start before CL (dependency chain)."""
        config = make_config(Mode.COMPARISON)
        orch = Orchestrator(config)
        procs = orch._build_process_list()
        names = [p.name for p in procs]
        # CL must be last
        assert names[-1] == "nitro-cl"
        # Both ELs before CL
        assert "nitro-el" in names[:2]
        assert "nethermind" in names[:2]

    def test_process_names_are_unique(self, make_config) -> None:
        for mode in Mode:
            config = make_config(mode)
            orch = Orchestrator(config)
            procs = orch._build_process_list()
            names = [p.name for p in procs]
            assert len(names) == len(set(names)), f"Duplicate names in {mode}"


class TestNeedsInit:
    def test_not_needed_for_nitro_nm(self, make_config) -> None:
        config = make_config(Mode.NITRO_NM, network=Network.MAINNET)
        orch = Orchestrator(config)
        assert not orch._needs_init()

    def test_needed_for_sepolia_when_data_missing(self, make_config) -> None:
        """Sepolia also needs init when data dir doesn't exist."""
        config = make_config(Mode.COMPARISON, network=Network.SEPOLIA)
        orch = Orchestrator(config)
        assert orch._needs_init()

    def test_needed_when_force_init(self, make_config) -> None:
        config = make_config(Mode.COMPARISON, force_init=True, network=Network.MAINNET)
        orch = Orchestrator(config)
        assert orch._needs_init()

    def test_needed_when_data_dir_missing(self, make_config) -> None:
        config = make_config(Mode.COMPARISON, network=Network.MAINNET)
        orch = Orchestrator(config)
        # data_path("nitro-el") doesn't exist → needs init
        assert orch._needs_init()

    def test_needed_when_data_dir_empty(self, make_config) -> None:
        config = make_config(Mode.COMPARISON, network=Network.MAINNET)
        # Create empty data dir
        data_path = config.data_path("nitro-el")
        data_path.mkdir(parents=True)
        orch = Orchestrator(config)
        assert orch._needs_init()

    def test_not_needed_when_data_exists(self, make_config) -> None:
        config = make_config(Mode.COMPARISON, network=Network.MAINNET)
        data_path = config.data_path("nitro-el")
        data_path.mkdir(parents=True)
        # Create a file to indicate init was done
        (data_path / "CURRENT").touch()
        orch = Orchestrator(config)
        assert not orch._needs_init()


class TestShutdown:
    def test_request_shutdown_sets_event(self, make_config) -> None:
        config = make_config(Mode.COMPARISON)
        orch = Orchestrator(config)
        assert not orch.is_shutdown_requested
        orch.request_shutdown()
        assert orch.is_shutdown_requested

    def test_processes_empty_before_start(self, make_config) -> None:
        config = make_config(Mode.COMPARISON)
        orch = Orchestrator(config)
        assert orch.processes == []

    def test_summary_empty_before_start(self, make_config) -> None:
        config = make_config(Mode.COMPARISON)
        orch = Orchestrator(config)
        assert orch.summary() == {}


class TestVerifyBlockHash:
    """Verification adds --VerifyBlockHash.Enabled=true to Nethermind, nothing else."""

    def test_nethermind_has_flag_when_enabled(self, make_config) -> None:
        config = make_config(Mode.NITRO_NM, verification=0)
        proc = NethermindProcess(config)
        cmd = proc.build_command()
        assert VERIFY_FLAG in cmd

    def test_nethermind_no_flag_by_default(self, make_config) -> None:
        config = make_config(Mode.NITRO_NM)
        proc = NethermindProcess(config)
        cmd = proc.build_command()
        assert VERIFY_FLAG not in cmd

    def test_every_n_blocks_passed_when_set(self, make_config) -> None:
        config = make_config(Mode.NITRO_NM, verification=500)
        proc = NethermindProcess(config)
        cmd = proc.build_command()
        assert VERIFY_FLAG in cmd
        assert "--VerifyBlockHash.VerifyEveryNBlocks=500" in cmd

    def test_every_n_blocks_omitted_when_zero(self, make_config) -> None:
        """Zero means use Nethermind's default — don't pass the interval flag."""
        config = make_config(Mode.NITRO_NM, verification=0)
        proc = NethermindProcess(config)
        cmd = proc.build_command()
        assert not any(a.startswith("--VerifyBlockHash.VerifyEveryNBlocks") for a in cmd)

    def test_cl_unchanged_with_verification(self, make_config) -> None:
        """CL command must be identical with and without verification."""
        base = make_config(Mode.NITRO_NM)
        with_verify = make_config(Mode.NITRO_NM, verification=0)

        base_cmd = NitroCLProcess(base, Mode.NITRO_NM).build_command()
        verify_cmd = NitroCLProcess(with_verify, Mode.NITRO_NM).build_command()
        assert base_cmd == verify_cmd
