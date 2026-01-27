---
name: sync-check
description: Check if Nitro has updates that need to be replicated in Nethermind
---

You are checking for Nitro updates that may require Nethermind implementation changes.

## Task

Check Nitro repository for recent changes and identify what needs to be synchronized to Nethermind.

## Steps

1. **Check Nitro recent commits**:
   ```bash
   cd /Users/daniilankusin/GolandProjects/arbitrum-nitro
   git fetch origin
   git log --oneline --since="7 days ago" -- precompiles/ arbos/ execution/
   ```

2. **Identify protocol-affecting changes**:
   - Precompile modifications (new methods, changed behavior)
   - ArbOS state changes (storage layout, pricing algorithms)
   - Transaction type updates
   - Gas calculation changes
   - Stylus/WASM updates

3. **For each significant change**:
   - Note the Nitro commit hash
   - Identify affected files
   - Use Implementation-Mapper to find corresponding Nethermind files
   - Check if Nethermind has corresponding update

4. **Check Nethermind status**:
   ```bash
   cd /Users/daniilankusin/RiderProjects/nethermind-arbitrum
   git log --oneline --since="7 days ago"
   ```

5. **Generate sync status report**:
   ```markdown
   ## Nitro → Nethermind Sync Status
   **Report Date**: [timestamp]
   **Nitro Checked**: Last 7 days
   **Nethermind Status**: [branch, last commit]

   ### 🔴 Critical Updates (High Priority)
   - [ ] **Precompile: ArbWasm** (Nitro commit: abc123)
     - Change: New method `extendProgramExpiry(bytes32,uint64)`
     - Files affected:
       - Nitro: `precompiles/ArbWasm.go:line`
       - Nethermind: `Precompiles/ArbWasm.cs` + `ArbWasmParser.cs`
     - Status: ❌ Not implemented in Nethermind
     - Impact: HIGH - breaks state roots if not implemented
     - Action: Implement ASAP

   ### 🟡 Feature Additions (Medium Priority)
   - [ ] **L1 Pricing: Surplus calculation update** (Nitro commit: def456)
     - Change: Modified surplus calculation formula
     - Files affected:
       - Nitro: `arbos/l1pricing/l1pricing.go:line`
       - Nethermind: `Arbos/Storage/L1PricingState.cs:line`
     - Status: ⚠️ Partially implemented (needs verification)
     - Impact: MEDIUM - affects gas pricing
     - Action: Validate and complete implementation

   ### ✅ Already Synchronized
   - [x] **Transaction encoding fix** (Nitro commit: xyz789)
     - Nethermind: Already synced in commit: uvw012

   ### 🔵 Optimizations (Low Priority)
   - [ ] **Performance: Cache optimization** (Nitro commit: ghi345)
     - Change: Added caching for address table lookups
     - Status: Optional optimization
     - Impact: LOW - performance only, no consensus effect
     - Action: Consider for future optimization

   ### Action Items Summary
   **Immediate**:
   1. Implement `ArbWasm.extendProgramExpiry` in Nethermind
   2. Add ABI parser for new method
   3. Port Nitro tests to C#

   **This Week**:
   1. Validate L1 pricing surplus calculation
   2. Run cross-client tests to verify

   **Future**:
   1. Consider implementing address table cache optimization

   ### Validation Commands
   ```bash
   # After implementing, validate
   cd /Users/daniilankusin/RiderProjects/nethermind-arbitrum
   dotnet test --filter ArbWasm

   # Compare with Nitro
   cd /Users/daniilankusin/GolandProjects/arbitrum-nitro
   go test ./precompiles/ -run TestArbWasm
   ```
   ```

6. **Prioritize by**:
   - **Critical (HIGH)**: Breaks state roots, must sync immediately
   - **Feature (MEDIUM)**: New functionality, sync soon
   - **Optimization (LOW)**: Performance only, optional

7. **Offer assistance**:
   - "Would you like me to help implement [specific update]?"
   - "Should I create a detailed implementation plan for the critical items?"

## Check Frequency

Recommend running this weekly or:
- After Nitro releases
- Before Nethermind releases
- When state root mismatches occur

## Remember
- Nitro is source of truth
- Protocol changes MUST be synced
- Optimizations are optional
- Always verify after implementation
