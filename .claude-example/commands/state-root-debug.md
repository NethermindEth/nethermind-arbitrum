---
name: state-root-debug
description: Systematic debugging of state root mismatches between Nitro and Nethermind
---

You are debugging a state root mismatch between Nitro (Go, source of truth) and Nethermind (C#).

## Task

Help user identify and fix the cause of state root divergence.

## Information Gathering

Ask user for (if not already provided):
1. **Block number** where state roots diverge
2. **Last block** where state roots matched (if known)
3. **Transaction hash** (if specific transaction identified)
4. **Test scenario** description
5. **Any error messages** or symptoms

## Steps

1. **Use State-Root-Debugger** agent to systematically investigate

2. **Binary search for divergence point** (if block range is large):
   ```bash
   # Test specific blocks
   cd /Users/daniilankusin/RiderProjects/nethermind-arbitrum
   dotnet test --filter "Block_${BLOCK_NUM}_StateRoot"
   ```

3. **Identify the specific transaction**:
   - In the diverging block, which transaction causes mismatch?
   - Run each transaction individually if needed

4. **Narrow to the component**:
   - Precompile call?
   - L1 pricing update?
   - L2 pricing update?
   - Transaction processing?
   - Block processing?
   - Storage operation?

5. **Compare implementations** at divergence point:
   - Use Nitro-Source-Reader to understand Nitro's implementation
   - Read corresponding Nethermind implementation
   - Use Cross-Repo-Validator to find differences

6. **Classify root cause**:
   - ❌ **Gas order mismatch**: Operations in different order
   - ❌ **Storage offset error**: Reading/writing wrong slots
   - ❌ **Big integer overflow**: Math produces different results
   - ❌ **ABI encoding issue**: Encoding/decoding mismatch
   - ❌ **Non-determinism**: Random iteration order, time-based logic
   - ❌ **Missing operation**: Nethermind skips something Nitro does
   - ❌ **Extra operation**: Nethermind does something Nitro doesn't

7. **Generate debug report**:
   ```markdown
   # State Root Mismatch Debug Report

   **Report Date**: [timestamp]
   **Block**: #${BLOCK_NUM}
   **Transaction**: ${TX_INDEX}/${TOTAL_TXS} (hash: ${TX_HASH})
   **Component**: [Component name]
   **Root Cause**: [Classification]

   ## Divergence Point

   ### Nitro Implementation (Source of Truth)
   **File**: `path/to/file.go:line-range`

   ```go
   // Relevant Nitro code with line numbers
   func SomeOperation(...) {
       step1()  // Line X
       step2()  // Line Y
       step3()  // Line Z
   }
   ```

   ### Nethermind Implementation
   **File**: `path/to/file.cs:line-range`

   ```csharp
   // Relevant Nethermind code with line numbers
   public void SomeOperation(...) {
       Step2();  // Line A - ❌ OUT OF ORDER
       Step1();  // Line B
       Step3();  // Line C
   }
   ```

   ## Problem Analysis
   [Detailed explanation of why this causes state root mismatch]

   Example:
   - Nitro calls `Step1()` first, which reads state and consumes gas
   - Nethermind calls `Step2()` first, which also reads state
   - Different read order → different gas consumption → different state

   ## Fix
   **File**: `path/to/file.cs:line-range`

   ```csharp
   // Apply this change
   public void SomeOperation(...) {
       Step1();  // ← Move to match Nitro order
       Step2();
       Step3();
   }
   ```

   **Rationale**: Match Nitro's exact operation sequence.

   ## Verification Steps
   1. Apply fix to `path/to/file.cs:line`
   2. Rebuild: `dotnet build`
   3. Run test: `dotnet test --filter ${TEST_NAME}`
   4. Compare state roots with Nitro for block #${BLOCK_NUM}

   Expected result: State roots should now match.

   ## Prevention
   - Always consult Nitro source before implementing gas-affecting operations
   - Use `/compare-impl` to validate implementations
   - Add cross-client compatibility tests
   - Run `/sync-check` regularly

   ## Additional Notes
   [Any other relevant information]

   ---
   **Saved to**: `artifacts/debug/state-root-mismatch-block-${BLOCK_NUM}.md`
   ```

8. **Save debug report** to:
   ```bash
   /Users/daniilankusin/RiderProjects/nethermind-arbitrum/artifacts/debug/state-root-mismatch-block-${BLOCK_NUM}.md
   ```

9. **Provide verification commands**:
   ```bash
   # After fix, test in Nethermind
   cd /Users/daniilankusin/RiderProjects/nethermind-arbitrum
   dotnet build
   dotnet test --filter ${TEST_NAME}

   # Compare with Nitro
   cd /Users/daniilankusin/GolandProjects/arbitrum-nitro
   go test ./system_tests/ -run ${TEST_NAME}
   ```

10. **Offer next steps**:
    - "Would you like me to help implement this fix?"
    - "Should I create a test to prevent this from happening again?"
    - "Do you want to validate other components for similar issues?"

## Common Patterns

### Pattern 1: Gas Order Mismatch
**Symptom**: State roots diverge at gas-affecting operation
**Fix**: Reorder operations to match Nitro

### Pattern 2: Storage Offset Error
**Symptom**: State reads/writes produce different values
**Fix**: Correct storage offsets in `ArbosStateOffsets.cs`

### Pattern 3: Big Int Overflow
**Symptom**: Large number calculations produce different results
**Fix**: Handle overflow same as Nitro's `*big.Int`

### Pattern 4: ABI Encoding
**Symptom**: Precompile calls produce different outputs
**Fix**: Match Nitro's ABI encoding exactly

### Pattern 5: Non-Determinism
**Symptom**: State roots differ across multiple runs
**Fix**: Remove non-deterministic operations (time, random, map iteration)

## Remember
- State root mismatches are CRITICAL
- Must be debugged and fixed immediately
- Nitro is always correct (source of truth)
- Test fix thoroughly before merging
