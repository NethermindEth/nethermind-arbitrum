# Validator Role

> **Maturity: Beta.** Stateless validation works end-to-end against Sepolia and mainnet, with full pruning coordination and the BOLD challenge-protocol contract on the Nitro side. Production use is reasonable for operators who can tolerate rough edges and want to participate in protocol validation; expect ongoing performance work and config changes. The shipped `*-with-validation` configs are the supported path; bespoke validator setups are not.

In the validator role, Nitro asks Nethermind for execution witnesses for blocks it wants to verify. Nethermind reconstructs the parent state on demand, captures every trie node and storage slot the block touches, and returns a witness Nitro can replay through its WAVM machine. Nitro's verdict feeds the staker, which posts and defends assertions on L1.

For the protocol-level explanation of how Arbitrum validation, fraud proofs, and BOLD work, see [Arbitrum docs on validators](https://docs.arbitrum.io/run-arbitrum-node/more-types/run-validator-node).

## When to choose this role

Pick the validator role if you:

- **Want to validate Arbitrum's protocol-level state transitions** rather than just trusting the canonical chain.
- **Are running a staker** that posts assertions on L1 and may need to defend them in BOLD challenges.
- **Need to verify that a chain you operate matches the canonical assertion record** without maintaining a full second copy of state.

Skip the validator role if you only need read access (use [external execution](external-execution.md)) or want Nethermind to produce blocks (use the [sequencer role](sequencer.md)).

## Prerequisites

Everything from [external execution prerequisites](external-execution.md#prerequisites), plus:

- **Increased memory budget.** The reconstructed-state MemDb overlay defaults to 1 GiB ([`ValidatorReconstructedStateMemDBMaxSizeMb`](../configuration.md#validator-reconstructed-state-memdb-max-size-mb)) and pins up to 1000 state roots ([`ValidatorMaxStateRootsInMem`](../configuration.md#validator-max-state-roots-in-mem)). Plan ≥ 32 GiB RAM available to Nethermind in addition to the base footprint.
- **Memory-mode pruning.** The shipped `*-with-validation` configs use `Pruning.Mode: "Memory"` rather than the default disk-pruning profile. The validator path needs many state roots pinned at once.
- **No history pruning, no snapshot.** Validation requires full block availability from the validated frontier backward. The `*-with-validation` configs disable `History.Pruning` and `Snapshot`.
- **Nitro configured to use this Nethermind as the execution client.** On the Nitro side, `--node.execution-rpc-client.url=http://...:20551` plus the staker/validator flags from Nitro's docs.

## Configuration walkthrough

The `*-with-validation` configs are the supported entry points:

| Config | Use |
|--------|-----|
| `arbitrum-sepolia-with-validation` | Sepolia validator. Recommended starting point for any new operator. |
| `arbitrum-mainnet-with-validation` | Arbitrum One validator. |

### Tuning the memory caps

If the validator falls behind because too many state roots get evicted (forcing re-reconstruction), raise [`ValidatorMaxStateRootsInMem`](../configuration.md#validator-max-state-roots-in-mem). If RSS grows too aggressively, lower [`ValidatorReconstructedStateMemDBMaxSizeMb`](../configuration.md#validator-reconstructed-state-memdb-max-size-mb) — eviction will spill oldest roots to the main state DB rather than discarding them, so the validator stays correct, just slower.

### Capping safe/finalized at validated

Validators often want safe and finalized block tags to track the *validated* frontier rather than just consensus confirmations. Two flags cap them:

```jsonc
{
  "Arbitrum": {
    "SafeBlockWaitForValidator": true,
    "FinalizedBlockWaitForValidator": true
  }
}
```

When set, [`setFinalityData`](../rpc-api.md#set-finality-data) capping logic prevents the safe/finalized tags from racing ahead of validation.

## Running the role

The mechanics are the same as external execution. Set the `*-with-validation` config in `.env` and start the compose stack:

```bash
cp .env.example .env
# Set in .env:
#   NETWORK=arbitrum-sepolia-with-validation
#   CHAIN_ID=421614
#   PARENT_CHAIN_RPC_URL=...
#   PARENT_CHAIN_BEACON_URL=...

docker compose up -d
```

Then start Nitro with the validator/staker flags from the [Nitro validator docs](https://docs.arbitrum.io/run-arbitrum-node/more-types/run-validator-node).

## Known issues / limitations

- **Memory pressure tuning is workload-dependent.** A node validating from a recent staked frontier behaves very differently from a node catching up from genesis. The shipped defaults target the steady-state case; bulk catch-up may need higher caps.
- **`MarkValid` requires the candidate to still be canonical.** If a reorg landed between the candidate being recorded and `setFinalityData(validated=...)` arriving, promotion fails (returns null, logs an error). The validator skips that promotion and waits for the next valid checkpoint.
- **Stateless verification is shape-equivalence with the in-process Geth recorder, not byte-equivalence.** The witness's `Preimages` map must answer every `keccakPreimage` query the WASM machine makes during replay. If a recording captures preimages the in-process recorder would not have, that is fine — supersets are correct. If it misses one, replay fails.
- **Cross-arch validator farms need every WASM target.** When one node is the recorder and others are validators on different architectures (e.g., a WAVM Arbitrator alongside a native-arch JIT), the recorder must capture the union of targets. The `wasmTargets` array on [`recordBlockCreation`](../rpc-api.md#record-block-creation) is how Nitro communicates this; misconfiguring it produces silent verification gaps.
