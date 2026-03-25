# node-runner

TUI tool for running Arbitrum Nitro + Nethermind nodes in multiple modes. Orchestrates process startup, streams logs in real time, and handles graceful shutdown — all from a single terminal.

```
┌─ COMPARISON | Sepolia | nitro-el:healthy | nethermind:healthy | nitro-cl:healthy ─┐
│                                                                                    │
│  ┌── nitro-el ── healthy ──────────┐  ┌── nethermind ── healthy ────────────────┐  │
│  │ INFO  Block 12345 validated     │  │ INFO  Processed block 12345             │  │
│  │ INFO  State root: 0xabc...      │  │ INFO  State root: 0xabc...             │  │
│  │                                 │  │                                         │  │
│  └─────────────────────────────────┘  └─────────────────────────────────────────┘  │
│  ┌── nitro-cl ── healthy ──────────────────────────────────────────────────────┐   │
│  │ INFO  Feeding block 12346 to execution engines                             │   │
│  └────────────────────────────────────────────────────────────────────────────┘    │
│                                                                                    │
│  q Quit  x Stop nodes  s Split view  c Combined view  f Errors only  y Copy log   │
└────────────────────────────────────────────────────────────────────────────────────┘
```

## Modes

| Mode | Processes | Purpose |
|------|-----------|---------|
| `comparison` | Nitro CL + Nitro EL + Nethermind EL | State root comparison between ELs |
| `nitro-nm` | Nitro CL + Nethermind EL | Production-like Nethermind setup |
| `nitro-nitro` | Nitro CL + Nitro EL | Reference baseline |

All modes use Nitro as the consensus layer (CL). The difference is which execution layer (EL) backends are started.

## Prerequisites

- **Python 3.12+**
- **uv** (recommended) or pip
- **Nitro** built locally (`make build` in the Nitro repo)
- **Nethermind** built locally (`make build` in the Nethermind repo) — required for `comparison` and `nitro-nm` modes
- A shared **JWT secret** at `~/.arbitrum/jwt.hex`

## Installation

```bash
cd tools/node-runner
uv sync          # installs dependencies into .venv
```

Or with pip:

```bash
pip install -e ".[dev]"
```

## Quick Start

Set repository paths (or pass them as flags):

```bash
export NITRO_PATH=/path/to/arbitrum-nitro
export NETHERMIND_PATH=/path/to/nethermind-arbitrum
```

A `.env` file in any parent directory is also picked up automatically.

Run in comparison mode on Sepolia:

```bash
uv run node-runner comparison --network sepolia
```

Run Nethermind as the sole EL:

```bash
uv run node-runner nitro-nm --network mainnet
```

## Usage

```
node-runner <mode> [options]
```

### Options

| Flag | Description | Default |
|------|-------------|---------|
| `--network {sepolia,mainnet}` | Arbitrum network | `sepolia` |
| `--log-level {error,warn,info,debug,trace}` | Log verbosity for all processes | `info` |
| `--clean` | Wipe databases before starting | off |
| `--init` | Force Nitro EL re-initialization (genesis download) | auto-detected |
| `--verification [N]` | Enable Nethermind `VerifyBlockHash` (`nitro-nm` only). Optionally verify every N blocks. | off |
| `--nitro-path DIR` | Path to Nitro repo | `$NITRO_PATH` |
| `--nethermind-path DIR` | Path to Nethermind repo | `$NETHERMIND_PATH` |
| `--data-dir DIR` | Base data directory | `.data` |
| `--l1-rpc URL` | Override L1 RPC endpoint | per-network default |
| `--l1-beacon URL` | Override L1 Beacon endpoint | per-network default |

### Verification Mode

The `--verification` flag enables Nethermind's `VerifyBlockHash` feature, which cross-checks computed block hashes against a public Arbitrum RPC endpoint. This runs entirely within Nethermind — no additional processes are started.

