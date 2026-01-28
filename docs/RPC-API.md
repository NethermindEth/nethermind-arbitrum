# Arbitrum RPC API Reference

## Overview

The Arbitrum plugin provides two RPC namespaces for communication between Nitro (consensus layer) and Nethermind (execution layer):

| Namespace | Status | Description |
|-----------|--------|-------------|
| `nitroexecution` | **Primary** | Nitro ExecutionClient interface with flat parameters |
| `arbitrum` | Legacy | Wrapped parameters, maintained for compatibility |

The `nitroexecution` namespace is the recommended interface. It matches Nitro's native `ExecutionClient` interface directly, using flat parameters and raw number serialization.

---

## nitroexecution Namespace

### nitroexecution_digestMessage

Process a message and produce a block.

**Parameters:**

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `msgIdx` | `uint64` | Message index (L2 block index) |
| 1 | `message` | `MessageWithMetadata` | Message data with metadata |
| 2 | `messageForPrefetch` | `MessageWithMetadata?` | Optional next message for prefetching |

**MessageWithMetadata:**

| Field | Type | Description |
|-------|------|-------------|
| `message` | `L1IncomingMessage` | The actual message content |
| `delayedMessagesRead` | `uint64` | Count of delayed messages read |

**L1IncomingMessage:**

| Field | Type | Description |
|-------|------|-------------|
| `header` | `L1IncomingMessageHeader` | Message header with L1 info |
| `l2Msg` | `bytes` | Base64-encoded L2 message data |
| `batchGasCost` | `uint64?` | Optional batch gas cost |
| `batchDataTokens` | `BatchDataStats?` | Optional batch data statistics |

**Returns:** `MessageResult`

| Field | Type | Description |
|-------|------|-------------|
| `blockHash` | `Hash256` | Produced block hash |
| `sendRoot` | `Hash256` | Updated send root |

**Example:**

```json
{
  "jsonrpc": "2.0",
  "method": "nitroexecution_digestMessage",
  "params": [
    1000,
    {
      "message": {
        "header": { ... },
        "l2Msg": "base64data..."
      },
      "delayedMessagesRead": 5
    },
    null
  ],
  "id": 1
}
```

**Errors:**
- `CreateBlock mutex held` - Another block is being produced
- `Wrong block number` - Message index doesn't match expected

---

### nitroexecution_reorg

Handle chain reorganization.

**Parameters:**

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `msgIdxOfFirstMsgToAdd` | `uint64` | First message index after reorg point |
| 1 | `newMessages` | `MessageWithMetadataAndBlockInfo[]` | New messages to process |
| 2 | `oldMessages` | `MessageWithMetadata[]` | Old messages for context |

**Returns:** `MessageResult[]` - Results for each processed message

---

### nitroexecution_setFinalityData

Update finality information (safe, finalized, validated blocks).

**Parameters:**

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `safeFinalityData` | `RpcFinalityData?` | Safe block data |
| 1 | `finalizedFinalityData` | `RpcFinalityData?` | Finalized block data |
| 2 | `validatedFinalityData` | `RpcFinalityData?` | Validated block data (for validator wait) |

**RpcFinalityData** (readonly struct):

| Field | Type | Description |
|-------|------|-------------|
| `msgIdx` | `uint64` | Message index (raw number, not hex) |
| `blockHash` | `Hash256` | Block hash |

**Returns:** `"OK"` on success

**Example:**

```json
{
  "jsonrpc": "2.0",
  "method": "nitroexecution_setFinalityData",
  "params": [
    { "msgIdx": 1000, "blockHash": "0xabc..." },
    { "msgIdx": 900, "blockHash": "0xdef..." },
    null
  ],
  "id": 1
}
```

**Notes:**
- Pass `null` for any finality data that shouldn't be updated
- When `SafeBlockWaitForValidator` config is enabled, uses validated block as safe if safe > validated
- When `FinalizedBlockWaitForValidator` config is enabled, uses validated block as finalized if finalized > validated

---

### nitroexecution_setConsensusSyncData

Update consensus layer sync status.

**Parameters:**

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `syncData` | `SetConsensusSyncDataParams` | Sync data |

**SetConsensusSyncDataParams:**

