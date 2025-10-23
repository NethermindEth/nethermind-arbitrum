using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbStatisticsParser : IArbitrumPrecompile<ArbStatisticsParser>
{
    public static readonly ArbStatisticsParser Instance = new();

    public static Address Address { get; } = ArbStatistics.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbStatistics.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _getStatsId = PrecompileHelper.GetMethodId("getStats()");

    static ArbStatisticsParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _getStatsId, GetStats },
        }.ToFrozenDictionary();
    }

    private static byte[] GetStats(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbStatistics.ArbStatisticsResult result = ArbStatistics.GetStats(context);

        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_getStatsId].AbiFunctionDescription;

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            result.BlockNumber, result.ClassicNumAccounts, result.ClassicStorageSum, result.ClassicGasSum, result.ClassicNumTxes, result.ClassicNumContracts
        );
    }
}
