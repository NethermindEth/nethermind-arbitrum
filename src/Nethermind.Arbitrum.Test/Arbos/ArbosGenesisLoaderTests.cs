using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Arbos;

public class ArbosGenesisLoaderTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void Load_FullChainSimulationAtV32_ProducesCorrectHash()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();
        WorldState worldState = new(new TrieStore(new MemDb(), Logger), new MemDb(), Logger);

        ArbitrumConfig arbitrumConfig = new()
        {
            Enabled = true,
            GenesisBlockNum = 0,
            InitialChainOwner = new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"),
            InitialArbOSVersion = 32,
            AllowDebugPrecompiles = true,
            DataAvailabilityCommittee = false,
            MaxCodeSize = null,
            MaxInitCodeSize = null,
        };

        DigestInitMessage digestInitMessage = FullChainSimulationInitMessage.CreateDigestInitMessage(92);
        ParsedInitMessage parsedInitMessage = new(
            chainSpec.ChainId,
            digestInitMessage.InitialL1BaseFee,
            null,
            Convert.FromBase64String(digestInitMessage.SerializedChainConfig));

        ArbitrumGenesisLoader genesisLoader = new(
            chainSpec,
            FullChainSimulationSpecProvider.Instance,
            worldState,
            parsedInitMessage,
            arbitrumConfig,
            LimboLogs.Instance);

        Block genesisBlock = genesisLoader.Load();

        genesisBlock.Hash.Should().Be(new Hash256("0xbd9f2163899efb7c39f945c9a7744b2c3ff12cfa00fe573dcb480a436c0803a8"));
    }
}
