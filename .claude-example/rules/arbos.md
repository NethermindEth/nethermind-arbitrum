---
paths: src/Nethermind.Arbitrum/Arbos/**/*.cs
---
# ArbOS Development Rules

## What is ArbOS
- Arbitrum Operating System - manages L2 state and chain behavior
- Handles L1/L2 pricing, retryables, address tables, merkle accumulator
- Version-gated features (e.g., Stylus available from v30+)

## Source Locations
**Nethermind**: `/src/Nethermind.Arbitrum/Arbos/`
**Nitro (Source of Truth)**: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/arbos/`

## Critical Invariants
- State transitions MUST match Nitro exactly
- Storage offsets MUST match Nitro exactly
- Any deviation causes state root mismatch

## Storage Rules
- Storage slot calculations must match Nitro's `arbosState` package
- Keccak hashing for dynamic storage must use same algorithms
- Versioned storage migrations must maintain backward compatibility

## Stylus/WASM Integration
- Located in `Arbos/Stylus/` and `../Stylus/`
- Interop with Nitro WASM runtime (`/Users/daniilankusin/GolandProjects/arbitrum-nitro/arbitrator`)
- Go abstractions: `/Users/daniilankusin/GolandProjects/arbitrum-nitro/arbos/programs`

## After Edits
Always validate against Nitro:
- Use Cross-Repo-Validator agent
- Run comparison tests
- Check state root consistency
