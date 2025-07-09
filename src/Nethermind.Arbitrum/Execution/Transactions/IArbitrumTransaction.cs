using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Transactions;

public interface IArbitrumTransaction
{
    IArbitrumTransactionData GetInner();
}

public class ArbitrumTransaction<T>(T inner) : Transaction, IArbitrumTransaction
    where T : IArbitrumTransactionData
{
    public T Inner { get; } = inner;

    public IArbitrumTransactionData GetInner() => Inner;
}

public interface IArbitrumTransactionData;

public enum ArbitrumTxType : byte
{
    EthLegacy = TxType.Legacy,
    EthAccessList = TxType.AccessList,
    EthEIP1559 = TxType.EIP1559,
    EthBlob = TxType.Blob,
    EthSetCode = TxType.SetCode,

    ArbitrumDeposit = 0x64,
    ArbitrumUnsigned = 0x65,
    ArbitrumContract = 0x66,
    ArbitrumRetry = 0x68,
    ArbitrumSubmitRetryable = 0x69,
    ArbitrumInternal = 0x6A,
    ArbitrumLegacy = 0x78,
}

public enum ArbitrumL1MessageKind : byte
{
    L2Message = 3,
    EndOfBlock = 6,
    L2FundedByL1 = 7,
    RollupEvent = 8,
    SubmitRetryable = 9,
    BatchForGasEstimation = 10,
    Initialize = 11,
    EthDeposit = 12,
    BatchPostingReport = 13,
    Invalid = 255
}

public enum ArbitrumL2MessageKind : byte
{
    UnsignedUserTx = 0,
    ContractTx = 1,
    NonmutatingCall = 2, // Unimplemented
    Batch = 3,
    SignedTx = 4,
    // 5 is reserved
    Heartbeat = 6, // Deprecated
    SignedCompressedTx = 7, // Unimplemented
    // 8 is reserved for BLS signed batch
}

public static class ArbitrumConstants
{
    // Precompiles
    // Note: Corrected addresses based on standard Arbitrum deployments. Replace if using custom values.
    public static readonly Address ArbRetryableTxAddress = new("0x000000000000000000000000000000000000006E");
    public static readonly Address ArbosAddress = new("0x000000000000000000000000000000000000006F");

    // Other constants
    public const int MaxL2MessageSize = 256 * 1024;

    // Heartbeat disable time (Mon, 08 Aug 2022 16:00:00 GMT)
    public static readonly ulong HeartbeatsDisabledAt = 1660003200; // Unix timestamp
}

public record ArbitrumUnsignedTx(
    ulong ChainId,
    Address From,
    UInt256 Nonce,
    UInt256 GasFeeCap,
    ulong Gas,
    Address? To,
    UInt256 Value,
    ReadOnlyMemory<byte> Data // Calldata
) : IArbitrumTransactionData;

public record ArbitrumContractTx(
    ulong ChainId,
    Hash256 RequestId,
    Address From,
    UInt256 GasFeeCap,
    ulong Gas,
    Address? To,
    UInt256 Value,
    ReadOnlyMemory<byte> Data // Calldata
) : IArbitrumTransactionData;

public record ArbitrumDepositTx(
    ulong ChainId,
    Hash256 L1RequestId,
    Address From, // L1 sender
    Address To, // L2 recipient
    UInt256 Value
) : IArbitrumTransactionData;

public record ArbitrumSubmitRetryableTx(
    ulong ChainId,
    Hash256 RequestId,
    Address From, // L1 sender
    UInt256 L1BaseFee,
    UInt256 DepositValue,
    UInt256 GasFeeCap,
    ulong Gas,
    Address? RetryTo,
    UInt256 RetryValue,
    Address Beneficiary,
    UInt256 MaxSubmissionFee,
    Address FeeRefundAddr,
    ReadOnlyMemory<byte> RetryData
) : IArbitrumTransactionData;

public record ArbitrumInternalTx(
    ulong ChainId,
    UInt256 BatchTimestamp,
    Address BatchPosterAddress,
    ulong BatchNumber,
    ulong BatchDataGas,
    UInt256 L1BaseFee
) : IArbitrumTransactionData;

public record ArbitrumRetryTx(
    ulong ChainId,
    ulong Nonce,
    Address From,
    UInt256 GasFeeCap,
    ulong Gas,
    Address? To, // null means contract creation
    UInt256 Value,
    ReadOnlyMemory<byte> Data, // Calldata
    Hash256 TicketId,
    Address RefundTo,
    UInt256 MaxRefund, // the maximum refund sent to RefundTo (the rest goes to From)
    UInt256 SubmissionFeeRefund // the submission fee to refund if successful (capped by MaxRefund)
) : IArbitrumTransactionData
{
    public ulong Gas { get; set; } = Gas;
}
