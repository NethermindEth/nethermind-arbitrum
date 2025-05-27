using FluentAssertions;
using Nethermind.Api;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Tests.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using NSubstitute;

namespace Nethermind.Arbitrum.Tests.Arbos;

public class ArbosGenesisLoaderTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ArbitrumGenesisLoader_FullChainSimulation_ProducesCorrectHash()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();

        INethermindApi? api = Substitute.For<INethermindApi>();
        api.LogManager.Returns(Logger);
        api.ChainSpec.Returns(chainSpec);
        api.SpecProvider.Returns(FullChainSimulationSpecProvider.Instance);

        WorldState provider = new(new TrieStore(new MemDb(), Logger), new MemDb(), Logger);
        IMainProcessingContext? processingContext = Substitute.For<IMainProcessingContext>();
        processingContext.WorldState.Returns(provider);
        api.MainProcessingContext.Returns(processingContext);

        var genesisLoader = new ArbitrumGenesisLoader(api.ChainSpec, api.SpecProvider!, api.MainProcessingContext!.WorldState, api.LogManager);
        var genesisBlock = genesisLoader.Load();

        genesisBlock.Hash.Should().Be(new Hash256("0xbd9f2163899efb7c39f945c9a7744b2c3ff12cfa00fe573dcb480a436c0803a8"));
    }
}
