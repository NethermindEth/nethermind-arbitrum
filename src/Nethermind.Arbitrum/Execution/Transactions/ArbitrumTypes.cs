using Nethermind.Core;

namespace Nethermind.Arbitrum.Execution.Transactions;

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
    // Other constants
    public const int MaxL2MessageSize = 256 * 1024;
    public static readonly Address ArbosAddress = new("0x000000000000000000000000000000000000006F");
    // Precompiles
    // Note: Corrected addresses based on standard Arbitrum deployments. Replace if using custom values.
    public static readonly Address ArbRetryableTxAddress = new("0x000000000000000000000000000000000000006E");

    // Heartbeat disable time (Mon, 08 Aug 2022 16:00:00 GMT)
    public static readonly ulong HeartbeatsDisabledAt = 1660003200; // Unix timestamp
}
