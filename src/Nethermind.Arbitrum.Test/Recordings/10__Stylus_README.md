# AccountCodeHash Bug - Reproduction (Block 75482901)

## Contract

https://github.com/NethermindEth/arbitrum-stylus-test/pull/13

This contract reproduces the AccountCodeHash bug
- Deployment of contract & automatic activation
- Triggers `account_codehash`

---

## Commands

### 1. Deploy Contract
```bash
cd account-codehash-test
cargo build --release --target wasm32-unknown-unknown
cargo stylus deploy --private-key KEY --endpoint http://localhost:8547
CONTRACT=0x...
```

### 2. Trigger the bug
```bash
TARGET=0x1111111111111111111111111111111111111111

cast send $CONTRACT "testAccountCodeHash(address)" $TARGET \
    --private-key KEY \
    --rpc-url http://localhost:8547 \
    --gas-limit 500000
```
