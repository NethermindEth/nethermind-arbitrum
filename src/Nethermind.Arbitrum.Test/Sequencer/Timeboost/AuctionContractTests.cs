// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class AuctionContractTests
{
    [TestCase(42ul)]
    [TestCase(ulong.MaxValue)]
    public void ParseOutput_ValidOutput_ReturnsControllerAndRound(ulong round)
    {
        ResolvedRound expected = new(TestItem.AddressE, round);
        IAuctionContract contract = CreateAuctionContract(FakeAuctionContract.AbiEncode(expected));

        ResolvedRound result = contract.ResolveRounds();

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void ParseOutput_OutputTooShort_ReturnsZero()
    {
        IAuctionContract contract = CreateAuctionContract(new byte[127]);

        ResolvedRound result = contract.ResolveRounds();

        result.Should().BeEquivalentTo(ResolvedRound.Empty);
    }

    [Test]
    public void ParseOutput_ZeroController_ReturnsZero()
    {
        ResolvedRound expected = new(Address.Zero, 1);
        IAuctionContract contract = CreateAuctionContract(FakeAuctionContract.AbiEncode(expected));

        ResolvedRound result = contract.ResolveRounds();

        result.Should().BeEquivalentTo(ResolvedRound.Empty);
    }

    [Test]
    public void ParseOutput_EmptyOutput_ReturnsZero()
    {
        IAuctionContract contract = CreateAuctionContract([]);

        ResolvedRound result = contract.ResolveRounds();

        result.Should().BeEquivalentTo(ResolvedRound.Empty);
    }

    private static IAuctionContract CreateAuctionContract(byte[] contractCallResponse)
    {
        Block block = Build.A.Block.TestObject;
        IBlockTree blockTree = Build.A.BlockTree().WithBlocks(block).TestObject;
        FakeAuctionContractBlockchainBridgeFactory factory = new(contractCallResponse);

        return new AuctionContract(blockTree, factory, new ArbitrumConfig { TimeboostAuctionContractAddress = TestItem.AddressE.ToString() }, LimboLogs.Instance);
    }
}
