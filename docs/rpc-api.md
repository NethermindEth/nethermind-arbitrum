# RPC API Reference

Nitro consensus drives Nethermind execution over JSON-RPC. This page documents the two RPC namespaces that carry that traffic. For background on how the namespaces integrate with the rest of the plugin, see [architecture](architecture.md).

## Audience

This artifact is contributor-facing. Routine operators do not call these methods directly — Nitro does. You are likely here because you are diagnosing a Nitro↔Nethermind mismatch, building tooling that drives the boundary, or modifying the plugin's RPC layer.

## Namespaces

| Namespace | Purpose | Status |
|-----------|---------|--------|
| `nitroexecution` | Matches Nitro's Go `ExecutionClient` interface. Flat parameters, raw-number serialization. | Canonical for new integrations. |
| `arbitrum` | Wrapped parameter objects, hex-encoded numbers in some responses. | Legacy. Marked for removal once migration is complete. |

Both delegate to the same `IArbitrumExecutionEngine` backend. Production Nitro deployments use `nitroexecution`.

The **engine port** (`20551` by default) is JWT-protected and exposes both namespaces. They are not exposed on the public port (`20545`). See [configuration](configuration.md#shipped-configs) for the port and module convention.

## Source of truth

The Nitro side defines the contract:

- Go interface: `nitro/execution/interface.go` (in the Nitro source tree).
- Implementation must match Nitro exactly for state consistency.

When a method is added or changed in Nitro, the corresponding Nethermind change is mechanical. When in doubt, the Go interface wins.

---

<a id="nitroexecution"></a>
## `nitroexecution` namespace

Defined in `Modules/INitroExecutionRpcModule.cs`. All methods are JSON-RPC-prefixed with `nitroexecution_`. Listed below grouped by purpose.

### Block production from messages

<a id="digest-message"></a>
#### `nitroexecution_digestMessage`

Process a message and produce a block.

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `msgIdx` | `MessageIndex` (uint64) | Message index (L2 block index). |
| 1 | `message` | `MessageWithMetadata` | Message data with metadata. |
| 2 | `messageForPrefetch` | `MessageWithMetadata?` | Optional next message for prefetching. |

Returns: `MessageResult` — `{ blockHash: Hash256, sendRoot: Hash256 }`.

Errors:
- `CreateBlock mutex held` — another block is being produced. Nitro is sending faster than Nethermind processes.
- `Wrong block number` — message index doesn't match expected.

<a id="reorg"></a>
#### `nitroexecution_reorg`

Handle chain reorganization.

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `msgIdxOfFirstMsgToAdd` | `MessageIndex` | First message index after reorg point. |
| 1 | `newMessages` | `MessageWithMetadataAndBlockInfo[]` | New messages to process. |
| 2 | `oldMessages` | `MessageWithMetadata[]` | Old messages for context. |

Returns: `MessageResult[]` — one per processed new message.

Cannot reorg to genesis (`msgIdxOfFirstMsgToAdd != 0` is enforced).

### Index lookups

<a id="result-at-message-index"></a>
#### `nitroexecution_resultAtMessageIndex`

Get the block result at a specific message index.

| Position | Name | Type |
|----------|------|------|
| 0 | `messageIndex` | `MessageIndex` |

Returns: `MessageResult`.

<a id="head-message-index"></a>
#### `nitroexecution_headMessageIndex`

Get the current head message index. No parameters.

Returns: `MessageIndex` (raw uint64, not hex).

<a id="message-index-to-block-number"></a>
#### `nitroexecution_messageIndexToBlockNumber`

| Position | Name | Type |
|----------|------|------|
| 0 | `messageIndex` | `MessageIndex` |

Returns: `long` — corresponding block number.

<a id="block-number-to-message-index"></a>
#### `nitroexecution_blockNumberToMessageIndex`

| Position | Name | Type |
|----------|------|------|
| 0 | `blockNumber` | `ulong` |

Returns: `MessageIndex`.

### Finality and sync

<a id="set-finality-data"></a>
#### `nitroexecution_setFinalityData`

Update finality information (safe, finalized, validated).

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `safeFinalityData` | `RpcFinalityData?` | Safe block data. |
| 1 | `finalizedFinalityData` | `RpcFinalityData?` | Finalized block data. |
| 2 | `validatedFinalityData` | `RpcFinalityData?` | Validated block data (validator role). |

`RpcFinalityData` (readonly struct):

| Field | Type | Description |
|-------|------|-------------|
| `msgIdx` | `uint64` | Message index (raw number, not hex). |
| `blockHash` | `Hash256` | Block hash. |

Returns: `EmptyResponse`.

Notes:
- Pass `null` for any finality data that should not be updated.
- When [`SafeBlockWaitForValidator`](configuration.md#safe-block-wait-for-validator) is enabled, the safe block tag is capped at the validated level.
- When [`FinalizedBlockWaitForValidator`](configuration.md#finalized-block-wait-for-validator) is enabled, the finalized tag is capped similarly.
- When [`ValidationEnabled`](configuration.md#validation-enabled) is `true` and `validatedFinalityData` is non-null, this method also triggers the internal `MarkValid` flow that promotes the validator's candidate header. See the [validator role](roles/validator.md) for how this fits the validation pipeline.

<a id="set-consensus-sync-data"></a>
#### `nitroexecution_setConsensusSyncData`

Update consensus-layer sync status.

| Position | Name | Type |
|----------|------|------|
| 0 | `syncData` | `SetConsensusSyncDataParams` |

`SetConsensusSyncDataParams`:

| Field | Type | Description |
|-------|------|-------------|
| `synced` | `bool` | Whether CL is synced. |
| `maxMessageCount` | `uint64` | Maximum message count. |
| `syncProgressMap` | `Dictionary<string, object>` | Detailed sync progress. |
| `updatedAt` | `long` | Timestamp of update. |

Returns: `EmptyResponse`.

<a id="mark-feed-start"></a>
#### `nitroexecution_markFeedStart`

Mark feed start position for L1 price data caching.

| Position | Name | Type |
|----------|------|------|
| 0 | `to` | `MessageIndex` |

Returns: `EmptyResponse`.

### Maintenance

<a id="trigger-maintenance"></a>
#### `nitroexecution_triggerMaintenance`

Trigger maintenance operations. No parameters.

Returns: `string` (`"OK"` on success).

<a id="should-trigger-maintenance"></a>
#### `nitroexecution_shouldTriggerMaintenance`

Check if maintenance should be triggered. No parameters.

Returns: `bool`.

<a id="maintenance-status"></a>
#### `nitroexecution_maintenanceStatus`

Get the current maintenance status. No parameters.

Returns: `MaintenanceStatus` — `{ isRunning: bool }`.

### Sequencer control

These methods are used by the [sequencer role](roles/sequencer.md). Calling them when the role is not enabled returns the structured error code `-50001` (`NoSequencer`).

<a id="start-sequencing"></a>
#### `nitroexecution_startSequencing`

Begin sequencing a new block. Called by the consensus layer roughly every 250 ms.

| Position | Name | Type |
|----------|------|------|
| 0 | `l1BlockNumber` | `ulong` |
| 1 | `l1Timestamp` | `ulong` |
| 2 | `timestamp` | `ulong` |

Returns: `StartSequencingResult` — sequenced message + wait time hint.

`AppendLastSequencedBlock` must complete before the next `StartSequencing` — this is a hard invariant.

<a id="end-sequencing"></a>
#### `nitroexecution_endSequencing`

Finalize the in-flight sequencing operation.

| Position | Name | Type |
|----------|------|------|
| 0 | `error` | `string?` |

Returns: `EmptyResponse`.

Three outcomes:
- `null` (success): finalize nonce cache, notify submitters.
- `"retry sequencer"`: forward to backup or re-queue. Submitters not notified yet.
- non-retry error: return error to all submitters.

<a id="append-last-sequenced-block"></a>
#### `nitroexecution_appendLastSequencedBlock`

Mark the last produced block as appended; cache its L1 price data and clear temp state. No parameters.

Returns: `EmptyResponse`.

<a id="enqueue-delayed-messages"></a>
#### `nitroexecution_enqueueDelayedMessages`

Enqueue L1 delayed messages for processing.

| Position | Name | Type |
|----------|------|------|
| 0 | `messages` | `L1IncomingMessage[]` |
| 1 | `firstMsgIdx` | `ulong` |

Returns: `EmptyResponse`.

<a id="next-delayed-message-number"></a>
#### `nitroexecution_nextDelayedMessageNumber`

Get the next delayed-message number expected by the sequencer. No parameters.

Returns: `ulong`.

<a id="resequence-reorged-message"></a>
#### `nitroexecution_resequenceReorgedMessage`

Re-sequence a message that was rolled back by a reorg.

| Position | Name | Type |
|----------|------|------|
| 0 | `message` | `MessageWithMetadata?` |

Returns: `SequencedMsg?`.

<a id="pause"></a>
#### `nitroexecution_pause`

Pause sequencing. New `eth_sendRawTransaction` calls are rejected; the sequencer returns a 50 ms wait. No parameters.

Returns: `EmptyResponse`.

<a id="activate"></a>
#### `nitroexecution_activate`

Resume sequencing from `Paused` or `Inactive` state. No parameters.

Returns: `EmptyResponse`.

<a id="forward-to"></a>
#### `nitroexecution_forwardTo`

Switch to forwarding mode — incoming transactions are relayed to the URL via HTTP rather than sequenced locally.

| Position | Name | Type |
|----------|------|------|
| 0 | `url` | `string` |

Returns: `EmptyResponse`.

### Timeboost / express-lane

Used when [`TimeboostEnabled`](configuration.md#timeboost-enabled) is `true`.

<a id="publish-auction-resolution-transaction"></a>
#### `nitroexecution_publishAuctionResolutionTransaction`

Publish a Timeboost auction-resolution transaction (the round winner). Bounded queue, drops oldest on overflow.

| Position | Name | Type |
|----------|------|------|
| 0 | `rlpTransaction` | `byte[]` |

Returns: `bool` — accepted into queue.

<a id="publish-express-lane-transaction"></a>
#### `nitroexecution_publishExpressLaneTransaction`

Publish a signed express-lane bid (Timeboost submission).

| Position | Name | Type |
|----------|------|------|
| 0 | `submission` | `ExpressLaneSubmissionForRpc` |

Returns: `bool`.

### Validator (stateless validation)

Used when [`ValidationEnabled`](configuration.md#validation-enabled) is `true`. See the [validator role](roles/validator.md) and [architecture](architecture.md#validation-flow) for the full pipeline.

<a id="prepare-for-record"></a>
#### `nitroexecution_prepareForRecord`

Pre-warm state for an upcoming range of `recordBlockCreation` calls. Genesis is skipped.

| Position | Name | Type |
|----------|------|------|
| 0 | `start` | `ulong` |
| 1 | `end` | `ulong` |

Returns: `EmptyResponse`.

Walks each header in the range, calls `StateReconstructor.EnsureStateAvailable` + `UpdateValidCandidateHeader`, then `PreparedAddTrim` (FIFO with [`ValidatorMaxStateRootsInMem`](configuration.md#validator-max-state-roots-in-mem) cap).

<a id="record-block-creation"></a>
#### `nitroexecution_recordBlockCreation`

Generate an execution witness for a specific block.

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `pos` | `ulong` | Message index of the block to record. |
| 1 | `message` | `MessageWithMetadata` | Block message. |
| 2 | `wasmTargets` | `string[]` | WASM target architectures (e.g. `["arm64", "amd64", "host"]`). |

Returns: `RecordResult` — `{ pos, blockHash, preimages: map[Hash256]bytes, userWasms: ... }`.

Cannot record genesis (returns `"Cannot generate witness for genesis block"`). Built block hash must match canonical or recording fails.

`Preimages` is base64-encoded over JSON-RPC to match Go's default `[]byte` encoding.

---

<a id="arbitrum-namespace"></a>
## `arbitrum` namespace (legacy)

Defined in `Modules/IArbitrumRpcModule.cs`. Provides the same core functionality as `nitroexecution` plus extras, with two shape differences:

- Parameters are wrapped in objects (`DigestMessageParameters`, `ReorgParameters`, ...) instead of flat.
- Some responses use hex-encoded numbers.

> **Legacy.** New integrations should use [`nitroexecution`](#nitroexecution). The interface carries a `// TODO: Remove this interface after migration to INitroExecutionRpcModule is complete` comment in source.

### Methods

| Method | Equivalent in `nitroexecution` |
|--------|--------------------------------|
| `arbitrum_digestInitMessage` | (none — initialization-only) |
| `arbitrum_digestMessage` | [`digestMessage`](#digest-message) (wrapped) |
| `arbitrum_reorg` | [`reorg`](#reorg) (wrapped) |
| `arbitrum_setFinalityData` | [`setFinalityData`](#set-finality-data) (wrapped) |
| `arbitrum_setConsensusSyncData` | [`setConsensusSyncData`](#set-consensus-sync-data) |
| `arbitrum_resultAtMessageIndex` | [`resultAtMessageIndex`](#result-at-message-index) |
| `arbitrum_headMessageIndex` | [`headMessageIndex`](#head-message-index) |
| `arbitrum_messageIndexToBlockNumber` | [`messageIndexToBlockNumber`](#message-index-to-block-number) |
| `arbitrum_blockNumberToMessageIndex` | [`blockNumberToMessageIndex`](#block-number-to-message-index) |
| `arbitrum_markFeedStart` | [`markFeedStart`](#mark-feed-start) |
| `arbitrum_synced` | (none — `setConsensusSyncData` is the modern path) |
| `arbitrum_fullSyncProgressMap` | (none) |
| `arbitrum_arbOSVersionForMessageIndex` | (none) |
| `arbitrum_triggerMaintenance` | [`triggerMaintenance`](#trigger-maintenance) |
| `arbitrum_shouldTriggerMaintenance` | [`shouldTriggerMaintenance`](#should-trigger-maintenance) |
| `arbitrum_maintenanceStatus` | [`maintenanceStatus`](#maintenance-status) |
| `arbitrum_recordBlockCreation` | [`recordBlockCreation`](#record-block-creation) (wrapped) |
| `arbitrum_prepareForRecord` | [`prepareForRecord`](#prepare-for-record) (wrapped) |
| `arbitrum_startSequencing` | [`startSequencing`](#start-sequencing) (wrapped) |
| `arbitrum_endSequencing` | [`endSequencing`](#end-sequencing) (wrapped) |
| `arbitrum_enqueueDelayedMessages` | [`enqueueDelayedMessages`](#enqueue-delayed-messages) (wrapped) |
| `arbitrum_appendLastSequencedBlock` | [`appendLastSequencedBlock`](#append-last-sequenced-block) |
| `arbitrum_nextDelayedMessageNumber` | [`nextDelayedMessageNumber`](#next-delayed-message-number) |
| `arbitrum_resequenceReorgedMessage` | [`resequenceReorgedMessage`](#resequence-reorged-message) |
| `arbitrum_pause` | [`pause`](#pause) |
| `arbitrum_activate` | [`activate`](#activate) |
| `arbitrum_forwardTo` | [`forwardTo`](#forward-to) |

### Shape differences (examples)

`digestMessage`:

```jsonc
// arbitrum (wrapped)
{ "params": [{ "index": 1000, "message": {...}, "messageForPrefetch": null }] }

// nitroexecution (flat)
{ "params": [1000, {...}, null] }
```

`setFinalityData`:

```jsonc
// arbitrum (wrapped)
{ "params": [{ "safeFinalityData": {...}, "finalizedFinalityData": {...} }] }

// nitroexecution (flat)
{ "params": [{...}, {...}, null] }
```

---

<a id="debug"></a>
## Debug methods

Defined in `Modules/IArbitrumDebugRpcModule.cs`. Available only on the `Debug` module; intended for system tests.

| Method | Description |
|--------|-------------|
| `debug_reinitialize(arbosVersion, accountsJson, maxCodeSize?)` | Reinitialize the execution engine with new chainspec/genesis. **MemDb mode only.** Used by system-test harnesses to switch ArbOS versions between tests. |
| `debug_schedulePruneHistory()` | Manually schedule a prune-history run (blocks and receipts). Behaves identically to the automatic pruner. |

---

<a id="types"></a>
## Common types

`MessageResult`:

| Field | Type | Description |
|-------|------|-------------|
| `blockHash` | `Hash256` | Block hash. |
| `sendRoot` | `Hash256` | Merkle accumulator send root. |

`RpcFinalityData`:

| Field | Type | Description |
|-------|------|-------------|
| `msgIdx` | `uint64` | Message index (raw number). |
| `blockHash` | `Hash256` | Block hash. |

`RecordResult`:

| Field | Type | Description |
|-------|------|-------------|
| `pos` | `ulong` | Message index of recorded block. |
| `blockHash` | `Hash256` | Produced block hash. |
| `preimages` | `Map<Hash256, bytes>` | Trie nodes + headers + codes accessed during execution. Base64-encoded over JSON-RPC. |
| `userWasms` | `...` | Stylus user WASM modules activated during the block, keyed by target. |

---

## Error handling

Standard JSON-RPC errors:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "error": {
    "code": -32000,
    "message": "Error description"
  }
}
```

| Code | Description |
|------|-------------|
| `-32000` | Internal error. |
| `-32602` | Invalid params. |
| `-50001` | `NoSequencer` — sequencer-only method called when [`SequencerEnabled`](configuration.md#sequencer-enabled) is `false`. |

Sequencer-specific structured error codes are defined in `Sequencer/ArbitrumSequencerErrors.cs`.

---

## Behavioral notes

### Block-production semaphore never waits
`SemaphoreSlim(1, 1)` with `WaitAsync(0)` — acquires immediately or fails. "CreateBlock mutex held" means Nitro is sending faster than Nethermind processes.

### Reorg clears finality
Safe/finalized blocks above the reorg cut point are cleared.

### Comparison-mode binary search
`ArbitrumExecutionEngineWithComparison` (when running in comparison mode against a reference EL) binary-searches to find the first divergent block on hash mismatch.

### Health tracker is heartbeat-based
`MarkConnected()` is only called from `nitroexecution_digestMessage`. If no messages arrive for an extended period, the tracker won't detect disconnection — it only detects initial connectivity.
