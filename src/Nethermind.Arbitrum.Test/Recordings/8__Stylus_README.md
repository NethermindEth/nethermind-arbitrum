# Stylus State Revert Bug - Reproduction

## Contracts

https://github.com/NethermindEth/arbitrum-stylus-test/pull/10/files

These contracts are producing blocks 18-27 in Chain Simulation
- Deployment of the 2 contracts & automatic activation
- Set helper address
- Trigger the bug

---

## Commands

### 1. Deploy Helper
```bash
cd helper-contract-2
cargo build --release --target wasm32-unknown-unknown
cargo stylus deploy --private-key KEY --endpoint http://localhost:8547
HELPER=0x...
```

### 2. Deploy State Revert
```bash
cd state-revert
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

### 4. Check initial state (should be 0,0,0)
```bash
cast call $HELPER "getState(address)" 0x0000000000000000000000000000000000000001 \
    --rpc-url http://localhost:8547
```

### 5. Trigger the bug
```bash
cast send $MAIN "testMultiSlotRevert(address)" 0x0000000000000000000000000000000000000001 \
    --private-key KEY \
    --rpc-url http://localhost:8547
```

### 6. Check state after (BUG: 1,1,1 | FIXED: 0,0,0)
```bash
cast call $HELPER "getState(address)" 0x0000000000000000000000000000000000000001 \
    --rpc-url http://localhost:8547
```
