# Troubleshooting

Symptom-first index of operational problems. Find your symptom, then read the entry.

If you don't find your symptom here, check the [GitHub issues](https://github.com/NethermindEth/nethermind-arbitrum/issues) for developer-internal notes.

## Index

- [Nethermind crashes after hours of running with `FileNotFoundException`](#fd-exhaustion)
- [Stylus call panics with "incompatible binary"](#stylus-wasmer-incompatible-binary)
- [`Waiting for connection from consensus layer...` repeats forever](#waiting-for-cl)
- [JWT / 401 errors between Nitro and Nethermind](#jwt-mismatch)
- [Sepolia / mainnet sync stuck at a specific block](#sync-stuck)

---

## FD exhaustion → misleading FileNotFoundException {#fd-exhaustion}

**Symptom.**
```
System.TypeInitializationException: The type initializer for
'Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http.ReasonPhrases' threw an exception.
 ---> System.IO.FileNotFoundException: Could not load file or assembly
'Microsoft.AspNetCore.WebUtilities, Version=10.0.0.0, ...'
...
Out of memory.
Aborted (core dumped)
```

The assembly is not actually missing. The first code path to call `open()` after FD exhaustion fails, and the CLR's lazy assembly resolver reinterprets that failure as a missing assembly.

**Cause.** Default [`DbConfig.MaxOpenFiles`](configuration.md#nethermind-base) is unset, which RocksDB translates to "unlimited." On a multi-TB chain database under co-tenant memory pressure (e.g. Nitro running on the same host), RocksDB monotonically grows its file-descriptor usage until the kernel `RLIMIT_NOFILE` is hit. The auto-adjust logic only activates when the effective `ulimit` is low (≤ 10000); on Docker hosts whose daemon raises the per-container limit, or on Linux hosts where the limit is high by default, it steps aside.

**Diagnosis.**
```bash
# Find the Nethermind PID and watch FD usage
watch -n 1 'pid=$(for p in $(pgrep -f Nethermind.Runner); do echo "$(ls /proc/$p/fd 2>/dev/null | wc -l) $p"; done | sort -rn | head -1 | awk "{print \$2}"); \
  echo "fds=$(ls /proc/$pid/fd 2>/dev/null | wc -l) maps=$(wc -l < /proc/$pid/maps 2>/dev/null)"; \
  grep "open files" /proc/$pid/limits 2>/dev/null'
```

If `fds` climbs monotonically toward `1048576` (or your ulimit), this is FD exhaustion.

**Fix.**
1. Cap explicitly in your config:
   ```jsonc
   "DbConfig":      { "MaxOpenFiles": 32768 },
   ```
   Total FD ceiling: ~`MaxOpenFiles × 15 databases`. 32768 × 15 ≈ 490K, comfortably under a 1M ulimit.
2. In Docker Compose, set `ulimits.nofile` on the Nethermind Arbitrum service so the container starts with the right ceiling:
   ```yaml
   services:
     nethermind-arbitrum:
       ulimits:
         nofile:
           soft: 1048576
           hard: 1048576
   ```

**Note.** The CLI flag for `WasmDbConfig.MaxOpenFiles` does not register (interface inheritance defect; see [configuration / naming conventions](configuration.md)). Use JSON config or env var. Most Docker users don't hit this in the first place — Docker daemons typically set a low default `ulimit` per container (1024) and the auto-adjust kicks in. The trap is hosts where the daemon ulimit has been bumped without a corresponding `MaxOpenFiles` cap in the config.

---

## Stylus "incompatible binary" panic {#stylus-wasmer-incompatible-binary}

**Symptom.** First Stylus call after a node upgrade panics:
```
encountered fatal wasm: init failed
incompatible binary: The provided bytes were serialized by an incompatible version of Wasmer
```

**Cause.** Activated Stylus programs are cached on disk as Wasmer-serialized native modules. Wasmer's serialization format is a private contract between matching `wasmer-types` versions. When the Stylus NuGet bumps the underlying Wasmer without `WasmStoreSchema.WasmerSerializeVersion` being incremented in the same change, the cached blobs become incompatible. The runtime panics on the first `Module::deserialize_unchecked` of stale bytes.

**Fix (operator).** Specify `Arbitrum.RebuildLocalWasm = force` in your config and restart. This deletes the local Wasm cache and forces a rebuild from the original Wasm blobs, which are guaranteed compatible because they ship with the client.

---

## "Waiting for connection from consensus layer..." {#waiting-for-cl}

**Symptom.** Every 30 seconds, indefinitely:
```
Waiting for connection from consensus layer...
```

**Cause.** `ArbitrumClHealthTracker` is heartbeat-based — it logs this message every 30 s until the first `nitroexecution_digestMessage` arrives. If you see it forever, Nitro hasn't connected.

**Diagnosis.**
- Is Nitro running? (`docker compose ps` or `systemctl status nitro`)
- Is Nitro pointed at the right URL? (`--node.execution-rpc-client.url`)
- Is the engine port reachable from Nitro? (`curl http://nethermind-host:20551/` — should return 401 without JWT)
- Is the JWT secret consistent on both sides?

If Nitro logs auth or connection errors, fix those. If Nitro logs nothing relevant, raise its log level (`--log-level=DEBUG`).

---

## JWT mismatch / 401 Unauthorized {#jwt-mismatch}

**Symptom.** Nitro logs `401 Unauthorized` against the engine URL, or `JWT mismatch`.

**Cause.** Nethermind auto-generates the JWT secret if the file doesn't exist, but if Nitro mounted an older copy or generated its own elsewhere, the two diverge.

**Fix.**
1. Stop both services.
2. Pick a single canonical path for `jwt.hex` (e.g. `./nethermind-data/jwt.hex` in Docker compose, or `~/.arbitrum/jwt.hex` for native).
3. Regenerate or pick one to be authoritative:
   ```bash
   mkdir -p ~/.arbitrum
   openssl rand -hex 32 > ~/.arbitrum/jwt.hex
   ```
4. Configure Nethermind: `--JsonRpc.JwtSecretFile=/path/to/jwt.hex`.
5. Configure Nitro: `--node.execution-rpc-client.jwtsecret=/path/to/jwt.hex` (mount the file into the Nitro container if Docker).
6. Restart both.

---

## Sync stuck at a specific block {#sync-stuck}

**Symptom.** Block height stops advancing. Nitro logs may show repeated reorg attempts or batch-replay stalls.

**Common causes.**

1. **Divergent state vs canonical.** Nitro detects a hash mismatch and refuses to advance. If [`VerifyBlockHash.Enabled = true`](configuration.md#verify-block-hash-enabled), Nethermind's logs will show the divergence. File an issue with the block number.
2. **L1 endpoint behind.** Nitro can't read past the L1 head — check `PARENT_CHAIN_RPC_URL`'s sync state.
3. **Beacon endpoint missing blob data.** Recent batches use blobs; archival blob retention is finite. Pre-Pectra blob data may not be available from public Beacon endpoints.
4. **Chain reorganization in progress.** Nitro is working through a batch reorg; this clears once reorg processing completes.

**Diagnosis.**
```bash
# Last block Nethermind processed
curl -s http://localhost:20545 -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}' | jq -r .result

# Compare with the reference RPC
curl -s https://arb1.arbitrum.io/rpc -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","method":"eth_blockNumber","params":[],"id":1}' | jq -r .result
```

If the gap is small and stable, sync is just slow. If the gap is growing, Nitro is failing to feed new messages — investigate L1/Beacon connectivity.
