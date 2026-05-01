# Quickstart

Run a Nethermind Arbitrum node paired with Nitro on Arbitrum Sepolia, using Docker Compose. Sepolia syncs faster than mainnet (~2× smaller chain), so it is the right starting point for a first run.

If you already know the layout and want a different network, jump to the [external execution role](roles/external-execution.md#running-the-role).

## Prerequisites

| Need | Why |
|------|-----|
| Docker + Docker Compose | The repo's `docker-compose.yml` runs Nethermind and Nitro side by side. |
| ~1 TB free disk for Sepolia (≥ 2 TB for mainnet) | Chain database. |
| ≥ 24 GiB RAM | Steady-state working set. |
| L1 (Ethereum) RPC endpoint with archive access for old blob data | Nitro reads L1 batches. |
| L1 Beacon endpoint | Nitro reads blob data via the Beacon API. |

If you don't already have L1 endpoints, use suggested [Arbitrum node providers](https://docs.arbitrum.io/build-decentralized-apps/reference/node-providers).

## Step 1 — Clone

```bash
git clone https://github.com/NethermindEth/nethermind-arbitrum.git
cd nethermind-arbitrum
```

You only need the Docker Compose file and `.env.example` from this repo — Docker pulls the Nethermind Arbitrum and Nitro images itself.

## Step 2 — Configure environment

```bash
cp .env.example .env
```

Edit `.env`:

```bash
# Required
PARENT_CHAIN_RPC_URL=https://your-l1-rpc.example.com
PARENT_CHAIN_BEACON_URL=https://your-l1-beacon.example.com

# Sepolia testnet (override the mainnet defaults)
NETWORK=arbitrum-sepolia
CHAIN_ID=421614
```

For mainnet, leave `NETWORK` and `CHAIN_ID` at the defaults (`arbitrum-mainnet` / `42161`). The full set of recognized network values is in [README → Network Selection](../README.md).

## Step 3 — Start

```bash
docker compose up -d
```

Compose order:

1. **Nethermind starts.** Auto-generates a JWT secret at `./nethermind-data/jwt.hex` if missing. Exposes the engine port on `:20551` and public RPC on `:20545`.
2. **Healthcheck waits.** Nitro's `depends_on` pauses until Nethermind's TCP healthcheck on `20551` passes (typically ≤ 30 s).
3. **Nitro starts.** Mounts the Nethermind data dir read-only, picks up `jwt.hex`, dials Nethermind on `http://nethermind-arbitrum:20551`.

If anything fails, see [Step 6](#step-6-troubleshoot).

## Step 4 — Watch it sync

```bash
# Tail both containers
docker compose logs -f -n 50

# Just Nethermind
docker compose logs nethermind-arbitrum -f -n 50

# Just Nitro
docker compose logs nitro -f -n 50
```

Once messages are flowing, the chain advances roughly at 0.25 Hz (Sepolia's slot time).

Sepolia from snapshot-less cold start can take many hours. Mainnet via the snapshot path takes a few hours.

## Step 5 — Verify

Once messages are flowing, verify the public RPC is responsive and the chain is advancing:

```bash
# Block height
curl -s http://localhost:20545 -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}'

# Sync state (false = synced; otherwise sync object)
curl -s http://localhost:20545 -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_syncing","params":[],"id":1}'

# Chain ID — should match your CHAIN_ID
curl -s http://localhost:20545 -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_chainId","params":[],"id":1}'
```

## Step 6 — Troubleshoot

Common first-run issues. For more, see the [troubleshooting page](troubleshooting.md).

### "Waiting for connection from consensus layer..." loops

Nitro hasn't connected yet. Check:

- Is Nitro's container running? (`docker compose ps`)
- Is Nitro's log showing connection errors against the engine URL?
- Is the JWT secret file present at `./nethermind-data/jwt.hex` and readable by both containers?

### Nitro logs "JWT mismatch" or "401 Unauthorized"

The compose file mounts `./nethermind-data` read-only into Nitro at `/nethermind-data:ro` and configures Nitro with `--node.execution-rpc-client.jwtsecret=/nethermind-data/jwt.hex`. If you regenerated the JWT secret without restarting Nitro, restart both:

```bash
docker compose restart
```

If you ran Nethermind separately first and then started Nitro against an outdated mounted path, the JWT files diverge. Easiest fix: stop everything, delete `./nethermind-data/jwt.hex`, restart Nethermind to regenerate, then start Nitro.

### Nitro logs `parent chain rpc unreachable`

Your `PARENT_CHAIN_RPC_URL` or `PARENT_CHAIN_BEACON_URL` is wrong, unreachable, or rate-limited. Test from your host:

```bash
curl $PARENT_CHAIN_RPC_URL -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}'
```

### Nethermind crashes hours into sync with `FileNotFoundException`

You are likely running on a bare-metal Linux host without `DbConfig.MaxOpenFiles` set. The crash is misleading — it's actually FD exhaustion. See [troubleshooting / FD exhaustion](troubleshooting.md#fd-exhaustion). Docker users typically don't hit this unless their daemon is configured with a high ulimit.

## Step 7 — Stop / clean up

```bash
# Graceful stop
docker compose down

# Stop AND wipe data (irreversible)
docker compose down
rm -rf ./nethermind-data ./nitro-data
```

## What's next?

- Read the [external execution role page](roles/external-execution.md) for production hardening (JWT management, snapshot caching, FD limits).
- Read [configuration](configuration.md) when you want to tune.
- Layer on the [validator](roles/validator.md) or [sequencer](roles/sequencer.md) role if your goal is something more than a regular node.
