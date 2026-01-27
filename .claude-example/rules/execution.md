---
paths: src/Nethermind.Arbitrum/Execution/**/*.cs
---
# Execution Layer Rules

## Critical Invariants
- **State roots MUST match Nitro** - Any divergence breaks consensus
- **Gas order MATTERS** - Operations must consume gas in exact same order as Nitro
- **Deterministic execution** - Same inputs must produce identical outputs

## Nitro Reference
**Transaction processing**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/arbos/tx_processor.go`
**Block processing**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/arbos/block_processor.go`
**State transition**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/go-ethereum/core/state_transition.go`

## Key Files
- `ArbitrumTransactionProcessor.cs` - Transaction execution logic
- `ArbitrumBlockProcessor.cs` - Block production and validation

## Gas Handling
- Gas deduction order must match Nitro exactly
- Intrinsic gas calculation must match
- Refund logic must match

## After Edits
Always validate against Nitro implementation:
1. Use Cross-Repo-Validator agent
2. Run recording-based comparison tests
3. Check state root matches for test blocks
4. Verify gas usage matches exactly
