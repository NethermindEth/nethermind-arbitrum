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

    public static string Abi => ArbAggregator.Abi;

    public static IReadOnlyDictionary<uint, AbiFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(Abi);

    private static readonly uint _getPreferredAggregatorId = MethodIdHelper.GetMethodId("getPreferredAggregator(address)");
    private static readonly uint _getDefaultAggregatorId = MethodIdHelper.GetMethodId("getDefaultAggregator()");
    private static readonly uint _getBatchPostersId = MethodIdHelper.GetMethodId("getBatchPosters()");
    private static readonly uint _addBatchPosterId = MethodIdHelper.GetMethodId("addBatchPoster(address)");
    private static readonly uint _getFeeCollectorId = MethodIdHelper.GetMethodId("getFeeCollector(address)");
    private static readonly uint _setFeeCollectorId = MethodIdHelper.GetMethodId("setFeeCollector(address,address)");
    private static readonly uint _getTxBaseFeeId = MethodIdHelper.GetMethodId("getTxBaseFee(address)");
    private static readonly uint _setTxBaseFeeId = MethodIdHelper.GetMethodId("setTxBaseFee(address,uint256)");

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
        ReadOnlySpan<byte> addressBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address address = new(addressBytes[(Hash256.Size - Address.Size)..]);

        (Address prefAgg, bool isDefault) = ArbAggregator.GetPreferredAggregator(context, address);

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_getPreferredAggregatorId].GetReturnInfo().Signature,
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
            PrecompileFunctions[_getBatchPostersId].GetReturnInfo().Signature,
            [batchPosters]
        );

        return abiEncodedResult;
    }

    private static byte[] AddBatchPoster(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> newBatchPosterBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address newBatchPoster = new(newBatchPosterBytes[(Hash256.Size - Address.Size)..]);

        ArbAggregator.AddBatchPoster(context, newBatchPoster);

        // No return value for this function
        return [];
    }

    private static byte[] GetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> batchPosterBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address batchPoster = new(batchPosterBytes[(Hash256.Size - Address.Size)..]);

        Address feeCollector = ArbAggregator.GetFeeCollector(context, batchPoster);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        feeCollector.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);

        return abiEncodedResult;
    }

    private static byte[] SetFeeCollector(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> batchPosterBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address batchPoster = new(batchPosterBytes[(Hash256.Size - Address.Size)..]);

        ReadOnlySpan<byte> newFeeCollectorBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address newFeeCollector = new(newFeeCollectorBytes[(Hash256.Size - Address.Size)..]);

        ArbAggregator.SetFeeCollector(context, batchPoster, newFeeCollector);

        // No return value for this function
        return [];
    }

    private static byte[] GetTxBaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> aggregatorBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address aggregator = new(aggregatorBytes[(Hash256.Size - Address.Size)..]);

        UInt256 txBaseFee = ArbAggregator.GetTxBaseFee(context, aggregator);

        return txBaseFee.ToBigEndian();
    }

    private static byte[] SetTxBaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> aggregatorBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address aggregator = new(aggregatorBytes[(Hash256.Size - Address.Size)..]);

        UInt256 feeInL1Gas = ArbitrumBinaryReader.ReadUInt256OrFail(ref inputData);

        ArbAggregator.SetTxBaseFee(context, aggregator, feeInL1Gas);

        // No return value for this function
        return [];
    }
}
