using Nethermind.Arbitrum.Precompiles.Parser;
using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbStatisticsParserTests
{
    private static readonly uint _getStatsId = PrecompileHelper.GetMethodId("getStats()");

    [Test]
    [TestCase(new byte[] {})]
    [TestCase(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })] // having unnecessary data should not cause any issue
    public void ParsesGetStats_ValidInputData_ReturnsStats(byte[] calldata)
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);
        long blockNumber = 3;
        genesisBlock.Header.Number = blockNumber;

        PrecompileTestContextBuilder context = new(worldState, GasSupplied: 0);
        context.WithBlockExecutionContext(genesisBlock.Header);

        bool exists = ArbStatisticsParser.PrecompileImplementation.TryGetValue(_getStatsId, out PrecompileHandler? getStats);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbStatisticsParser.PrecompileFunctionDescription[_getStatsId].AbiFunctionDescription;

        byte[] result = getStats!(context, calldata);

        ArbStatistics.ArbStatisticsResult expectedStats = new(
            BlockNumber: (UInt256)blockNumber,
            ClassicNumAccounts: 0,
            ClassicStorageSum: 0,
            ClassicGasSum: 0,
            ClassicNumTxes: 0,
            ClassicNumContracts: 0
        );

        byte[] expectedStatsBytes = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            expectedStats.BlockNumber, expectedStats.ClassicNumAccounts, expectedStats.ClassicStorageSum, expectedStats.ClassicGasSum, expectedStats.ClassicNumTxes, expectedStats.ClassicNumContracts
        );

        result.Should().BeEquivalentTo(expectedStatsBytes);
    }
}
