# External Execution Role

> **Maturity: Stable.** This is the default and recommended chain role. Most operational paths — sync from snapshot, pruned and archive, JWT-authenticated Engine API — are covered by automated tests and have been exercised against Arbitrum One mainnet and Sepolia testnet.

Nitro consensus runs the protocol; Nethermind Arbitrum runs the execution. This shape matches the [Arbitrum full node](https://docs.arbitrum.io/run-arbitrum-node/run-full-node) description on `docs.arbitrum.io` — Nethermind simply replaces the Geth-based execution layer that ships inside Nitro.

## When to choose this role

Pick the external execution role if you want to:

- **Run a regular Arbitrum node** for L2 RPC access, dApp backends, indexers, or general read traffic.
- **Sync mainnet or Sepolia** from an official Nethermind snapshot.

Pick a different role on top only if you need stateless validation ([validator role](validator.md)) or want Nethermind to produce blocks itself ([sequencer role](sequencer.md)). Roles can be combined — for example, validator on top of external execution.

## Prerequisites

- **Nitro consensus client.** Use the official `offchainlabs/nitro-node` image. The `docker-compose.yml` at the repo root pins a known-good version; update it if you build your own image.
- **Ethereum L1 RPC and Beacon endpoints** (`PARENT_CHAIN_RPC_URL`, `PARENT_CHAIN_BEACON_URL`). Nitro reads L1 batches and blob data from these. Pruned archive nodes work fine.
- **Disk space.** Mainnet pruned ≈ 2 TB. Mainnet archive ≈ 4 TB+. Sepolia is ~2× smaller.
- **System resources.** A modern host with ≥ 24 GB RAM and a fast NVMe drive for the database. RocksDB is I/O-sensitive.

## Configuration walkthrough

The external execution role uses one of the shipped configs that does **not** set [`SequencerEnabled`](../configuration.md#sequencer-enabled) or [`ValidationEnabled`](../configuration.md#validation-enabled):

| Config | Use |
|--------|-----|
| `arbitrum-mainnet` | Arbitrum One pruned (rolling history pruning, snapshot sync). |
| `arbitrum-mainnet-archive` | Arbitrum One archive (full history, no pruning). |
| `arbitrum-sepolia` | Sepolia pruned. |
| `arbitrum-sepolia-archive` | Sepolia archive. |
| `arbitrum-local` | Local development paired with the Nitro testnode. |

`nitroexecution` and `Arbitrum` modules **must** appear in `EngineEnabledModules` — Nitro communicates over those namespaces. P2P networking is off.

Mainnet and Sepolia configs pre-populate `Snapshot.DownloadUrl` and `VerifyBlockHash.ArbNodeRpcUrl` — see [configuration / shipped configs](../configuration.md#shipped-configs) for the full per-config diff.

## Running the role

The supported launch path is Docker Compose. The repo's `docker-compose.yml` runs both Nethermind Arbitrum and Nitro side by side, with JWT authentication wired up automatically.

```bash
cp .env.example .env
# Edit .env with your L1 RPC / Beacon URLs:
#   PARENT_CHAIN_RPC_URL=...
#   PARENT_CHAIN_BEACON_URL=...
#   NETWORK=arbitrum-mainnet      (default)
#   CHAIN_ID=42161                (default)

docker compose up -d
```

Nethermind starts first, generates a JWT secret at `./nethermind-data/jwt.hex` if missing, and exposes the engine port to the network the compose file creates. Nitro waits for Nethermind's healthcheck (`tcp/20551`) before starting, then mounts the JWT secret read-only and connects.

To pick a different network (Sepolia, archive), set `NETWORK` in `.env` to one of the [shipped configs](../configuration.md#shipped-configs). To override individual settings without editing the JSON config, pass `NETHERMIND_*` env vars or extra CLI flags through the `command:` section of the compose file — see [configuration / overriding settings](../configuration.md#selecting-a-config-and-overriding-settings).

## Verification

The node is healthy when:

- Nitro's logs show `Allocated cache and file handles` and `created chain config from chain id` without errors.
- Nethermind's logs stop printing `Waiting for connection from consensus layer...` (this happens once the first `digestMessage` arrives).
- `eth_syncing` against the public RPC port returns either `false` (synced) or a sync object with progress.
- Block height advances at roughly 0.25 Hz in steady state (Arbitrum's 250 ms slot time).

### Quick health checks

```bash
# Block height
curl -s http://localhost:20545 -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}' | jq .result

# Sync state
curl -s http://localhost:20545 -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_syncing","params":[],"id":1}' | jq .result

# Chain ID (sanity check the chainspec applied)
curl -s http://localhost:20545 -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_chainId","params":[],"id":1}' | jq .result
```

### Optional: block-hash verification

Mainnet and Sepolia configs ship with [`VerifyBlockHash`](../configuration.md#verify-block-hash) pre-configured but disabled. Turn it on to periodically cross-check produced block hashes against the public Arbitrum RPC. In Docker Compose, set the env vars:

```yaml
services:
  nethermind-arbitrum:
    environment:
      NETHERMIND_VERIFYBLOCKHASHCONFIG_ENABLED: "true"
      NETHERMIND_VERIFYBLOCKHASHCONFIG_VERIFYEVERYNBLOCKS: 10000
```

This is a useful safety net during early adoption — it catches divergences from canonical state without requiring you to run a full validator.

## Known issues / limitations

- **`MaxOpenFiles` defaults are dangerous on Linux Docker hosts with high `ulimit`.** Auto-adjust steps aside on hosts whose effective `RLIMIT_NOFILE` is high, leaving RocksDB unbounded. The process will eventually crash with a misleading `FileNotFoundException`. Set [`DbConfig.MaxOpenFiles: 32768`](../configuration.md#nethermind-base) explicitly. See [troubleshooting / FD exhaustion](../troubleshooting.md#fd-exhaustion).
- **Mainnet genesis block is non-zero.** Arbitrum One was migrated from the classic chain at block 22207817. Block 0 in the historical chain is not the same as block 0 in the current chain. RPC clients and analytics tooling that assume `block 0 == genesis` need to be reconciled with [`genesisBlockNum`](../configuration.md#genesis-block-num).