```bash
# Enable with Nethermind's default interval (every 10,000 blocks)
uv run node-runner nitro-nm --verification

# Verify every 100 blocks
uv run node-runner nitro-nm --verification 100
```

### Examples

```bash
# Comparison mode, Sepolia, debug logging
uv run node-runner comparison --network sepolia --log-level debug

# Nethermind-only with verification, mainnet
uv run node-runner nitro-nm --network mainnet --verification

# Reference baseline, clean start
uv run node-runner nitro-nitro --clean --init

# Custom L1 RPC
uv run node-runner comparison --l1-rpc wss://my-rpc.example.com
```

## TUI Keybindings

| Key | Action |
|-----|--------|
| `q` | Quit (stops all processes, writes reports, exits) |
| `x` | Stop nodes but keep TUI open for log review |
| `s` | Split view (one pane per process) |
| `c` | Combined view (interleaved logs from all processes) |
| `f` | Toggle errors-only filter |
| `y` | Copy log content to system clipboard |
| `1`/`2`/`3` | Focus a specific pane |
| `Shift+click` | Native text selection (bypasses TUI mouse capture) |

## Architecture

```
node_runner/
├── cli.py              # Argument parsing, config construction
├── config.py           # Networks, modes, ports, RunnerConfig model
├── orchestrator.py     # Process lifecycle: init → start → monitor → stop
├── crash_report.py     # Shutdown reports (crash dump + error-only logs)
├── models.py           # ProcessState, LogEntry, ring buffers
├── exceptions.py       # Typed errors (StartupError, HealthCheckError, etc.)
├── processes/
│   ├── base.py         # BaseProcess: start, stop, health check, log parsing
│   ├── init_el.py      # One-shot Nitro EL genesis initialization
│   ├── nethermind.py   # Nethermind EL (dotnet nethermind.dll)
│   ├── nitro_cl.py     # Nitro CL with per-mode EL connection flags
│   └── nitro_el.py     # Nitro EL (geth-based)
└── tui/
    ├── app.py          # Textual application, keybindings, log streaming
    ├── widgets.py      # ProcessLogPane, CombinedLogPane
    └── styles.tcss     # Terminal CSS for layout and colors
```

### Startup Sequence

1. **Clean** (if `--clean`): wipe network data directory
2. **Init** (if needed): run `nitro --init.then-quit` to bootstrap EL genesis from L1
3. **Start processes** in dependency order — each must pass a TCP health check before the next starts
4. **Stream logs** from per-process ring buffers to TUI panes at 10 Hz
5. **Monitor** for crashes and update status bar

### Shutdown

On `q` or `x`, processes are stopped in reverse startup order (CL first, then ELs). Each process gets a SIGTERM with a 10-second grace period before SIGKILL. Crash reports and error logs are written to `.data/crash_reports/` and `.data/errors/`.

## Data Directory Layout

```
.data/
└── sepolia/
    ├── nitro-el/       # Nitro EL chain data
    ├── nethermind/     # Nethermind chain data
    └── nitro-cl/       # Nitro CL data
.data/
├── crash_reports/      # Timestamped shutdown reports
└── errors/             # Error-only log files per process
```

## Development

```bash
# Run tests
uv run pytest

# Lint
uv run ruff check node_runner/ tests/

# Type check
uv run mypy node_runner/
```

## Port Assignments

Ports are fixed and chosen to avoid conflicts with the `comparison_runner` tool:

| Component | Port | Protocol |
|-----------|------|----------|
| Nitro EL WS | 20552 | WebSocket |
| Nitro EL HTTP | 8547 | JSON-RPC |
| Nitro EL Auth | 8551 | Engine API |
| Nitro CL WS | 8559 | WebSocket |
| Nitro CL HTTP | 8558 | JSON-RPC |
| Nethermind HTTP | 20545 | JSON-RPC |
| Nethermind Engine | 20551 | Engine API |
