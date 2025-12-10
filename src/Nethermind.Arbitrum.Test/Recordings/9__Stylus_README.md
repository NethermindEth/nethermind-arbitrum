# Native Precompile Failure Restore Bug - Reproduction

## Contracts

https://github.com/NethermindEth/arbitrum-stylus-test/pull/12

These contracts reproduce the native precompile failure bug
- Deployment of the 2 contracts & automatic activation
- Set helper address
- Trigger the bug

---

## Commands

### 1. Deploy Helper
```bash
cd helper-contract-3
cargo build --release --target wasm32-unknown-unknown
cargo stylus deploy --private-key KEY --endpoint http://localhost:8547
HELPER=0x...
```

### 2. Deploy Main Contract
```bash
cd native-precompile-failure-restore
cargo build --release --target wasm32-unknown-unknown
cargo stylus deploy --private-key KEY --endpoint http://localhost:8547
MAIN=0x...
```

### 3. Set helper address
```bash
cast send $MAIN "setHelper(address)" $HELPER \
    --private-key KEY \
    --rpc-url http://localhost:8547
```

### 4. Check initial state (should be 0,0)
```bash
cast call $HELPER "getCounter()" \
    --rpc-url http://localhost:8547
cast call $HELPER "getData1()" \
    --rpc-url http://localhost:8547
```

### 5. Trigger the bug
```bash
cast send $HELPER "getValue()" \
    --private-key KEY \
    --rpc-url http://localhost:8547 \
    --value 0 \
    --gas-limit 200000
```

### 6. Check state after (BUG: 1,1 | FIXED: 0,0)
```bash
cast call $HELPER "getCounter()" \
    --rpc-url http://localhost:8547
cast call $HELPER "getData1()" \
    --rpc-url http://localhost:8547
```
