# Arbitrum Precompiles Reference

## Overview

Precompiles are special smart contracts at fixed addresses that provide system-level functionality. They are implemented natively in the execution client for performance and security.

## Precompile Addresses

| Address | Name | Description |
|---------|------|-------------|
| `0x64` | **ArbSys** | System info, L2-to-L1 messaging, block info |
| `0x65` | **ArbInfo** | Account balance and code queries |
| `0x66` | **ArbAddressTable** | Address compression for gas efficiency |
| `0x67` | **ArbBLS** | BLS signatures (deprecated) |
| `0x68` | **ArbFunctionTable** | Function signature registry |
| `0x69` | **ArbTest** | Testing utilities (dev only) |
| `0x6b` | **ArbOwnerPublic** | Public chain owner queries |
| `0x6c` | **ArbGasInfo** | Gas pricing information |
| `0x6d` | **ArbAggregator** | Batch poster configuration |
| `0x6e` | **ArbRetryableTx** | Retryable ticket management |
| `0x6f` | **ArbStatistics** | Chain statistics |
| `0x70` | **ArbOwner** | Chain owner operations (admin only) |
| `0x71` | **ArbWasm** | Stylus program deployment & activation |
| `0x72` | **ArbWasmCache** | WASM caching control |
| `0x73` | **ArbNativeTokenManager** | Native token minting/burning |
| `0xff` | **ArbDebug** | Debug utilities |

### Virtual Contracts (Not True Precompiles)

| Address | Name | Description |
|---------|------|-------------|
| `0xc8` | **NodeInterface** | Node query interface |
| `0xc9` | **NodeInterfaceDebug** | Debug node queries |

## Implementation Pattern

Each precompile has two files:

1. **`ArbXxx.cs`** - Business logic implementation
   - Static methods matching ABI signatures
   - Gas accounting
   - State mutations via `ArbitrumPrecompileExecutionContext`

2. **`ArbXxxParser.cs`** - ABI encoding/decoding
   - Method ID mapping (function selectors)
   - Input/output serialization
   - Frozen dictionary dispatch for performance

**Example structure:**
```
src/Nethermind.Arbitrum/Precompiles/
├── ArbSys.cs              # Business logic
├── ArbSysParser.cs        # ABI parsing
├── ArbInfo.cs
├── ArbInfoParser.cs
└── ...
```

## Version Gating

Some precompile features are only available in certain ArbOS versions:

| Feature | Minimum ArbOS | Precompile |
|---------|---------------|------------|
| Stylus program activation | v30 | ArbWasm |
| WASM cache management | v30 | ArbWasmCache |
| Native token management | v41 | ArbNativeTokenManager |
| Multi-constraint gas info | v50 | ArbGasInfo |

## Key Precompiles

### ArbSys (0x64)

The primary system precompile for L2 operations.

**Key methods:**
- `arbBlockNumber()` - Get current L2 block number
- `arbBlockHash(uint256)` - Get L2 block hash
- `arbChainID()` - Get chain ID
- `sendTxToL1(address, bytes)` - Send L2-to-L1 message
- `withdrawEth(address)` - Withdraw ETH to L1

### ArbRetryableTx (0x6e)

Manages retryable tickets for L1-to-L2 messaging.

**Key methods:**
- `redeem(bytes32)` - Redeem a retryable ticket
- `getTimeout(bytes32)` - Get ticket timeout
- `cancel(bytes32)` - Cancel a ticket
- `keepalive(bytes32)` - Extend ticket lifetime

### ArbGasInfo (0x6c)

Gas pricing information and estimates.

**Key methods:**
- `getPricesInWei()` - Get all gas prices
- `getL1BaseFeeEstimate()` - Estimate L1 base fee
- `getGasBacklog()` - Get current gas backlog

## Adding New Precompiles

1. Create `ArbXxx.cs` with business logic
2. Create `ArbXxxParser.cs` implementing `IArbitrumPrecompile<T>`
3. Add address constant in `ArbosAddresses.cs`
4. Register in `PrecompileHelper.TryCheckMethodVisibility`
5. Write tests validating against Nitro behavior

## Source of Truth

The Nitro repository is authoritative for precompile behavior:
- **ABI definitions:** `solgen/go/localgen/localgen.go`
- **Implementation:** `precompiles/Arb*.go`
- **Registration:** `precompiles/precompile.go`

## External References

- [Arbitrum Precompiles Documentation](https://docs.arbitrum.io/build-decentralized-apps/precompiles/overview)
- [ArbSys Reference](https://docs.arbitrum.io/build-decentralized-apps/precompiles/reference#arbsys)

---

## Related Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - Component overview
- [RPC-API.md](RPC-API.md) - RPC method reference
- [DEVELOPMENT.md](DEVELOPMENT.md) - Development guide
