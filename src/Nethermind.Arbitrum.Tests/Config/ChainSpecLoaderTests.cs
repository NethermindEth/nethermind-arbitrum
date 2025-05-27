using FluentAssertions;
using Nethermind.Arbitrum.Tests.Infrastructure;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Tests.Config;

[Parallelizable(ParallelScope.All)]
public class ChainSpecLoaderTests
{
    [Test]
    public void FullChainSimulation_TestChainSpec_AlwaysMatchesFullChainSimulation()
    {
        string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "../../../../Nethermind/src/Nethermind/Chains/arbitrum-local.json");

        ChainSpec expected = FullChainSimulationChainSpecProvider.Create();
        ChainSpec actual = LoadChainSpec(path);

        expected.Should().BeEquivalentTo(actual);
    }

    private static ChainSpec LoadChainSpec(string path)
    {
        var loader = new ChainSpecFileLoader(new EthereumJsonSerializer(), LimboTraceLogger.Instance);
        var chainSpec = loader.LoadEmbeddedOrFromFile(path);
        return chainSpec;
    }
}
