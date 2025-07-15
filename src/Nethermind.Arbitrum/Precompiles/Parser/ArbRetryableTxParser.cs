using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbRetryableTxParser : IArbitrumPrecompile<ArbRetryableTxParser>
{
    public static readonly ArbRetryableTxParser Instance = new();
    public static Address Address { get; } = ArbRetryableTx.Address;

    private static readonly uint _redeemId;
    private static readonly uint _getLifetimeId;
    private static readonly uint _getTimeoutId;
    private static readonly uint _keepaliveId;
    private static readonly uint _getBeneficiaryId;
    private static readonly uint _cancelId;
    private static readonly uint _getCurrentRedeemerId;
    private static readonly uint _submitRetryableId;

    static ArbRetryableTxParser()
    {
        _redeemId = MethodIdHelper.GetMethodId("redeem(bytes32)");
        _getLifetimeId = MethodIdHelper.GetMethodId("getLifetime()");
        _getTimeoutId = MethodIdHelper.GetMethodId("getTimeout(bytes32)");
        _keepaliveId = MethodIdHelper.GetMethodId("keepalive(bytes32)");
        _getBeneficiaryId = MethodIdHelper.GetMethodId("getBeneficiary(bytes32)");
        _cancelId = MethodIdHelper.GetMethodId("cancel(bytes32)");
        _getCurrentRedeemerId = MethodIdHelper.GetMethodId("getCurrentRedeemer()");
        _submitRetryableId = MethodIdHelper.GetMethodId("submitRetryable(bytes32,uint256,uint256,uint256,uint256,uint64,uint256,address,address,address,bytes)");
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (methodId == _redeemId)
        {
            return Redeem(context, inputDataSpan);
        }

        if (methodId == _getLifetimeId)
        {
            return GetLifetime(context, inputDataSpan);
        }

        if (methodId == _getTimeoutId)
        {
            return GetTimeout(context, inputDataSpan);
        }

        if (methodId == _keepaliveId)
        {
            return KeepAlive(context, inputDataSpan);
        }

        if (methodId == _getBeneficiaryId)
        {
            return GetBeneficiary(context, inputDataSpan);
        }

        if (methodId == _cancelId)
        {
            return Cancel(context, inputDataSpan);
        }

        if (methodId == _getCurrentRedeemerId)
        {
            return GetCurrentRedeemer(context, inputDataSpan);
        }

        if (methodId == _submitRetryableId)
        {
            return SubmitRetryable(context, inputDataSpan);
        }

        throw new ArgumentException($"Invalid precompile method ID: {methodId}");
    }

    private static byte[] Redeem(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        return ArbRetryableTx.Redeem(context, ticketId).BytesToArray();
    }

    private static byte[] GetLifetime(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        return ArbRetryableTx.GetLifetime(context).ToBigEndian();
    }

    private static byte[] GetTimeout(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        return ArbRetryableTx.GetTimeout(context, ticketId).ToBigEndian();
    }

    private static byte[] KeepAlive(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        return ArbRetryableTx.KeepAlive(context, ticketId).ToBigEndian();
    }

    private static byte[] GetBeneficiary(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        Address beneficiary = ArbRetryableTx.GetBeneficiary(context, ticketId);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        beneficiary.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);

        return abiEncodedResult;
    }

    private static byte[] Cancel(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        Hash256 ticketId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);

        ArbRetryableTx.Cancel(context, ticketId);

        return [];
    }

    private static byte[] GetCurrentRedeemer(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        Address currentRedeemer = ArbRetryableTx.GetCurrentRedeemer(context);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        currentRedeemer.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);

        return abiEncodedResult;
    }

    private static byte[] SubmitRetryable(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        Hash256 requestId = ArbitrumBinaryReader.ReadHash256OrFail(ref inputData);
        UInt256 l1BaseFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);
        UInt256 deposit = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);
        UInt256 callvalue = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);
        UInt256 gasFeeCap = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);

        ReadOnlySpan<byte> gasLimitBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong gasLimit = gasLimitBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        UInt256 maxSubmissionFee = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);

        ReadOnlySpan<byte> feeRefundAddressBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address feeRefundAddress = new(feeRefundAddressBytes[(Hash256.Size - Address.Size)..]);

        ReadOnlySpan<byte> beneficiaryBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address beneficiary = new(beneficiaryBytes[(Hash256.Size - Address.Size)..]);

        ReadOnlySpan<byte> retryToBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address retryTo = new(retryToBytes[(Hash256.Size - Address.Size)..]);

        ArbRetryableTx.SubmitRetryable(
            context, requestId, l1BaseFee, deposit, callvalue,
            gasFeeCap, gasLimit, maxSubmissionFee, feeRefundAddress,
            beneficiary, retryTo, inputData.ToArray()
        );

        return [];
    }
}
