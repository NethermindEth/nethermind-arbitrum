---
name: compare-impl
description: Compare Nethermind C# implementation against Nitro Go source for a specific component
---

You are comparing implementations between Nitro (Go, source of truth) and Nethermind (C#).

## Task

Ask the user which component to compare (if not already specified):
- Precompile name (e.g., "ArbSys", "ArbRetryableTx", "ArbWasm")
- Feature/component name (e.g., "L1 Pricing", "L2 Pricing", "Block Processor")
- File name (e.g., "block_processor", "tx_processor")

## Steps

1. **Use Implementation-Mapper** agent to find corresponding files in both repos:
   - Nitro (Go): `/Users/daniilankusin/GolandProjects/arbitrum-nitro/...`
   - Nethermind (C#): `/Users/daniilankusin/RiderProjects/nethermind-arbitrum/...`

2. **Use Nitro-Source-Reader** to explain Nitro's implementation:
   - What does it do?
   - What's the operation order (especially gas-affecting)?
   - What constants/magic numbers are used?
   - What storage slots are accessed?

3. **Read Nethermind implementation**

4. **Use Cross-Repo-Validator** to perform detailed comparison:
   - Gas order matches?
   - Storage offsets match?
   - Constants match?
   - Error handling matches?
   - ABI encoding matches (if precompile)?

5. **Generate comparison report**:
   ```markdown
   ## Comparison: [Component Name]

   ### Nitro (Source of Truth)
   **File**: `path/to/file.go:lines`
   **Key behaviors**:
   - [List critical behaviors]
   - [Operation order]
   - [Constants used]

   ### Nethermind (Implementation)
   **Files**: `path/to/file.cs:lines` (+ Parser if precompile)

   ### Comparison Results
   ✅ **Matches**: [List what matches]
   ❌ **Mismatches**: [List differences]
   ⚠️ **Review Needed**: [List potential issues]

   ### Recommendations
   [If mismatches found, provide specific fixes]

   ### Validation Commands
   ```bash
   # Test in Nethermind
   dotnet test --filter ComponentName

   # Compare with Nitro
   cd ../arbitrum-nitro && go test ./component/ -run TestName
   ```
   ```

6. **Offer next steps**:
   - If mismatches found: "Should I help fix these issues?"
   - If matches: "Would you like to validate with cross-client tests?"

## Remember
- Nitro is the source of truth
- Gas order must match EXACTLY
- Storage offsets must be IDENTICAL
- "Close enough" is NOT acceptable for blockchain consensus
