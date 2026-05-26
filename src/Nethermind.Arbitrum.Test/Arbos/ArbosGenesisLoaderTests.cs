// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Arbos;

public class ArbosGenesisLoaderTests
{
    [Test]
    public void Load_FullChainSimulationAtV32_ProducesCorrectHash()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        Block genesisBlock = ArbOSInitialization.Create(worldState);

        genesisBlock.Hash.Should().Be(new Hash256("0xbd9f2163899efb7c39f945c9a7744b2c3ff12cfa00fe573dcb480a436c0803a8"));
    }
}
