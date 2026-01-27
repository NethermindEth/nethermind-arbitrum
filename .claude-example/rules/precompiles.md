---
paths: src/Nethermind.Arbitrum/Precompiles/**/*.cs
---
# Precompile Development Rules

## Nitro Source of Truth
Always check Nitro Go implementation FIRST before making changes.

**ABI Source**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/solgen/go/localgen/localgen.go`
- Look for `var {precompile-name}MetaData = &bind.MetaData{...}` pattern

**Implementation**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/precompiles/Arb*.go`

**Registration**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/precompiles/precompile.go:Precompiles()`

## Nethermind Two-File Pattern
Each precompile requires two files:
1. **`ArbXxx.cs`** - Business logic implementation
2. **`ArbXxxParser.cs`** - ABI encoding/decoding

**Location**: `/src/Nethermind.Arbitrum/Precompiles/`
**Registration**: Manual in `PrecompileHelper.cs`

## Validation Requirements
After any precompile edit, verify:
1. Method IDs (4-byte selectors) match Nitro
2. Input/output encoding is identical
3. Gas costs match exactly
4. Gas operation ORDER matches (critical for determinism)
5. Error messages match for state consistency

## Common Issues
- Big-endian encoding required for ABI parameters
- Padding must match Nitro exactly (32-byte alignment)
- Return value encoding must match Solidity ABI spec
