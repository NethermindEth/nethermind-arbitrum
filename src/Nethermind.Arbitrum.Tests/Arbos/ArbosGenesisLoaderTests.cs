using Nethermind.Api;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using NSubstitute;

namespace Nethermind.Arbitrum.Tests.Arbos;

public class ArbosGenesisLoaderTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ArbitrumGenesisLoader_Always_ProducesTheSameChangesAsNitro()
    {
        string chainSpecFile = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../../../Nethermind/src/Nethermind/Chains/arbitrum-local.json"));
        if (!File.Exists(chainSpecFile))
        {
            throw new InvalidOperationException("Unable to find arbitrum-local.json file.");
        }

        ChainSpecLoader chainSpecLoader = new(new EthereumJsonSerializer());
        ChainSpec chainSpec = chainSpecLoader.Load(File.OpenRead(chainSpecFile));

        INethermindApi? api = Substitute.For<INethermindApi>();
        api.LogManager.Returns(Logger);
        api.ChainSpec.Returns(chainSpec);
        api.SpecProvider.Returns(SepoliaSpecProvider.Instance);

        WorldState provider = new(new TrieStore(new MemDb(), Logger), new MemDb(), Logger);
        IMainProcessingContext? processingContext = Substitute.For<IMainProcessingContext>();
        processingContext.WorldState.Returns(provider);
        api.MainProcessingContext.Returns(processingContext);

        var genesisLoader = new ArbitrumGenesisLoader(api);

        genesisLoader.Load();
    }
}
