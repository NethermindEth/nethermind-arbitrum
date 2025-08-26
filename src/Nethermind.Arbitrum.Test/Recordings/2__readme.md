# Stylus Recording Description

This recording is created to test Stylus integration with Nethermind EVM.
It contains standard 18 blocks of Full Chain Simulation from https://github.com/NethermindEth/arbitrum-nitro-testnode followed by 5 blocks deploying contracts from https://github.com/NethermindEth/arbitrum-stylus-test.

Recording deploys 2 Solidity and 2 Stylus contracts in the following blocks:

- [blocks 1-18] Full Chain Simulation
- [block 19] Deploys `Counter` Solidity contract
- [block 20] Deploys `Call` Solidity contract
- [block 21] Deploys `Counter` Stylus contract
- [block 22] Activates `Counter` Stylus contract
- [block 23] Deploys `Call` Stylus contract
- [block 24] Activates `Call` Stylus contract
- [block 25] BatchPostingReport

`Counter` and `Call` contracts in Solidity and Stylus are functionally equivalent and have the same ABIs:

```solidity
contract Counter {
    uint256 public count;

    event LogCount(uint256 indexed count);

    function get() external view returns (uint256) {
        return count;
    }

    function emitCount() external {
        emit LogCount(count);
    }

    function inc() external {
        count++;
    }
}

contract Call {
    function executeCall(address target, bytes memory data) external returns (bytes memory) {
        (bool success, bytes memory result) = target.call(data);
        require(success, "Call failed");
        return result;
    }

    function executeDelegateCall(address target, bytes memory data) external returns (bytes memory) {
        (bool success, bytes memory result) = target.delegatecall(data);
        require(success, "Delegatecall failed");
        return result;
    }

    function executeStaticCall(address target, bytes memory data) external view returns (bytes memory) {
        (bool success, bytes memory result) = target.staticcall(data);
        require(success, "Staticcall failed");
        return result;
    }
}
```
