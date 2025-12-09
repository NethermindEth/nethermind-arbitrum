# Stylus State Revert Bug - Reproduction

## Contracts

https://github.com/NethermindEth/arbitrum-stylus-test/pull/11

Deploy 2 standard contracts with additional methods marked as 'payable' to tranfer value together with call:
- stylus counter contract
- stylus call contract

---

## Commands

### 1. Deploy Stylus Call contract
```bash
cd arbitrum-stylus-test
make stylus-deploy-call
```

### 2. Deploy Stylus Counter contract
```bash
cd arbitrum-stylus-test
make stylus-deploy-counter
```

### 3. Test call (replicated in test)
```bash
cast send --rpc-url 'http://localhost:8547' --private-key 0xdc04c5399f82306ec4b4d654a342f40e2e0620fe39950d967e1e574b32d4dd36 --value 66 0x517a626131162f32e08a788a60491bb61cf5928d "executeCallPayable(address,bytes)" 0x9df23e34ac13a7145eba1164660e701839197b1b 5d2c7ee6
```