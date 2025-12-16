# Goto OutOfGas Bug - Reproduction

## Contracts

https://github.com/NethermindEth/arbitrum-stylus-test/pull/14

These contracts reproduce the goto OutOfGas bug
- Deployment of Helper contract with `triggerGotoOutofgas()` function
- Deployment of Main contract that calls Helper with limited gas
- Triggers `goto OutOfGas` label in StylusCall, causing state changes to persist incorrectly

---

## Commands

### 1. Deploy Helper Contract
```bash
cd helper-contract-3
cargo build --release --target wasm32-unknown-unknown
cargo stylus deploy --private-key KEY --endpoint http://localhost:8547
HELPER=0x...
```

### 2. Deploy Main Contract
```bash
cd out-of-gas
cargo build --release --target wasm32-unknown-unknown
cargo stylus deploy --private-key KEY --endpoint http://localhost:8547
GOTO_MAIN=0x...
```

### 3. Set helper address
```bash
cast send $GOTO_MAIN "setHelper(address)" $HELPER \
    --private-key KEY \
    --rpc-url http://localhost:8547
```

### 4. Check initial state (should be 0,0)
```bash
cast call $HELPER "getCounter()" --rpc-url http://localhost:8547
cast call $HELPER "getData1()" --rpc-url http://localhost:8547
```

### 5. Trigger the bug
```bash
cast send $GOTO_MAIN "testGotoOutofgasBug()" \
    --private-key KEY \
    --rpc-url http://localhost:8547 \
    --gas-limit 200000
```

### 6. Check state after (BUG: 1,1 | FIXED: 0,0)
```bash
cast call $HELPER "getCounter()" --rpc-url http://localhost:8547
cast call $HELPER "getData1()" --rpc-url http://localhost:8547
```

## Expected Behavior
- Counter and Data1 should remain 0,0 (state reverted due to OutOfGas)

## Buggy Behavior
- Counter and Data1 become 1,1 (state changes persist despite OutOfGas)
