# System Tests in Comparison Mode

This document explains which Nitro system tests are included in comparison mode testing
and why certain tests are excluded.

## Overview

Comparison mode runs Nitro system tests with two execution clients:
- **Primary**: Geth (Go) - the canonical implementation
- **Secondary**: Nethermind (C#) - the implementation under test

Both clients receive identical messages and must produce identical block hashes.
Any divergence indicates a consensus bug.

## Test Selection

Tests included in comparison mode are listed in `passing-tests.txt`. This is a
curated allowlist of tests that:
1. Produce deterministic results
2. Don't modify state after block production
3. Test consensus-critical behavior

## Excluded Tests

### RecreateState RPC Tests

**Files**: `system_tests/recreatestate_rpc_test.go`

**Excluded tests**:
- `TestRecreateStateForRPCDepthLimitExceeded`
- `TestRecreateStateForRPCMissingBlockParent`
- `TestRecreateStateForRPCNoDepthLimit`

**Reason**: These tests deliberately corrupt the database after block production
to test RPC error handling.

```go
// From recreatestate_rpc_test.go
func removeStatesFromDb(t *testing.T, bc *core.BlockChain, db ethdb.Database,
                        from, to uint64) {
    for i := from; i <= to; i++ {
        block := bc.GetBlockByNumber(i)
        db.Delete(block.Root().Bytes())  // Deletes trie nodes
    }
}
```

**Why incompatible**:

1. **Asymmetric state modification**: `removeStatesFromDb()` only affects Geth's
   local database. Nethermind runs as a separate process with its own database
   and never sees this deletion.

2. **Timing race**: Comparison may occur before or after state removal:
   ```
   Timeline A (PASS):          Timeline B (FAIL):
   Block produced              Block produced
   Comparison completes        State removed
   State removed               Comparison fails (state gone)
   ```

3. **Not consensus tests**: These test RPC depth limit error handling, not
   block production consensus. They verify that Geth returns
   `ErrDepthLimitExceeded` when querying removed state.

4. **Expected behavior divergence**: After state removal:
   - Geth: Returns `ErrDepthLimitExceeded` or similar errors
   - Nethermind: Has full state, returns normal response

**Test coverage**: These tests still run in normal (non-comparison) mode and
provide value for testing Geth's RPC error handling.

### Other Potentially Incompatible Tests

Tests that modify state post-production may have similar issues:

| Test File | Pattern | Status |
|-----------|---------|--------|
| `blocks_reexecutor_test.go` | Re-executes blocks, modifies state | Monitor |
| `staterecovery_test.go` | Removes then recovers state | Monitor |
| `triedb_race_test.go` | Concurrent state access | Monitor |

These are not currently excluded but should be monitored for flaky failures.

## Adding/Removing Tests

### To add a test to comparison mode:

1. Verify the test produces deterministic results
2. Verify it doesn't modify state after block production
3. Run the test multiple times to check for flakiness
4. Add the test name to `passing-tests.txt`

### To exclude a test:

1. Document the reason in this file
2. Remove the test name from `passing-tests.txt`
3. Create a failure analysis if the exclusion is due to a discovered issue

## Architecture Reference

```
┌─────────────────┐     message      ┌─────────────────┐
│   Geth (Go)     │ ───────────────► │ Nethermind (C#) │
│   PRIMARY       │                  │   SECONDARY     │
└────────┬────────┘                  └────────┬────────┘
         │                                    │
         ▼                                    ▼
    Block Hash A                         Block Hash B
         │                                    │
         └──────────► COMPARE ◄───────────────┘
                         │
                    Must Match!
```

For comparison to work correctly:
- Both clients must receive identical inputs
- Neither client should have state modifications that the other doesn't see
- Error handling can differ (RPC-level), but block production must match
