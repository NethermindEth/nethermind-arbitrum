---
name: validate-precompile
description: Comprehensive validation of a precompile implementation against Nitro
---

You are performing comprehensive precompile validation against Nitro source of truth.

## Task

Ask user which precompile to validate (if not specified):
- ArbSys
- ArbInfo
- ArbAddressTable
- ArbGasInfo
- ArbRetryableTx
- ArbOwner
- ArbWasm
- [Other precompile name]

## Steps

1. **Use Precompile-Validator** agent to perform full validation:
   - ABI consistency (method IDs, encoding/decoding)
   - Gas cost matching
   - Behavior matching
   - Event emission matching
   - Implementation pattern (Nethermind two-file system)

2. **Extract ABI from Nitro**:
   ```bash
   cd /Users/daniilankudin/GolandProjects/arbitrum-nitro
   grep -A 100 "var Arb${PRECOMPILE}MetaData" solgen/go/localgen/localgen.go | head -150
   ```

3. **Compare with Nethermind Parser**:
   - Check `Precompiles/Arb${PRECOMPILE}Parser.cs`
   - Verify public method ID constants match
   - Validate encoding/decoding logic

4. **Compare implementations**:
   - Nitro: `precompiles/Arb${PRECOMPILE}.go`
   - Nethermind: `Precompiles/Arb${PRECOMPILE}.cs` + Parser

5. **Check tests**:
   ```bash
   # Nitro tests
   cd /Users/daniilankusin/GolandProjects/arbitrum-nitro
   go test ./precompiles/ -run Test${PRECOMPILE} -v

   # Nethermind tests
   cd /Users/daniilankusin/RiderProjects/nethermind-arbitrum
   dotnet test --filter ${PRECOMPILE}Tests
   ```

6. **Generate validation report**:
   ```markdown
   ## Precompile Validation Report: Arb${PRECOMPILE}

   **Validation Date**: [timestamp]
   **Nitro Version**: [commit hash]
   **Nethermind Version**: [commit hash]

   ### Overall Status
   ✅ PASS | ⚠️ ISSUES FOUND | ❌ FAIL

   ### ABI Validation
   **Method IDs**:
   - ✅ `methodName1`: `0xabcd1234` (matches)
   - ❌ `methodName2`: Nitro `0x5678abcd`, Nethermind `0x5678abce` (MISMATCH)

   **Encoding/Decoding**:
   - ✅ Input encoding correct
   - ⚠️ Output encoding: Missing padding at line X

   ### Gas Cost Validation
   - ✅ Base gas costs match
   - ❌ Dynamic gas order mismatch:
     - Nitro: `GetValue()` → `Check()` → `Set()`
     - Nethermind: `Check()` → `GetValue()` → `Set()`
     - **Impact**: State root divergence
     - **Fix**: Reorder operations at `ArbXxx.cs:line`

   ### Behavior Validation
   - ✅ Happy path: Identical outputs
   - ❌ Error case: Different error messages
     - Nitro: "insufficient funds"
     - Nethermind: "not enough balance"
     - **Fix**: Update at `ArbXxx.cs:line`

   ### Event Emissions
   - ✅ Event topics match
   - ✅ Event data encoding matches

   ### Implementation Pattern (Nethermind)
   - ✅ Two-file system used correctly
   - ✅ Method IDs as public constants
   - ⚠️ Errors should be nested inside precompile class

   ### Test Coverage
   - Nitro: 15 test cases
   - Nethermind: 12 test cases
   - **Missing**: Tests for edge cases X, Y, Z

   ### Critical Fixes Required
   1. **Method ID mismatch** at `ArbXxxParser.cs:line`
      ```csharp
      // Current (wrong)
      public const uint MethodId = 0x5678abce;

      // Fix to
      public const uint MethodId = 0x5678abcd;
      ```

   2. **Gas order** at `ArbXxx.cs:line`
      ```csharp
      // Fix: Move GetValue() before Check()
      var value = GetValue();  // ← Move here
      if (Check()) {
          Set(value);
      }
      ```

   3. **Error message** at `ArbXxx.cs:line`
      ```csharp
      // Change to match Nitro
      return (false, Encoding.UTF8.GetBytes("insufficient funds"));
      ```

   ### Verification Steps
   After applying fixes:
   ```bash
   # Rebuild
   dotnet build

   # Run tests
   dotnet test --filter Arb${PRECOMPILE}Tests

   # Validate against Nitro
   # (run same test scenario in both clients, compare state roots)
   ```

   ### Recommendations
   - Priority 1: Fix method ID and gas order (critical for state roots)
   - Priority 2: Update error messages for consistency
   - Priority 3: Add missing test cases
   - Priority 4: Refactor errors into nested class

   ### Sign-Off
   - [ ] All critical fixes applied
   - [ ] Tests passing
   - [ ] Cross-client validation done
   - [ ] State roots match Nitro
   ```

7. **Offer assistance**:
   - "Would you like me to help implement these fixes?"
   - "Should I create a detailed fix plan with code snippets?"

## Validation Frequency

Run validation:
- After implementing new precompile
- After modifying existing precompile
- When state root mismatches occur
- Before major releases

## Remember
- Precompiles are consensus-critical
- ABI must be byte-perfect
- Gas order must match exactly
- Tests must cover all cases