| Field | Type | Description |
|-------|------|-------------|
| `synced` | `bool` | Whether CL is synced |
| `maxMessageCount` | `uint64` | Maximum message count |
| `syncProgressMap` | `Dictionary<string, object>` | Detailed sync progress |
| `updatedAt` | `long` | Timestamp of update |

**Returns:** `"OK"` on success

---

### nitroexecution_resultAtMessageIndex

Get block result at a specific message index.

**Parameters:**

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `messageIndex` | `uint64` | Message index |

**Returns:** `MessageResult`

| Field | Type | Description |
|-------|------|-------------|
| `blockHash` | `Hash256` | Block hash at index |
| `sendRoot` | `Hash256` | Send root at index |

---

### nitroexecution_headMessageIndex

Get the current head (latest) message index.

**Parameters:** None

**Returns:** `uint64` - Head message index (raw number)

---

### nitroexecution_markFeedStart

Mark feed start position for L1 price data caching.

**Parameters:**

| Position | Name | Type | Description |
|----------|------|------|-------------|
| 0 | `to` | `uint64` | Feed start position |

**Returns:** `"OK"` on success

---

### nitroexecution_maintenanceStatus

Get the current maintenance status.

**Parameters:** None

**Returns:** `MaintenanceStatus`

| Field | Type | Description |
|-------|------|-------------|
| `isRunning` | `bool` | Whether maintenance is currently running |

---

### nitroexecution_shouldTriggerMaintenance

Check if maintenance should be triggered.

**Parameters:** None

**Returns:** `bool` - `true` if maintenance should be triggered

---

### nitroexecution_triggerMaintenance

Trigger maintenance operations.

**Parameters:** None

**Returns:** `"OK"` on success

---

## arbitrum Namespace (Legacy)

> **Note:** The `arbitrum` namespace is maintained for legacy development tools. New integrations should use the `nitroexecution` namespace.

The `arbitrum` namespace provides the same functionality as `nitroexecution` but with:
- Parameters wrapped in objects instead of flat
- Hex-encoded numbers in some responses

### Method Summary

| Method | Description |
|--------|-------------|
| `arbitrum_digestInitMessage` | Initialize genesis state |
| `arbitrum_digestMessage` | Process message (wrapped `DigestMessageParameters`) |
| `arbitrum_reorg` | Handle reorg (wrapped `ReorgParameters`) |
| `arbitrum_setFinalityData` | Update finality (wrapped `SetFinalityDataParams`) |
| `arbitrum_setConsensusSyncData` | Update sync status |
| `arbitrum_resultAtPos` | Get result at message index |
| `arbitrum_headMessageNumber` | Get head message index |
| `arbitrum_messageIndexToBlockNumber` | Convert message index to block |
| `arbitrum_blockNumberToMessageIndex` | Convert block to message index |
| `arbitrum_markFeedStart` | Mark feed start |
| `arbitrum_synced` | Check if synced |
| `arbitrum_fullSyncProgressMap` | Get sync progress map |
| `arbitrum_arbOSVersionForMessageIndex` | Get ArbOS version |

### Key Differences from nitroexecution

**digestMessage:**
```json
// arbitrum (wrapped)
{ "params": [{ "index": 1000, "message": {...}, "messageForPrefetch": null }] }

// nitroexecution (flat)
{ "params": [1000, {...}, null] }
```

**setFinalityData:**
```json
// arbitrum (wrapped)
{ "params": [{ "safeFinalityData": {...}, "finalizedFinalityData": {...} }] }

// nitroexecution (flat)
{ "params": [{...}, {...}, null] }
```

---

## Common Types

### MessageResult

| Field | Type | Description |
|-------|------|-------------|
| `blockHash` | `Hash256` | Block hash |
| `sendRoot` | `Hash256` | Merkle accumulator send root |

### RpcFinalityData

| Field | Type | Description |
|-------|------|-------------|
| `msgIdx` | `uint64` | Message index |
| `blockHash` | `Hash256` | Block hash |

---

## Error Handling

All methods return standard JSON-RPC error responses on failure:

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
| `-32000` | Internal error |
| `-32602` | Invalid params |

---

## Source of Truth

The RPC interface is defined in Nitro:
- **Interface:** `execution/interface.go`
- **Implementation must match Nitro exactly** for state consistency

---

## Related Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - Component overview and precompile reference
