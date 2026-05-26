# Configuration

Reference for every Nethermind Arbitrum configuration option. For higher-level walkthroughs, see the [role pages](roles/) and [quickstart](quickstart.md).

## Configuration layers

Nethermind reads settings from three sources, in increasing priority:

```
JSON config file  (lowest)
  → Environment variables
    → Command-line arguments  (highest)
```

You can define a base JSON config and override individual settings via CLI flags or env vars without editing files.

Two files are always in play together:

- **Config file** (`-c <name>`) — runtime categories: `Init`, `JsonRpc`, `Sync`, `Pruning`, `Arbitrum`, etc.
- **Chainspec file** (referenced by `Init.ChainSpecPath` in the config) — chain identity: network ID, genesis block, engine parameters, EIP transitions.

The config file says *how to run*; the chainspec says *what chain to run on*.

### Config file resolution

`-c arbitrum-local` resolves as:

1. Take the value of `-c` (or `NETHERMIND_CONFIG` env var, or default `mainnet`).
2. If no directory path, look in `--configs-dir` (default: `configs/` in the app directory).
3. If no extension, try `.json`.
4. Load and parse.

Source: `Nethermind.Runner/Program.cs` `CreateConfigProvider`.

File locations:

- Source: `src/Nethermind.Arbitrum/Properties/configs/`
- Build output: `artifacts/bin/Nethermind.Runner/<Configuration>/configs/`
- Chainspecs: `src/Nethermind.Arbitrum/Properties/chainspec/` → `configs/chainspec/` in build output

### Naming conventions across sources

The same setting is named differently in each source. For `IWasmDbConfig.MaxOpenFiles`:

| Source | Form | Example |
|--------|------|---------|
| JSON file | Concrete class name (with optional `Config` suffix) | `"WasmDb"` or `"WasmDbConfig"` |
| Env var | Uppercased class name, prefixed `NETHERMIND_`, name appended with `_` | `NETHERMIND_WASMDBCONFIG_MAXOPENFILES` |
| CLI flag | Interface name minus leading `I` and trailing `Config` | `--WasmDb.MaxOpenFiles` or `--wasmdb-maxopenfiles` |

Two valid CLI forms per option: dotted case-preserving `--Category.Name` and lowercase dashed `--category-name`.

Known defect: CLI flags for `IWasmDbConfig` do not register because the interface inherits all properties from `IRocksDbConfig` and declares none of its own. `Type.GetProperties()` on an interface does not return inherited members. Use JSON or env var for those settings.

---

<a id="arbitrum"></a>
## `Arbitrum.*` settings

Defined in `Config/IArbitrumConfig.cs` / `ArbitrumConfig.cs`. Grouped by what they control.

### Block-tag and finality

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| <a id="safe-block-wait-for-validator"></a>`SafeBlockWaitForValidator` | bool | `false` | Whether the `safe` block tag waits for validator confirmation. |
| <a id="finalized-block-wait-for-validator"></a>`FinalizedBlockWaitForValidator` | bool | `false` | Whether the `finalized` block tag waits for validator confirmation. |
| <a id="message-lag-ms"></a>`MessageLagMs` | int (ms) | `1000` | How far behind the consensus node a node can be while still reporting "in sync". |

### Block processing

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| <a id="block-processing-timeout"></a>`BlockProcessingTimeout` | int (ms) | `1000` | Timeout for block processing operations. All shipped configs set this to `10000` (10 seconds). Auto-extended to 5 minutes when a debugger is attached. |
| <a id="rebuild-local-wasm"></a>`RebuildLocalWasm` | enum | `auto` | Stylus WASM store rebuild: `false` (skip), `force` (full rebuild), `auto` (resume). |
| <a id="expose-multi-gas"></a>`ExposeMultiGas` | bool | `false` | Experimental: expose multi-dimensional gas in transaction receipts. |

### Validator (stateless validation)

