using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Tests.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Db;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Tests.Arbos;

public class ArbosGenesisLoaderTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    [Test]
    public void ArbitrumGenesisLoader_FullChainSimulation_ProducesCorrectHash()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();
        WorldState worldState = new(new TrieStore(new MemDb(), Logger), new MemDb(), Logger);

        ArbitrumConfig arbitrumConfig = new()
        {
            Enabled = true,
            GenesisBlockNum = 0,
            InitialChainOwner = new Address("0x5E1497dD1f08C87b2d8FE23e9AAB6c1De833D927"),
            InitialArbOSVersion = 32
        };

        ParsedInitMessage initMessage = new(
            chainSpec.ChainId,
            92,
            null,
            Convert.FromHexString(
                "7b22636861696e4964223a3431323334362c22686f6d657374656164426c6f636b223a302c2264616f466f726b537570706f7274223a747275652c22656970313530426c6f636b223a302c2265697031353048617368223a22307830303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030303030222c22656970313535426c6f636b223a302c22656970313538426c6f636b223a302c2262797a616e7469756d426c6f636b223a302c22636f6e7374616e74696e6f706c65426c6f636b223a302c2270657465727362757267426c6f636b223a302c22697374616e62756c426c6f636b223a302c226d756972476c6163696572426c6f636b223a302c226265726c696e426c6f636b223a302c226c6f6e646f6e426c6f636b223a302c22636c69717565223a7b22706572696f64223a302c2265706f6368223a307d2c22617262697472756d223a7b22456e61626c654172624f53223a747275652c22416c6c6f774465627567507265636f6d70696c6573223a747275652c2244617461417661696c6162696c697479436f6d6d6974746565223a66616c73652c22496e697469616c4172624f5356657273696f6e223a33322c22496e697469616c436861696e4f776e6572223a22307835453134393764443166303843383762326438464532336539414142366331446538333344393237222c2247656e65736973426c6f636b4e756d223a307d7d"));

        ArbitrumGenesisLoader genesisLoader = new(
            chainSpec,
            FullChainSimulationSpecProvider.Instance,
            worldState,
            initMessage,
            arbitrumConfig,
            LimboLogs.Instance);

        Block genesisBlock = genesisLoader.Load();

        genesisBlock.Hash.Should().Be(new Hash256("0xbd9f2163899efb7c39f945c9a7744b2c3ff12cfa00fe573dcb480a436c0803a8"));
    }
}
