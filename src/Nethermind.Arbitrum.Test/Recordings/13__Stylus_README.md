# EvmState.GasAvailable Not Zeroed Bug - Reproduction (Block 126571472)

### Contracts

https://github.com/NethermindEth/arbitrum-stylus-test/pull/16

These contracts reproduce the OutOfGas bug from block 126571472 where `EvmState.GasAvailable` is not zeroed when EVM code fails with OutOfGas, causing leftover gas to propagate incorrectly.

- **OutOfGasEVM.sol**: Solidity contract with `getValue()` function that reads 50 storage slots (~105k gas required)
- **OOGCaller**: Stylus contract that makes TWO calls to OutOfGasEVM - first with 40k gas (OOG), second with 100k gas (succeeds)

The bug: After the first call OOGs, `EvmState.GasAvailable` is not zeroed in `ExecuteStylusEvmCallback`, allowing execution to continue when it shouldn't.

---

### Commands

### 1. Deploy OutOfGasEVM Contract
```bash
cd solidity
npx hardhat run scripts/deploy.ts --network localdev
```

Note the deployed address:
```bash
export OOG_EVM=0x...
```

### 2. Initialize OutOfGasEVM Storage
```bash
cast send $OOG_EVM "init()" \
    --private-key 0xb6b15c8cb491557369f3c7d2c287b053eb229daa9c22138887752191c9520659 \
    --rpc-url http://localhost:8547
```

### 3. Deploy OOGCaller (Stylus)
```bash
cd oog-caller
cargo build --release --target wasm32-unknown-unknown
cargo stylus deploy \
    --private-key $KEY \
    --endpoint http://localhost:8547
```

Note the deployed address:
```bash
export OOG_CALLER=0x...
```

### 4. Link Contracts
```bash
cast send $OOG_CALLER "setOogEvm(address)" $OOG_EVM \
    --private-key $KEY \
    --rpc-url http://localhost:8547
```

### 5. Trigger the Bug
```bash
cast send $OOG_CALLER "testOogNotZeroed()" \
    --private-key $KEY \
    --rpc-url http://localhost:8547 \
    --gas-limit 500000
```
