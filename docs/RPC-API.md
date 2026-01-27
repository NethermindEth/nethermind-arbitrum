# Arbitrum RPC API Reference

## Overview

The Arbitrum RPC module provides communication between Nitro (consensus layer) and Nethermind (execution layer). All methods are prefixed with `arbitrum_`.

**Module:** `Arbitrum`
**Sharable:** Most methods are non-sharable (require sequential execution)

---

## Methods

### arbitrum_digestInitMessage

Initialize genesis state for the chain.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `message` | `DigestInitMessage` | Genesis initialization parameters |

**DigestInitMessage:**

| Field | Type | Description |
|-------|------|-------------|
| `initialL1BaseFee` | `UInt256` | Initial L1 base fee (must be > 0) |
| `serializedChainConfig` | `bytes` | Base64-encoded JSON chain configuration |

**Returns:** `MessageResult`

| Field | Type | Description |
|-------|------|-------------|
| `blockHash` | `Hash256` | Genesis block hash |
| `sendRoot` | `Hash256` | Initial send root (zero for genesis) |

**Example:**

```json
{
  "jsonrpc": "2.0",
  "method": "arbitrum_digestInitMessage",
  "params": [{
    "initialL1BaseFee": "0x3b9aca00",
    "serializedChainConfig": "eyJjaGFpbklkIjo0MjE2MSwi..."
  }],
  "id": 1
}
```

---

### arbitrum_digestMessage

Process a message and produce a block.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `parameters` | `DigestMessageParameters` | Message data and metadata |

**DigestMessageParameters:**

| Field | Type | Description |
|-------|------|-------------|
| `index` | `uint64` | Message index (L2 block index) |
| `message` | `MessageWithMetadata` | Message data with metadata |
| `messageForPrefetch` | `MessageWithMetadata?` | Optional next message for prefetching |

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

**Errors:**
- `CreateBlock mutex held` - Another block is being produced
- `Wrong block number` - Message index doesn't match expected

---

### arbitrum_reorg

Handle chain reorganization.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `parameters` | `ReorgParameters` | Reorganization data |

**ReorgParameters:**

| Field | Type | Description |
|-------|------|-------------|
| `number` | `uint64` | First message index after reorg point |
| `message` | `MessageWithMetadataAndBlockInfo[]` | New messages to process |
| `messageForPrefetch` | `MessageWithMetadata[]` | Messages for prefetching |

**Returns:** `MessageResult[]` - Results for each processed message

---

### arbitrum_setFinalityData

Update finality information (safe, finalized, validated blocks).

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `parameters` | `SetFinalityDataParams` | Finality data |

**SetFinalityDataParams:**

| Field | Type | Description |
|-------|------|-------------|
| `safeFinalityData` | `RpcFinalityData?` | Safe block data |
| `finalizedFinalityData` | `RpcFinalityData?` | Finalized block data |
| `validatedFinalityData` | `RpcFinalityData?` | Validated block data |

**RpcFinalityData:**

| Field | Type | Description |
|-------|------|-------------|
| `msgIdx` | `uint64` | Message index |
| `blockHash` | `Hash256` | Block hash |

**Returns:** `"OK"` on success

---

### arbitrum_setConsensusSyncData

Update consensus layer sync status.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `parameters` | `SetConsensusSyncDataParams?` | Sync data (null resets) |

**SetConsensusSyncDataParams:**

| Field | Type | Description |
|-------|------|-------------|
| `synced` | `bool` | Whether CL is synced |
| `maxMessageCount` | `uint64` | Maximum message count |
| `syncProgressMap` | `Dictionary<string, object>` | Detailed sync progress |
| `updatedAt` | `long` | Timestamp of update |

**Returns:** `"OK"` on success

---

### arbitrum_messageIndexToBlockNumber

Convert a message index to a block number.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `messageIndex` | `uint64` | Message index |

**Returns:** `long` - Corresponding block number

---

### arbitrum_blockNumberToMessageIndex

Convert a block number to a message index.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `blockNumber` | `uint64` | Block number |

**Returns:** `uint64` - Corresponding message index

---

### arbitrum_resultAtMessageIndex

Get block result at a specific message index.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `messageIndex` | `uint64` | Message index |

**Returns:** `MessageResult`

| Field | Type | Description |
|-------|------|-------------|
| `blockHash` | `Hash256` | Block hash at index |
| `sendRoot` | `Hash256` | Send root at index |

---

### arbitrum_headMessageIndex

Get the current head (latest) message index.

**Parameters:** None

**Returns:** `uint64` - Head message index

---

### arbitrum_markFeedStart

Mark feed start position for L1 price data caching.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `to` | `uint64` | Feed start position |

**Returns:** `"OK"` on success

---

### arbitrum_synced

Check if the node is considered synced.

**Parameters:** None

**Returns:** `bool` - `true` if synced, `false` otherwise

**Note:** Sync status considers both consensus layer sync state and message lag tolerance configured via `MessageLagMs`.

---

### arbitrum_fullSyncProgressMap

Get detailed sync progress information.

**Parameters:** None

**Returns:** `Dictionary<string, object>` - Sync progress map with various metrics

---

### arbitrum_arbOSVersionForMessageIndex

Get the ArbOS version for a specific message index.

**Parameters:**

| Name | Type | Description |
|------|------|-------------|
| `messageIndex` | `uint64` | Message index |

**Returns:** `uint64` - ArbOS version number

**Note:** ArbOS version determines available features:
- v30+: Stylus/WASM support
- v40+: Parent block hash processing
- v41+: Native token management
- v50+: Multi-constraint gas pricing

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

## Source of Truth

The RPC interface is defined in Nitro:
- **Interface:** `execution/interface.go`
- **Implementation must match Nitro exactly** for state consistency

---

## Related Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - Component overview and precompile reference
