namespace Nethermind.Arbitrum.Tracing;

public enum BalanceChangeReason : byte
{
    BalanceChangeUnspecified = 0,

    // Issuance
    // BalanceIncreaseRewardMineUncle is a reward for mining an uncle block.
    BalanceIncreaseRewardMineUncle = 1,

    // BalanceIncreaseRewardMineBlock is a reward for mining a block.
    BalanceIncreaseRewardMineBlock = 2,

    // BalanceIncreaseWithdrawal is ether withdrawn from the beacon chain.
    BalanceIncreaseWithdrawal = 3,

    // BalanceIncreaseGenesisBalance is ether allocated at the genesis block.
    BalanceIncreaseGenesisBalance = 4,

    // Transaction fees
    // BalanceIncreaseRewardTransactionFee is the transaction tip increasing block builder's balance.
    BalanceIncreaseRewardTransactionFee = 5,

    // BalanceDecreaseGasBuy is spent to purchase gas for execution a transaction.
    // Part of this gas will be burnt as per EIP-1559 rules.
    BalanceDecreaseGasBuy = 6,

    // BalanceIncreaseGasReturn is ether returned for unused gas at the end of execution.
    BalanceIncreaseGasReturn = 7,

    // DAO fork
    // BalanceIncreaseDaoContract is ether sent to the DAO refund contract.
    BalanceIncreaseDaoContract = 8,

    // BalanceDecreaseDaoAccount is ether taken from a DAO account to be moved to the refund contract.
    BalanceDecreaseDaoAccount = 9,

    // BalanceChangeTransfer is ether transferred via a call.
    // it is a decrease for the sender and an increase for the recipient.
    BalanceChangeTransfer = 10,

    // BalanceChangeTouchAccount is a transfer of zero value. It is only there to
    // touch-create an account.
    BalanceChangeTouchAccount = 11,

    // BalanceIncreaseSelfdestruct is added to the recipient as indicated by a selfdestructing account.
    BalanceIncreaseSelfdestruct = 12,

    // BalanceDecreaseSelfdestruct is deducted from a contract due to self-destruct.
    BalanceDecreaseSelfdestruct = 13,

    // BalanceDecreaseSelfdestructBurn is ether that is sent to an already self-destructed
    // account within the same tx (captured at end of tx).
    // Note it doesn't account for a self-destruct which appoints itself as recipient.
    BalanceDecreaseSelfdestructBurn = 14,

    // BalanceChangeRevert is emitted when the balance is reverted back to a previous value due to call failure.
    // It is only emitted when the tracer has opted in to use the journaling wrapper (WrapWithJournal).
    BalanceChangeRevert = 15,
    BalanceChangeDuringEvmExecution = 128,
    BalanceIncreaseDeposit,
    BalanceDecreaseWithdrawToL1,
    BalanceIncreaseL1PosterFee,
    BalanceIncreaseInfraFee,
    BalanceIncreaseNetworkFee,
    BalanceChangeTransferInfraRefund,
    BalanceChangeTransferNetworkRefund,
    BalanceIncreasePrepaid,
    BalanceDecreaseUndoRefund,
    BalanceChangeEscrowTransfer,
    BalanceChangeTransferBatchPosterReward,
    BalanceChangeTransferBatchPosterRefund,
    BalanceChangeTransferRetryableExcessRefund,

    // Stylus
    BalanceChangeTransferActivationFee,
    BalanceChangeTransferActivationReimburse,

    // Native token minting and burning
    BalanceIncreaseMintNativeToken,
    BalanceDecreaseBurnNativeToken
}
