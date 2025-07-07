using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Arbos;

public class ArbosGenesisLoaderTests
{
    [Test]
    public void Load_FullChainSimulationAtV32_ProducesCorrectHash()
    {
        (_, Block genesisBlock) = ArbOSInitialization.Create();

        genesisBlock.Hash.Should().Be(new Hash256("0xbd9f2163899efb7c39f945c9a7744b2c3ff12cfa00fe573dcb480a436c0803a8"));
    }
}
