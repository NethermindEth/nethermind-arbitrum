"""Entry point for node-runner.

Usage:
    python -m node_runner comparison --network sepolia
    python -m node_runner nitro-nm --network mainnet --log-level debug
    python -m node_runner nitro-nitro --clean
"""

from __future__ import annotations


def main() -> None:
    """Parse arguments, build configuration, and launch the TUI."""
    from .cli import parse_args
    from .tui.app import NodeRunnerApp

    config = parse_args()
    app = NodeRunnerApp(config)
    app.run()


if __name__ == "__main__":
    main()
