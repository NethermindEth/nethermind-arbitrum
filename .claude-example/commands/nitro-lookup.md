---
name: nitro-lookup
description: Quick lookup and explanation of Nitro source code for a feature or component
---

You are looking up and explaining Nitro (Go) source code to help with Nethermind (C#) implementation.

## Task

Ask the user what to look up (if not already specified):
- Feature (e.g., "retryable redemption", "L1 pricing update")
- Function (e.g., "UpdateForBatchPosterSpending", "ProcessBlock")
- Component (e.g., "L2 pricing", "address table", "Stylus programs")
- Precompile method (e.g., "ArbSys.arbBlockNumber", "ArbRetryableTx.redeem")

## Steps

1. **Use Nitro-Source-Reader** agent to:
   - Find the relevant Go files in `/Users/daniilankusin/GolandProjects/arbitrum-nitro/`
   - Read and explain the implementation
   - Identify critical behaviors:
     - Gas-affecting operation order
     - Storage access patterns
     - Constants and magic numbers
     - Error conditions

2. **Use Implementation-Mapper** to find C# equivalent (if exists)

3. **Provide comprehensive summary**:
   ```markdown
   ## Nitro Implementation: [Feature/Component]

   **Location**: `arbos/component/file.go:line-range`

   ### What It Does
   [3-5 sentence explanation]

   ### Critical Behaviors
   **Gas Order** (operations that affect state):
   1. `Operation1()` at line X - [what it does]
   2. `Operation2()` at line Y - [what it does]
   3. ...

   **Storage Access**:
   - Reads from: [slots/keys]
   - Writes to: [slots/keys]

   **Constants**:
   - `ConstantName = value` - [purpose]

   **Error Conditions**:
   - [When errors occur]

   ### C# Implementation Notes
   **Go → C# Mappings**:
   - `*big.Int` → `UInt256`
   - `[]byte` → `byte[]` or `Span<byte>`
   - `common.Address` → `Address`
   - `vm.StateDB` → `IWorldState`

   **Watch Out For**:
   - [Specific gotchas]
   - [Performance considerations]

   ### Corresponding Nethermind File
   `Namespace/FileName.cs:line-range`

   ### Next Steps
   1. Read the C# implementation
   2. Use `/compare-impl` to validate it matches Nitro
   3. Run tests to verify behavior
   ```

4. **Offer to compare** with Nethermind implementation:
   - "Would you like me to compare this with the Nethermind implementation?"
   - "Should I validate if the C# version matches this exactly?"

## Remember
- Explain WHY, not just WHAT
- Always note operation order for gas-affecting code
- Cite specific line numbers
- Map Go patterns to C# equivalents
