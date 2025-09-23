using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbAggregatorParser : IArbitrumPrecompile<ArbAggregatorParser>
{
    public static readonly ArbAggregatorParser Instance = new();
    public static Address Address { get; } = ArbAggregator.Address;

    private static readonly uint _getPreferredAggregatorId = MethodIdHelper.GetMethodId("getPreferredAggregator(address)");
    private static readonly uint _getDefaultAggregatorId = MethodIdHelper.GetMethodId("getDefaultAggregator()");
    private static readonly uint _getBatchPostersId = MethodIdHelper.GetMethodId("getBatchPosters()");
    private static readonly uint _addBatchPosterId = MethodIdHelper.GetMethodId("addBatchPoster(address)");
    private static readonly uint _getFeeCollectorId = MethodIdHelper.GetMethodId("getFeeCollector(address)");
    private static readonly uint _setFeeCollectorId = MethodIdHelper.GetMethodId("setFeeCollector(address,address)");
    private static readonly uint _getTxBaseFeeId = MethodIdHelper.GetMethodId("getTxBaseFee(address)");
    private static readonly uint _setTxBaseFeeId = MethodIdHelper.GetMethodId("setTxBaseFee(address,uint256)");

    private static readonly AbiSignature GetPreferredAggregatorSignature;
    private static readonly AbiSignature GetBatchPostersSignature;

    static ArbAggregatorParser()
    {
        Dictionary<string, AbiFunctionDescription> precompileFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbAggregator.Abi);
        GetPreferredAggregatorSignature = precompileFunctions["getPreferredAggregator"].GetReturnInfo().Signature;
        GetBatchPostersSignature = precompileFunctions["getBatchPosters"].GetReturnInfo().Signature;
    }

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
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode("getPreferredAggregator", inputData, AbiType.Address);
        Address address = (Address)decoded[0];
        (Address prefAgg, bool isDefault) = ArbAggregator.GetPreferredAggregator(context, address);

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            GetPreferredAggregatorSignature,
            [prefAgg, isDefault]
        );

        return abiEncodedResult;
    }

    private static byte[] GetDefaultAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address defaultAggregator = ArbAggregator.GetDefaultAggregator(context);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        defaultAggregator.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);

        return abiEncodedResult;
    }

    private static byte[] GetBatchPosters(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] batchPosters = ArbAggregator.GetBatchPosters(context);

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            GetBatchPostersSignature,
            [batchPosters]
        );

        return abiEncodedResult;
    }

    private static byte[] AddBatchPoster(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode("addBatchPoster", inputData, AbiType.Address);
        Address newBatchPoster = (Address)decoded[0];
        ArbAggregator.AddBatchPoster(context, newBatchPoster);
        return [];
    }

    private static byte[] GetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode("getFeeCollector", inputData, AbiType.Address);
        Address batchPoster = (Address)decoded[0];
        Address feeCollector = ArbAggregator.GetFeeCollector(context, batchPoster);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        feeCollector.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);
        return abiEncodedResult;
    }

    private static byte[] SetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode("setFeeCollector", inputData, AbiType.Address, AbiType.Address);
        Address batchPoster = (Address)decoded[0];
        Address newFeeCollector = (Address)decoded[1];
        ArbAggregator.SetFeeCollector(context, batchPoster, newFeeCollector);
        return [];
    }

    private static byte[] GetTxBaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode("getTxBaseFee", inputData, AbiType.Address);
        Address aggregator = (Address)decoded[0];
        UInt256 txBaseFee = ArbAggregator.GetTxBaseFee(context, aggregator);
        return txBaseFee.ToBigEndian();
    }

    private static byte[] SetTxBaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode("setTxBaseFee", inputData, AbiType.Address, AbiType.UInt256);
        Address aggregator = (Address)decoded[0];
        UInt256 feeInL1Gas = (UInt256)decoded[1];
        ArbAggregator.SetTxBaseFee(context, aggregator, feeInL1Gas);
        return [];
    }
}
