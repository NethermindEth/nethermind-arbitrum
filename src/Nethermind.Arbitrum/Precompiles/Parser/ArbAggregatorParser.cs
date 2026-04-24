// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbAggregatorParser : IArbitrumPrecompile<ArbAggregatorParser>
{
    public static readonly ArbAggregatorParser Instance = new();

    public static Address Address { get; } = ArbAggregator.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbAggregator.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint GetPreferredAggregatorId = Solgen.ArbAggregator.Methods.GetPreferredAggregator;
    private const uint GetDefaultAggregatorId = Solgen.ArbAggregator.Methods.GetDefaultAggregator;
    private const uint GetBatchPostersId = Solgen.ArbAggregator.Methods.GetBatchPosters;
    private const uint AddBatchPosterId = Solgen.ArbAggregator.Methods.AddBatchPoster;
    private const uint GetFeeCollectorId = Solgen.ArbAggregator.Methods.GetFeeCollector;
    private const uint SetFeeCollectorId = Solgen.ArbAggregator.Methods.SetFeeCollector;
    private const uint GetTxBaseFeeId = Solgen.ArbAggregator.Methods.GetTxBaseFee;
    private const uint SetTxBaseFeeId = Solgen.ArbAggregator.Methods.SetTxBaseFee;

    static ArbAggregatorParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { GetPreferredAggregatorId, GetPreferredAggregator },
            { GetDefaultAggregatorId, GetDefaultAggregator },
            { GetBatchPostersId, GetBatchPosters },
            { AddBatchPosterId, AddBatchPoster },
            { GetFeeCollectorId, GetFeeCollector },
            { SetFeeCollectorId, SetFeeCollector },
            { GetTxBaseFeeId, GetTxBaseFee },
            { SetTxBaseFeeId, SetTxBaseFee },
        }.ToFrozenDictionary();
    }

    private static byte[] GetPreferredAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetPreferredAggregatorId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        (Address prefAgg, bool isDefault) = ArbAggregator.GetPreferredAggregator(context, address);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [prefAgg, isDefault]
        );
    }

    private static byte[] GetDefaultAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address defaultAggregator = ArbAggregator.GetDefaultAggregator(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetDefaultAggregatorId].AbiFunctionDescription.GetReturnInfo().Signature,
            defaultAggregator
        );
    }

    private static byte[] GetBatchPosters(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] batchPosters = ArbAggregator.GetBatchPosters(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetBatchPostersId].AbiFunctionDescription.GetReturnInfo().Signature,
            [batchPosters]
        );
    }

    private static byte[] AddBatchPoster(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[AddBatchPosterId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address newBatchPoster = (Address)decoded[0];
        ArbAggregator.AddBatchPoster(context, newBatchPoster);
        return [];
    }

    private static byte[] GetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetFeeCollectorId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address batchPoster = (Address)decoded[0];
        Address feeCollector = ArbAggregator.GetFeeCollector(context, batchPoster);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            feeCollector
        );
    }

    private static byte[] SetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address batchPoster = (Address)decoded[0];
        Address newFeeCollector = (Address)decoded[1];
        ArbAggregator.SetFeeCollector(context, batchPoster, newFeeCollector);
        return [];
    }

    private static byte[] GetTxBaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address aggregator = (Address)decoded[0];
        UInt256 txBaseFee = ArbAggregator.GetTxBaseFee(context, aggregator);
        return txBaseFee.ToBigEndian();
    }

    private static byte[] SetTxBaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address aggregator = (Address)decoded[0];
        UInt256 feeInL1Gas = (UInt256)decoded[1];
        ArbAggregator.SetTxBaseFee(context, aggregator, feeInL1Gas);
        return [];
    }
}
