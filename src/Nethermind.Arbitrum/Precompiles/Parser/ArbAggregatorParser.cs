using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbAggregatorParser : IArbitrumPrecompile<ArbAggregatorParser>
{
    public static readonly ArbAggregatorParser Instance = new();
    private static readonly uint _addBatchPosterId = PrecompileHelper.GetMethodId("addBatchPoster(address)");
    private static readonly uint _getBatchPostersId = PrecompileHelper.GetMethodId("getBatchPosters()");
    private static readonly uint _getDefaultAggregatorId = PrecompileHelper.GetMethodId("getDefaultAggregator()");
    private static readonly uint _getFeeCollectorId = PrecompileHelper.GetMethodId("getFeeCollector(address)");

    private static readonly uint _getPreferredAggregatorId = PrecompileHelper.GetMethodId("getPreferredAggregator(address)");
    private static readonly uint _getTxBaseFeeId = PrecompileHelper.GetMethodId("getTxBaseFee(address)");
    private static readonly uint _setFeeCollectorId = PrecompileHelper.GetMethodId("setFeeCollector(address,address)");
    private static readonly uint _setTxBaseFeeId = PrecompileHelper.GetMethodId("setTxBaseFee(address,uint256)");

    public static Address Address { get; } = ArbAggregator.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbAggregator.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    static ArbAggregatorParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _getPreferredAggregatorId, GetPreferredAggregator },
            { _getDefaultAggregatorId, GetDefaultAggregator },
            { _getBatchPostersId, GetBatchPosters },
            { _addBatchPosterId, AddBatchPoster },
            { _getFeeCollectorId, GetFeeCollector },
            { _setFeeCollectorId, SetFeeCollector },
            { _getTxBaseFeeId, GetTxBaseFee },
            { _setTxBaseFeeId, SetTxBaseFee },
        }.ToFrozenDictionary();
    }

    private static byte[] AddBatchPoster(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_addBatchPosterId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address newBatchPoster = (Address)decoded[0];
        ArbAggregator.AddBatchPoster(context, newBatchPoster);
        return [];
    }

    private static byte[] GetBatchPosters(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] batchPosters = ArbAggregator.GetBatchPosters(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getBatchPostersId].AbiFunctionDescription.GetReturnInfo().Signature,
            [batchPosters]
        );
    }

    private static byte[] GetDefaultAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address defaultAggregator = ArbAggregator.GetDefaultAggregator(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getDefaultAggregatorId].AbiFunctionDescription.GetReturnInfo().Signature,
            defaultAggregator
        );
    }

    private static byte[] GetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_getFeeCollectorId].AbiFunctionDescription;

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

    private static byte[] GetPreferredAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_getPreferredAggregatorId].AbiFunctionDescription;

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

    private static byte[] GetTxBaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address aggregator = (Address)decoded[0];
        UInt256 txBaseFee = ArbAggregator.GetTxBaseFee(context, aggregator);
        return txBaseFee.ToBigEndian();
    }

    private static byte[] SetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_setFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address batchPoster = (Address)decoded[0];
        Address newFeeCollector = (Address)decoded[1];
        ArbAggregator.SetFeeCollector(context, batchPoster, newFeeCollector);
        return [];
    }

    private static byte[] SetTxBaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_setTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address aggregator = (Address)decoded[0];
        UInt256 feeInL1Gas = (UInt256)decoded[1];
        ArbAggregator.SetTxBaseFee(context, aggregator, feeInL1Gas);
        return [];
    }
}
