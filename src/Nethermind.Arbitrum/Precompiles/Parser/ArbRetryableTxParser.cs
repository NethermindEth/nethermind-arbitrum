using Nethermind.Abi;
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
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "redeem",
            inputData,
            AbiType.Bytes32
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        return ArbRetryableTx.Redeem(context, ticketId).BytesToArray();
    }

    private static byte[] GetLifetime(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        return ArbRetryableTx.GetLifetime(context).ToBigEndian();
    }

    private static byte[] GetTimeout(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "getTimeout",
            inputData,
            AbiType.Bytes32
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        return ArbRetryableTx.GetTimeout(context, ticketId).ToBigEndian();
    }

    private static byte[] KeepAlive(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "keepalive",
            inputData,
            AbiType.Bytes32
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        return ArbRetryableTx.KeepAlive(context, ticketId).ToBigEndian();
    }

    private static byte[] GetBeneficiary(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "getBeneficiary",
            inputData,
            AbiType.Bytes32
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        Address beneficiary = ArbRetryableTx.GetBeneficiary(context, ticketId);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        beneficiary.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);
        return abiEncodedResult;
    }

    private static byte[] Cancel(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "cancel",
            inputData,
            AbiType.Bytes32
        );

        Hash256 ticketId = new((byte[])decoded[0]);
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
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "submitRetryable",
            inputData,
            AbiType.Bytes32,    // requestId
            AbiType.UInt256,    // l1BaseFee
            AbiType.UInt256,    // deposit
            AbiType.UInt256,    // callvalue
            AbiType.UInt256,    // gasFeeCap
            AbiType.UInt64,     // gasLimit
            AbiType.UInt256,    // maxSubmissionFee
            AbiType.Address,    // feeRefundAddress
            AbiType.Address,    // beneficiary
            AbiType.Address,    // retryTo
            AbiType.DynamicBytes // retryData
        );

        Hash256 requestId = new((byte[])decoded[0]);
        UInt256 l1BaseFee = (UInt256)decoded[1];
        UInt256 deposit = (UInt256)decoded[2];
        UInt256 callvalue = (UInt256)decoded[3];
        UInt256 gasFeeCap = (UInt256)decoded[4];
        ulong gasLimit = (ulong)decoded[5];
        UInt256 maxSubmissionFee = (UInt256)decoded[6];
        Address feeRefundAddress = (Address)decoded[7];
        Address beneficiary = (Address)decoded[8];
        Address retryTo = (Address)decoded[9];
        byte[] retryData = (byte[])decoded[10];

        ArbRetryableTx.SubmitRetryable(
            context, requestId, l1BaseFee, deposit, callvalue,
            gasFeeCap, gasLimit, maxSubmissionFee, feeRefundAddress,
            beneficiary, retryTo, retryData
        );

        return [];
    }
}
