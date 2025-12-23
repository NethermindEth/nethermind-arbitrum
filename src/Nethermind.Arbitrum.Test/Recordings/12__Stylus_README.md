# Refund Propagation Bug - Reproduction

## Contracts

https://github.com/NethermindEth/arbitrum-stylus-test/pull/15

These contracts reproduce the refund propagation bug where gas refunds from a reverted nested call are incorrectly propagated to the parent frame.

- RefundGeneratorEVM: Solidity EVM contract that generates ~48k gas refunds then reverts
- NestedCaller: Stylus contract that calls RefundGeneratorEVM and handles the revert

---

## Commands

### 1. Deploy RefundGeneratorEVM Contract
```bash
cd solidity
npx hardhat run scripts/deploy.ts --network localdev
REFUND_GEN_EVM=0x...
```

### 2. Initialize RefundGeneratorEVM
```bash
cast send $REFUND_GEN_EVM "initialize()" \
    --private-key KEY \
    --rpc-url http://localhost:8547
```

### 3. Verify initialization (should be false)
```bash
cast call $REFUND_GEN_EVM "isSlotZero(uint8)" 0 --rpc-url http://localhost:8547
```

### 4. Deploy NestedCaller Contract
```bash
cd nested-caller
cargo build --release --target wasm32-unknown-unknown
cargo stylus deploy --private-key KEY --endpoint http://localhost:8547
NESTED_CALLER=0x...
```

### 5. Link contracts
```bash
cast send $NESTED_CALLER "setRefundGenerator(address)" $REFUND_GEN_EVM \
    --private-key KEY \
    --rpc-url http://localhost:8547
```

### 6. Trigger the bug
```bash
cast send $NESTED_CALLER "testRefundPropagationBug()" \
    --private-key KEY \
    --rpc-url http://localhost:8547 \
    --gas-limit 500000
```