Used in the [validator role](roles/validator.md).

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| <a id="validation-enabled"></a>`ValidationEnabled` | bool | `false` | Master switch for stateless validation. The `*-with-validation` configs set this to `true`. |
| <a id="validator-max-state-roots-in-mem"></a>`ValidatorMaxStateRootsInMem` | int | `1000` | Max state roots pinned simultaneously in the reconstructed-state MemDb overlay. |
| <a id="validator-reconstructed-state-memdb-max-size-mb"></a>`ValidatorReconstructedStateMemDBMaxSizeMb` | int (MB) | `1024` | Max size of the reconstructed-state MemDb overlay before oldest roots spill to disk. |

### Sequencer

Used in the [sequencer role](roles/sequencer.md).

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| <a id="sequencer-enabled"></a>`SequencerEnabled` | bool | `false` | Enables the sequencer role (Nethermind produces blocks). |
| <a id="sequencer-nonce-cache-size"></a>`SequencerNonceCacheSize` | int | `1024` | Nonce cache size for sender addresses. |
| <a id="sequencer-max-tx-queue-size"></a>`SequencerMaxTxQueueSize` | int | `1024` | Max items in the bounded transaction channel. |
| <a id="sequencer-max-tx-data-size"></a>`SequencerMaxTxDataSize` | int (bytes) | `95000` | Max transaction data size accepted. Mirrors Nitro's `MaxTxDataSize`. |
| <a id="sequencer-max-acceptable-timestamp-delta"></a>`SequencerMaxAcceptableTimestampDelta` | int (s) | `3600` | Max diff between local time and L1 block timestamp. |
| <a id="sequencer-max-block-speed-ms"></a>`SequencerMaxBlockSpeedMs` | int (ms) | `250` | Max wait when there is nothing to sequence (block-build cadence ceiling). |
| <a id="sequencer-inactive-wait-ms"></a>`SequencerInactiveWaitMs` | int (ms) | `50` | Wait when sequencer is paused/forwarding. |
| <a id="sequencer-await-tx-result"></a>`SequencerAwaitTxResult` | bool | `false` | If true, `eth_sendRawTransaction` blocks until the tx is sequenced. |
| <a id="sequencer-queue-timeout-ms"></a>`SequencerQueueTimeoutMs` | int (ms) | `12000` | Per-tx queue-wait timeout. Extended by [`TimeboostExpressLaneAdvantageMs`](#timeboost-express-lane-advantage-ms) when Timeboost is enabled. |
| <a id="sequencer-sender-whitelist"></a>`SequencerSenderWhitelist` | string | `""` | Comma-separated list of allowed senders. Empty = all senders allowed. |

### Timeboost (express-lane auctions)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| <a id="timeboost-enabled"></a>`TimeboostEnabled` | bool | `false` | Master switch for express-lane priority. |
| <a id="timeboost-express-lane-advantage-ms"></a>`TimeboostExpressLaneAdvantageMs` | int (ms) | `200` | Delay applied to non-express-lane txs at enqueue time when a controller exists. |
| <a id="timeboost-queue-timeout-in-blocks"></a>`TimeboostQueueTimeoutInBlocks` | ulong | `5` | Block-based expiry for timeboosted queue items. |
| <a id="timeboost-auction-contract-address"></a>`TimeboostAuctionContractAddress` | string | `""` | ExpressLaneAuction proxy address. **Required** when Timeboost is enabled — startup fails without it. |
| <a id="timeboost-auction-contract-poll-interval-ms"></a>`TimeboostAuctionContractPollIntervalMs` | int (ms) | `1000` | `ExpressLaneTracker` polling cadence for `resolvedRounds()`. |
| <a id="timeboost-auctioneer-address"></a>`TimeboostAuctioneerAddress` | string | `""` | Authorized auctioneer (sender of resolution txs). |
| <a id="timeboost-early-submission-grace-ms"></a>`TimeboostEarlySubmissionGraceMs` | int (ms) | `2000` | Grace period before next round during which next-round submissions are accepted. |
| <a id="timeboost-round-duration-seconds"></a>`TimeboostRoundDurationSeconds` | int (s) | `60` | Round duration. |
| <a id="timeboost-auction-closing-window-seconds"></a>`TimeboostAuctionClosingWindowSeconds` | int (s) | `15` | Auction close window at the end of each round. |

### Consensus-node RPC (block-metadata fetch)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| <a id="consensus-node-rpc-enabled"></a>`ConsensusNodeRpcEnabled` | bool | `false` | Enable fetching block metadata (e.g., timeboosted bitmap) from Nitro consensus node. |
| <a id="consensus-node-rpc-url"></a>`ConsensusNodeRpcUrl` | string | `""` | URL of the Nitro consensus node RPC. |
| <a id="consensus-node-rpc-timeout-ms"></a>`ConsensusNodeRpcTimeoutMs` | int (ms) | `10000` | Timeout for consensus-node RPC calls. |

When `ConsensusNodeRpcEnabled = false`, transaction receipts omit the `timeboosted` field.

---

<a id="verify-block-hash"></a>
## `VerifyBlockHash.*` settings

Defined in `Config/IVerifyBlockHashConfig.cs`. Optional safety net that cross-checks block hashes against an external Arbitrum RPC.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| <a id="verify-block-hash-enabled"></a>`Enabled` | bool | `false` | Turn on periodic block-hash verification. |
| <a id="verify-block-hash-every-n-blocks"></a>`VerifyEveryNBlocks` | ulong | `10000` | Check every Nth block. |
| <a id="verify-block-hash-arb-node-rpc-url"></a>`ArbNodeRpcUrl` | string | `""` | External Arbitrum RPC to verify against. |

Mainnet and Sepolia configs pre-populate `ArbNodeRpcUrl` (`https://arb1.arbitrum.io/rpc`, `https://sepolia-rollup.arbitrum.io/rpc`) but leave `Enabled = false`.

---

<a id="nethermind-base"></a>
## Standard Nethermind categories

Base Nethermind settings that Arbitrum configs customize. These categories are documented in full at [docs.nethermind.io](https://docs.nethermind.io/); the table here lists only what each shipped Arbitrum config changes from the Nethermind defaults.

| Category | Key Arbitrum-specific settings | Why |
|----------|-------------------------------|-----|
| `Init` | `ChainSpecPath`, `BaseDbPath`, `LogFileName` | Points to Arbitrum chainspec and sets DB/log paths. |
| `TxPool` | `BlobsSupport: "Disabled"` | Arbitrum doesn't support L2 blob transactions. |
| `Sync` | `NetworkingEnabled: false`, `FastSync`, `SnapSync`, `PivotNumber`/`PivotHash` | Arbitrum uses Engine API from Nitro, not P2P sync. |
| `History` | `Pruning: "Rolling"`, `PruningInterval`, `PruningTimeoutSeconds` | Drives `ArbitrumHistoryPruner` (mainnet/sepolia non-archive only). |
| `JsonRpc` | `Port: 20545`, `EnginePort: 20551`, `EngineEnabledModules` | Arbitrum's port convention; engine modules must include `Arbitrum` and `nitroexecution`. |
| `Blocks` | `SecondsPerSlot: 2`, `BuildBlocksOnMainState: true`, `PreWarmStateOnBlockProcessing` | Arbitrum's fast block time, optimized state access. |
| `Pruning` | `PruningBoundary: 192` (pruned) or `Mode: "None"` (archive) | State pruning settings. |
| `Merge` | `Enabled: true` | Required — Arbitrum uses post-merge Engine API. |
| `Discovery` | `DiscoveryVersion: "V5"` | Standard Nethermind. |
| `Metrics` | `NodeName` | Identifies the node in monitoring dashboards. |
| `Snapshot` | `Enabled`, `SnapshotDirectory`, `DownloadUrl` | Mainnet configs enable snapshot download. |

---

<a id="chainspec-engine"></a>
## Chainspec engine parameters

Defined in `Config/ArbitrumChainSpecEngineParameters.cs`. Live in chainspec files under `engine.Arbitrum`.

| Parameter | Type | Description |
|-----------|------|-------------|
| <a id="initial-arbos-version"></a>`initialArbOSVersion` | ulong | ArbOS version at chain genesis. Mainnet 6, Sepolia 10, local test 32. |
| <a id="initial-chain-owner"></a>`initialChainOwner` | Address | Address with ArbOS administrative privileges. |
| <a id="genesis-block-num"></a>`genesisBlockNum` | ulong | Genesis block number (0 for new chains; 22207817 for mainnet, which migrated from the classic chain). |
| <a id="enable-arbos"></a>`enableArbOS` | bool | Must be `true`. |
| <a id="allow-debug-precompiles"></a>`allowDebugPrecompiles` | bool | Enables `ArbDebug` precompile. `true` only for local/test chains. |
| <a id="data-availability-committee"></a>`dataAvailabilityCommittee` | bool | Whether the chain uses a DAC. |
| <a id="serialized-chain-config"></a>`serializedChainConfig` | string | Base64-encoded chain config JSON matching Nitro's `DigestInitMessage` format. |
| <a id="max-code-size"></a>`maxCodeSize` | ulong | Max contract bytecode size. |
| <a id="max-init-code-size"></a>`maxInitCodeSize` | ulong | Max init code size for contract deployment. |
| <a id="initial-l1-base-fee"></a>`initialL1BaseFee` | UInt256 | Initial L1 base fee for gas pricing. |

---

<a id="shipped-configs"></a>
## Shipped configurations

### Config inventory

`Properties/configs/`:

| File | Purpose |
|------|---------|
| `arbitrum-local.json` | Local dev (validating). |
| `arbitrum-local-sequencer.json` | Local sequencer role + Timeboost. |
| `arbitrum-sepolia.json` | Sepolia testnet (pruned). |
| `arbitrum-sepolia-archive.json` | Sepolia archive. |
| `arbitrum-sepolia-with-validation.json` | Sepolia with stateless validation enabled. |
| `arbitrum-mainnet.json` | Mainnet (pruned). |
| `arbitrum-mainnet-archive.json` | Mainnet archive. |
| `arbitrum-mainnet-with-validation.json` | Mainnet with stateless validation enabled. |

`Properties/chainspec/`:

| File | Purpose |
|------|---------|
| `arbitrum-local.json` | Local chain identity (chain ID 412346, ArbOS 32, debug precompiles on). |
| `arbitrum-sepolia.json` | Sepolia chain identity (chain ID 421614, ArbOS 10). |
| `arbitrum-mainnet.json` | Mainnet chain identity (chain ID 42161, ArbOS 6, genesis block 22207817). |
| `system-test-chainspec.template` | Template for system-test ArbOS-vN chainspecs (generated at test time). |

## Selecting a config and overriding settings

Nethermind Arbitrum is run as a Docker image, typically alongside Nitro via Docker Compose. The `docker-compose.yml` at the repo root selects the config by passing the `-c <name>` flag and mounts a host directory for the data dir.

Pick the config by setting `NETWORK` in `.env`:

```bash
NETWORK=arbitrum-mainnet                    # mainnet pruned
NETWORK=arbitrum-mainnet-archive            # mainnet archive
NETWORK=arbitrum-mainnet-with-validation    # mainnet validator
NETWORK=arbitrum-sepolia                    # sepolia pruned
NETWORK=arbitrum-sepolia-archive            # sepolia archive
NETWORK=arbitrum-sepolia-with-validation    # sepolia validator
NETWORK=arbitrum-local                      # local dev (with Nitro testnode)
NETWORK=arbitrum-local-sequencer            # local sequencer + Timeboost
```

Then start with `docker compose up -d`. See [quickstart](quickstart.md) for the full Docker Compose walkthrough.

### Overriding individual settings

Both env vars and CLI flags pass through the container:

```yaml
# docker-compose.yml override snippet
services:
  nethermind-arbitrum:
    environment:
      NETHERMIND_METRICSCONFIG_NODENAME: "my-arbitrum-node"
      NETHERMIND_ARBITRUMCONFIG_BLOCKPROCESSINGTIMEOUT: 30000
    command:
      - -c
      - arbitrum-mainnet
      - --VerifyBlockHash.Enabled=true
      - --VerifyBlockHash.VerifyEveryNBlocks=10000
```

For ad-hoc overrides without editing compose:

```bash
docker compose run --rm \
  -e NETHERMIND_ARBITRUMCONFIG_BLOCKPROCESSINGTIMEOUT=30000 \
  nethermind-arbitrum \
  -c arbitrum-mainnet --VerifyBlockHash.Enabled=true
```

See the [naming conventions table](#arbitrum) above for how a single setting maps across JSON, env var, and CLI forms.

---

## Gotchas

### Networking is always disabled
All Arbitrum configs set `Sync.NetworkingEnabled: false`. Nethermind does NOT discover peers or sync via P2P — Nitro feeds blocks via the Engine API on `EnginePort`.

### Blob transactions are disabled
`TxPool.BlobsSupport: "Disabled"` is mandatory. Arbitrum doesn't support EIP-4844 at the L2 level. The KZG precompile exists from ArbOS 30+ but only for fraud-proof verification.

### Port convention: 20xxx
Arbitrum uses `20545` (RPC) and `20551` (Engine), not Ethereum's `8545`/`8551`. This allows running alongside an L1 node on the same host.

### Engine modules must include `nitroexecution`
The `nitroexecution` module in `EngineEnabledModules` exposes the [RPC methods](rpc-api.md) Nitro uses to drive block production. Without it, Nitro cannot communicate with Nethermind.

### `UnsecureDevNoRpcAuthentication` is local-only
Only `arbitrum-local*` configs set this. Testnet and mainnet require JWT authentication for the Engine API.

### `BlockProcessingTimeout` is in milliseconds
The value is passed directly to `CancellationTokenSource(int)`, which expects milliseconds. The default `1000` is therefore 1 second; all shipped configs explicitly set it to `10000` (10 seconds). With a debugger attached, the timeout is replaced with a 5-minute fallback.

### `TimeboostAuctionContractAddress` is required when `TimeboostEnabled`
`ArbitrumSequencerModule` validates this at DI registration time and throws if Timeboost is enabled without a contract address.

### Archive vs pruned: sync strategy differs
Archive configs explicitly set `FastSync: false` and `SnapSync: false`, and have no `History.Pruning`. They process every block from genesis (or snapshot), which is slower but preserves full historical state.

### Mainnet genesis block is non-zero
Arbitrum One was migrated from the "classic" chain, so `genesisBlockNum: 22207817` in the mainnet chainspec. Block 0 on mainnet is actually block 22207817 in the historical chain — a common source of confusion.

### `*-with-validation` configs use a memory pruning profile
The validator path needs many state roots pinned at once and the reconstructed-state MemDb overlay. The pruning config is tuned accordingly: `Pruning.Mode: "Memory"`, `PruningBoundary: 192`, `MaxUnpersistedBlockCount: 1000`, `TrackPastKeys: false`. No history pruning, no snapshot.

### `MaxOpenFiles` should be set explicitly on bare-metal Linux
On a non-Docker Linux host with a high `ulimit`, the auto-adjust logic leaves `DbConfig.MaxOpenFiles` unlimited. RocksDB then never evicts file handles and crashes after several hours with a misleading `FileNotFoundException`. Set `DbConfig.MaxOpenFiles: 32768` in your config. See [FD exhaustion](troubleshooting.md#fd-exhaustion).
