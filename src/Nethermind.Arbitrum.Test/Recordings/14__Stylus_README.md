# All custom stylus-related transactions from the utility repo below

### Contracts

See [Arbitrum-stylus-test](https://github.com/NethermindEth/arbitrum-stylus-test/).

The recordings are the usual 18 blocks created when spawning a full chain simulation.
And then come the stylus & solidity -related recordings from the 2 following sets of commands:

---

### Commands

### 1. Deploy Stylus & Solidity contracts
```bash
make deploy
```

### 2. Call those contracts
```bash
# Verify Stylus contracts
make stylus-verify

# Verify Solidity contracts
make solidity-verify

# Verify cross-calls from Stylus to Solidity
make stylus-to-solidity-verify
make solidity-to-stylus-verify

# Emit Stylus event
make stylus-emit-counter
```

---


PS: in particular, the block 27 includes all transactions created by the commands in point 2.