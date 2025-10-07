using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbAggregatorParser : IArbitrumPrecompile<ArbAggregatorParser>
{
    public static readonly ArbAggregatorParser Instance = new();

    public static Address Address { get; } = ArbAggregator.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbAggregator.Abi);

    private static readonly uint _getPreferredAggregatorId = PrecompileHelper.GetMethodId("getPreferredAggregator(address)");
    private static readonly uint _getDefaultAggregatorId = PrecompileHelper.GetMethodId("getDefaultAggregator()");
    private static readonly uint _getBatchPostersId = PrecompileHelper.GetMethodId("getBatchPosters()");
    private static readonly uint _addBatchPosterId = PrecompileHelper.GetMethodId("addBatchPoster(address)");
    private static readonly uint _getFeeCollectorId = PrecompileHelper.GetMethodId("getFeeCollector(address)");
    private static readonly uint _setFeeCollectorId = PrecompileHelper.GetMethodId("setFeeCollector(address,address)");
    private static readonly uint _getTxBaseFeeId = PrecompileHelper.GetMethodId("getTxBaseFee(address)");
    private static readonly uint _setTxBaseFeeId = PrecompileHelper.GetMethodId("setTxBaseFee(address,uint256)");

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        return methodId switch
        {
            _ when methodId == _getPreferredAggregatorId => GetPreferredAggregator(context, inputDataSpan),
            _ when methodId == _getDefaultAggregatorId => GetDefaultAggregator(context, inputDataSpan),
            _ when methodId == _getBatchPostersId => GetBatchPosters(context, inputDataSpan),
            _ when methodId == _addBatchPosterId => AddBatchPoster(context, inputDataSpan),
            _ when methodId == _getFeeCollectorId => GetFeeCollector(context, inputDataSpan),
            _ when methodId == _setFeeCollectorId => SetFeeCollector(context, inputDataSpan),
            _ when methodId == _getTxBaseFeeId => GetTxBaseFee(context, inputDataSpan),
            _ when methodId == _setTxBaseFeeId => SetTxBaseFee(context, inputDataSpan),
            _ => throw new ArgumentException($"Invalid precompile method ID: {methodId}")
        };
    }

    private static byte[] GetPreferredAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctions[_getPreferredAggregatorId].AbiFunctionDescription;

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
            PrecompileFunctions[_getDefaultAggregatorId].AbiFunctionDescription.GetReturnInfo().Signature,
            defaultAggregator
        );
    }

    private static byte[] GetBatchPosters(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] batchPosters = ArbAggregator.GetBatchPosters(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_getBatchPostersId].AbiFunctionDescription.GetReturnInfo().Signature,
            [batchPosters]
        );
    }

    private static byte[] AddBatchPoster(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_addBatchPosterId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address newBatchPoster = (Address)decoded[0];
        ArbAggregator.AddBatchPoster(context, newBatchPoster);
        return [];
    }

    private static byte[] GetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctions[_getFeeCollectorId].AbiFunctionDescription;

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
            PrecompileFunctions[_setFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctions[_getTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctions[_setTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address aggregator = (Address)decoded[0];
        UInt256 feeInL1Gas = (UInt256)decoded[1];
        ArbAggregator.SetTxBaseFee(context, aggregator, feeInL1Gas);
        return [];
    }
}
