"""Command-line interface for node-runner."""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

from dotenv import find_dotenv, load_dotenv

from .config import LogLevel, Mode, Network, RunnerConfig
from .exceptions import ConfigError

# Load .env from the nearest parent directory (walks up from cwd)
load_dotenv(find_dotenv(usecwd=True))


def create_parser() -> argparse.ArgumentParser:
    """Create the argument parser."""
    parser = argparse.ArgumentParser(
        prog="node-runner",
        description="Run Arbitrum Nitro + Nethermind nodes with TUI monitoring",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Modes:
  comparison   Nitro CL + Nitro EL + Nethermind EL (state root comparison)
  nitro-nm     Nitro CL + Nethermind EL (production-like)
  nitro-nitro  Nitro CL + Nitro EL (reference baseline)

Examples:
  node-runner comparison --network sepolia
  node-runner nitro-nm --network mainnet --log-level debug
  node-runner nitro-nitro --clean --init
  node-runner comparison --l1-rpc wss://my-rpc.example.com

Environment Variables:
  NITRO_PATH        Path to Nitro repository (required if --nitro-path not set)
  NETHERMIND_PATH   Path to Nethermind repository (required if --nethermind-path not set)
""",
    )

    parser.add_argument(
        "mode",
        choices=["comparison", "nitro-nm", "nitro-nitro"],
        help="Operating mode",
    )

    parser.add_argument(
        "--network",
        choices=["sepolia", "mainnet"],
        default="sepolia",
        help="Network to sync (default: sepolia)",
    )

    parser.add_argument(
        "--log-level",
        choices=["error", "warn", "info", "debug", "trace"],
        default="info",
        help="Log level for all processes (default: info)",
    )

    parser.add_argument(
        "--clean",
        action="store_true",
        help="Clean databases before starting",
    )

    parser.add_argument(
        "--init",
        action="store_true",
        dest="force_init",
        help="Force re-initialization of Nitro EL (download genesis from L1)",
    )

    parser.add_argument(
        "--nitro-path",
        type=Path,
        default=None,
        metavar="DIR",
        help="Path to Nitro repository (default: $NITRO_PATH)",
    )

    parser.add_argument(
        "--nethermind-path",
        type=Path,
        default=None,
        metavar="DIR",
        help="Path to Nethermind repository (default: $NETHERMIND_PATH)",
    )

    parser.add_argument(
        "--data-dir",
        type=Path,
        default=Path(".data"),
        metavar="DIR",
        help="Base data directory (default: .data)",
    )

    parser.add_argument(
        "--verification",
        nargs="?",
        const=0,
        default=None,
        type=int,
        metavar="N",
        help="Enable Nethermind VerifyBlockHash; optionally verify every N blocks (nitro-nm only)",
    )

    parser.add_argument(
        "--l1-rpc",
        default=None,
        metavar="URL",
        help="Override L1 RPC URL",
    )

    parser.add_argument(
        "--l1-beacon",
        default=None,
        metavar="URL",
        help="Override L1 Beacon URL",
    )

    return parser


def _resolve_path(
    arg_value: Path | None,
    env_var: str,
    label: str,
) -> Path:
    """Resolve a path from CLI arg or environment variable.

    Args:
        arg_value: Value from argparse (may be None)
        env_var: Environment variable name to fall back to
        label: Human-readable label for error messages

    Returns:
        Resolved absolute path

    Raises:
        ConfigError: If path not provided or doesn't exist
    """
    if arg_value is not None:
        path = arg_value.expanduser().resolve()
    else:
        env_path = os.environ.get(env_var)
        if not env_path:
            raise ConfigError(
                f"{label} not specified. Use --{label.lower().replace(' ', '-')} "
                f"or set {env_var} environment variable."
            )
        path = Path(env_path).expanduser().resolve()

    if not path.is_dir():
        raise ConfigError(f"{label} does not exist: {path}")

    return path


def build_config(args: argparse.Namespace) -> RunnerConfig:
    """Build RunnerConfig from parsed CLI arguments.

    Raises:
        ConfigError: If configuration is invalid
    """
    nitro_path = _resolve_path(args.nitro_path, "NITRO_PATH", "Nitro path")
    nethermind_path = _resolve_path(args.nethermind_path, "NETHERMIND_PATH", "Nethermind path")

    # Load Nitro's .env for L1 RPC URLs (won't override already-set vars)
    nitro_env = nitro_path / ".env"
    if nitro_env.is_file():
        load_dotenv(nitro_env, override=False)

    mode = Mode(args.mode)

    if args.verification is not None and mode != Mode.NITRO_NM:
        raise ConfigError("--verification is only supported with nitro-nm mode")

    # All modes run Nitro CL, so the nitro binary is always required
    nitro_binary = nitro_path / "target" / "bin" / "nitro"
    if not nitro_binary.exists():
        raise ConfigError(
            f"Nitro binary not found at {nitro_binary}. "
            f"Build it with: cd {nitro_path} && make build"
        )

    nethermind_dll = (
        nethermind_path
        / "src"
        / "Nethermind"
        / "src"
        / "Nethermind"
        / "artifacts"
        / "bin"
        / "Nethermind.Runner"
        / "debug"
        / "nethermind.dll"
    )
    if mode in (Mode.COMPARISON, Mode.NITRO_NM) and not nethermind_dll.exists():
        raise ConfigError(
            f"Nethermind build not found at {nethermind_dll.parent}. "
            f"Build it with: cd {nethermind_path} && make build"
        )

    # Resolve L1 URLs: CLI flag > Nitro .env > hardcoded default
    network = Network(args.network)
    network_prefix = network.value.upper()
    l1_rpc = args.l1_rpc or os.environ.get(f"{network_prefix}_L1_RPC")
    l1_beacon = args.l1_beacon or os.environ.get(f"{network_prefix}_L1_BEACON")

    return RunnerConfig(
        mode=mode,
        network=network,
        log_level=LogLevel(args.log_level),
        clean=args.clean,
        force_init=args.force_init,
        nitro_path=nitro_path,
        nethermind_path=nethermind_path,
        data_dir=args.data_dir.resolve(),
        verification=args.verification,
        l1_rpc_override=l1_rpc,
        l1_beacon_override=l1_beacon,
    )


def parse_args(argv: list[str] | None = None) -> RunnerConfig:
    """Parse CLI arguments and build configuration.

    Args:
        argv: Arguments to parse (defaults to sys.argv[1:])

    Returns:
        Validated RunnerConfig
    """
    parser = create_parser()
    args = parser.parse_args(argv)

    try:
        return build_config(args)
    except ConfigError as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)
