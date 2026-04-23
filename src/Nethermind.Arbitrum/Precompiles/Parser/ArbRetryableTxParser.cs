// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbRetryableTxParser : IArbitrumPrecompile<ArbRetryableTxParser>
{
    public static readonly ArbRetryableTxParser Instance = new();

    public static Address Address { get; } = ArbRetryableTx.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbRetryableTx.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint _redeemId = Solgen.ArbRetryableTx.Methods.Redeem;
    private const uint _getLifetimeId = Solgen.ArbRetryableTx.Methods.GetLifetime;
    private const uint _getTimeoutId = Solgen.ArbRetryableTx.Methods.GetTimeout;
    private const uint _keepaliveId = Solgen.ArbRetryableTx.Methods.Keepalive;
    private const uint _getBeneficiaryId = Solgen.ArbRetryableTx.Methods.GetBeneficiary;
    private const uint _cancelId = Solgen.ArbRetryableTx.Methods.Cancel;
    private const uint _getCurrentRedeemerId = Solgen.ArbRetryableTx.Methods.GetCurrentRedeemer;
    private const uint _submitRetryableId = Solgen.ArbRetryableTx.Methods.SubmitRetryable;

    static ArbRetryableTxParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _redeemId, Redeem },
            { _getLifetimeId, GetLifetime },
            { _getTimeoutId, GetTimeout },
            { _keepaliveId, KeepAlive },
            { _getBeneficiaryId, GetBeneficiary },
            { _cancelId, Cancel },
            { _getCurrentRedeemerId, GetCurrentRedeemer },
            { _submitRetryableId, SubmitRetryable },
        }.ToFrozenDictionary();
    }

    private static byte[] Redeem(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_redeemId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        Hash256 retryTxHash = ArbRetryableTx.Redeem(context, ticketId);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            retryTxHash
        );
    }

    private static byte[] GetLifetime(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        return ArbRetryableTx.GetLifetime(context).ToBigEndian();
    }

    private static byte[] GetTimeout(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getTimeoutId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        return ArbRetryableTx.GetTimeout(context, ticketId).ToBigEndian();
    }

    private static byte[] KeepAlive(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_keepaliveId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        return ArbRetryableTx.KeepAlive(context, ticketId).ToBigEndian();
    }

    private static byte[] GetBeneficiary(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_getBeneficiaryId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        Address beneficiary = ArbRetryableTx.GetBeneficiary(context, ticketId);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            beneficiary
        );
    }

    private static byte[] Cancel(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_cancelId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Hash256 ticketId = new((byte[])decoded[0]);
        ArbRetryableTx.Cancel(context, ticketId);
        return [];
    }

    private static byte[] GetCurrentRedeemer(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address currentRedeemer = ArbRetryableTx.GetCurrentRedeemer(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getCurrentRedeemerId].AbiFunctionDescription.GetReturnInfo().Signature,
            currentRedeemer
        );
    }

    private static byte[] SubmitRetryable(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_submitRetryableId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
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
