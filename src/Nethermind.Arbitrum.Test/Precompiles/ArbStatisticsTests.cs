// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Int256;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbStatisticsTests
{
    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbStatistics.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileTestAbiHelpers.GetMethodId("getStats()"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbStatistics.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbStatistics.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("getStats()").Should().Be(Solgen.ArbStatistics.Methods.GetStats);
    }

    [Test]
    public void GetStats_AtGenesisBlock_ReturnsZeroBlockNumberAndClassicMetrics()
    {
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context, blockNumber: 0);

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
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context, blockNumber: (ulong)blockNumber);

        ArbStatistics.ArbStatisticsResult result = ArbStatistics.GetStats(context);

        result.BlockNumber.Should().Be((ulong)blockNumber);
    }

    [Test]
    public void GetStats_Always_ReturnsZeroesForClassicPreNitroMetrics()
    {
        // All Classic* fields are hardcoded to zero because Arbitrum Classic (pre-Nitro) state is no longer tracked.
        using IDisposable scope = PrecompileTestContextBuilder.CreateAtBlock(out PrecompileTestContextBuilder context, blockNumber: 9_999);

        ArbStatistics.ArbStatisticsResult result = ArbStatistics.GetStats(context);

        result.ClassicNumAccounts.Should().Be(UInt256.Zero);
        result.ClassicStorageSum.Should().Be(UInt256.Zero);
        result.ClassicGasSum.Should().Be(UInt256.Zero);
        result.ClassicNumTxes.Should().Be(UInt256.Zero);
        result.ClassicNumContracts.Should().Be(UInt256.Zero);
    }
}
