// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbStatisticsTests
{
    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbStatistics.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileHelper.GetMethodId("getStats()"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        AbiMetadata.GetAllEventDescriptions(ArbStatistics.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        AbiMetadata.GetAllErrorDescriptions(ArbStatistics.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("getStats()").Should().Be(0xc59d4847u);
    }

    [Test]
    public void GetStats_AtGenesisBlock_ReturnsZeroBlockNumberAndClassicMetrics()
    {
        using IDisposable scope = SetupContext(blockNumber: 0, out PrecompileTestContextBuilder context);

        ArbStatistics.ArbStatisticsResult result = ArbStatistics.GetStats(context);

        result.Should().Be(new ArbStatistics.ArbStatisticsResult(
            BlockNumber: UInt256.Zero,
            ClassicNumAccounts: UInt256.Zero,
            ClassicStorageSum: UInt256.Zero,
            ClassicGasSum: UInt256.Zero,
            ClassicNumTxes: UInt256.Zero,
            ClassicNumContracts: UInt256.Zero));
    }

    // BlockExecutionContext.Number is a ulong but BlockHeader.WithNumber takes a long;
    // the test range is therefore bounded above by long.MaxValue (valid ulong values > long.MaxValue
    // are unreachable through this builder path).
    [TestCase(1L)]
    [TestCase(123_456L)]
    [TestCase(long.MaxValue)]
    public void GetStats_AtArbitraryBlock_ReturnsBlockNumberFromExecutionContext(long blockNumber)
    {
        using IDisposable scope = SetupContext(blockNumber, out PrecompileTestContextBuilder context);

        ArbStatistics.ArbStatisticsResult result = ArbStatistics.GetStats(context);

        result.BlockNumber.Should().Be((ulong)blockNumber);
    }

    [Test]
    public void GetStats_Always_ReturnsZeroesForClassicPreNitroMetrics()
    {
        // All Classic* fields are hardcoded to zero because Arbitrum Classic (pre-Nitro) state is no longer tracked.
        using IDisposable scope = SetupContext(blockNumber: 9_999, out PrecompileTestContextBuilder context);

        ArbStatistics.ArbStatisticsResult result = ArbStatistics.GetStats(context);

        result.ClassicNumAccounts.Should().Be(UInt256.Zero);
        result.ClassicStorageSum.Should().Be(UInt256.Zero);
        result.ClassicGasSum.Should().Be(UInt256.Zero);
        result.ClassicNumTxes.Should().Be(UInt256.Zero);
        result.ClassicNumContracts.Should().Be(UInt256.Zero);
    }

    private static IDisposable SetupContext(long blockNumber, out PrecompileTestContextBuilder context)
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        IDisposable scope = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        BlockHeader header = Build.A.BlockHeader.WithNumber(blockNumber).TestObject;
        context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithBlockExecutionContext(header);

        return scope;
    }
}
