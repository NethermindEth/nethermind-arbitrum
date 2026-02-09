// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Config;

[Parallelizable(ParallelScope.All)]
public class ChainSpecLoaderTests
{
    [Test]
    public void FullChainSimulation_TestChainSpec_AlwaysMatchesFullChainSimulation()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "../../../../Nethermind.Arbitrum/Properties/chainspec/arbitrum-local.json");

        ChainSpec expected = FullChainSimulationChainSpecProvider.Create();
        ChainSpec actual = LoadChainSpec(path);

        actual.Should().BeEquivalentTo(expected);
    }

    private static ChainSpec LoadChainSpec(string path)
    {
        var loader = new ChainSpecFileLoader(new EthereumJsonSerializer(), LimboTraceLogger.Instance);
        var chainSpec = loader.LoadEmbeddedOrFromFile(path);
        return chainSpec;
    }
}
