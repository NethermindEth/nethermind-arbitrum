// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Facade;
using Nethermind.Logging;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class ExpressLaneTrackerTests
{
    private static readonly Address ControllerAddress = FullChainSimulationAccounts.AccountA.Address;

    [Test]
    public void CurrentRoundHasController_BeforeStart_ReturnsFalse()
    {
        using ExpressLaneTracker tracker = CreateTracker(currentRound: 1);

        tracker.CurrentRoundHasController().Should().BeFalse();
    }

    [Test]
    public void PollResolvedRounds_DiscoversController_StoresInState()
    {
        using ExpressLaneTracker tracker = CreateTrackerWithBridge(
            currentRound: 1,
            contractOutput: TimeboostTestHelpers.BuildAbiOutput(ControllerAddress, round: 1));

        tracker.TriggerPoll();

        tracker.GetController(1).Should().Be(ControllerAddress);
    }

    [Test]
    public void PollResolvedRounds_NewController_RaisesEvent()
    {
        using ExpressLaneTracker tracker = CreateTrackerWithBridge(
            currentRound: 1,
            contractOutput: TimeboostTestHelpers.BuildAbiOutput(ControllerAddress, round: 1));

        RoundControllerResolvedEventArgs? receivedArgs = null;
        tracker.ControllerResolved += (_, args) => receivedArgs = args;

        tracker.TriggerPoll();

        receivedArgs.Should().NotBeNull();
        receivedArgs!.Round.Should().Be(1);
        receivedArgs.Controller.Should().Be(ControllerAddress);
    }

    [Test]
    public void PollResolvedRounds_SameController_DoesNotRaiseEvent()
    {
        using ExpressLaneTracker tracker = CreateTrackerWithBridge(
            currentRound: 1,
            contractOutput: TimeboostTestHelpers.BuildAbiOutput(ControllerAddress, round: 1));

        tracker.TriggerPoll(); // first discovery

        int eventCount = 0;
        tracker.ControllerResolved += (_, _) => eventCount++;

        tracker.TriggerPoll(); // same round+controller

        eventCount.Should().Be(0, "event should not fire again for an already-known round");
    }

    [Test]
    public void PollResolvedRounds_RoundsMoreThanTwoBehind_AreEvicted()
    {
        using ExpressLaneTracker tracker = CreateTrackerWithBridge(
            currentRound: 4,
            contractOutput: TimeboostTestHelpers.BuildAbiOutput(ControllerAddress, round: 4));

        tracker.ForceSetController(1, ControllerAddress);
        tracker.HasControllerForRound(1).Should().BeTrue();

        tracker.TriggerPoll();

        tracker.HasControllerForRound(1).Should().BeFalse("round 1 is more than 2 behind round 4 and must be evicted");
        tracker.HasControllerForRound(4).Should().BeTrue("round 4 controller should be registered from the poll");
    }

    [Test]
    public void ForceSetController_ValidRound_StoresController()
    {
        using ExpressLaneTracker tracker = CreateTracker(currentRound: 5);

        tracker.ForceSetController(5, ControllerAddress);

        tracker.GetController(5).Should().Be(ControllerAddress);
        tracker.CurrentRoundHasController().Should().BeTrue();
    }

    [Test]
    public void GetController_UnknownRound_ReturnsNull()
    {
        using ExpressLaneTracker tracker = CreateTracker(currentRound: 1);

        tracker.GetController(99).Should().BeNull();
    }

    [Test]
    public void DisabledTracker_AllQueries_ReturnDefaults()
    {
        DisabledExpressLaneTracker tracker = new();

        tracker.CurrentRoundHasController().Should().BeFalse();
        tracker.GetController(1).Should().BeNull();
        tracker.IsWithinAuctionCloseWindow(DateTime.UtcNow).Should().BeFalse();
        tracker.AuctionContractAddress.Should().Be(Address.Zero);
    }

    [Test]
    public void PollResolvedRounds_OutputTooShort_DoesNotRegisterController()
    {
        using ExpressLaneTracker tracker = CreateTrackerWithBridge(
            currentRound: 1,
            contractOutput: new byte[127]);

        tracker.TriggerPoll();

        tracker.CurrentRoundHasController().Should().BeFalse();
    }

    [Test]
    public void PollResolvedRounds_ZeroControllerAddress_DoesNotRegisterController()
    {
        using ExpressLaneTracker tracker = CreateTrackerWithBridge(
            currentRound: 1,
            contractOutput: TimeboostTestHelpers.BuildAbiOutput(Address.Zero, round: 1));

        tracker.TriggerPoll();

        tracker.CurrentRoundHasController().Should().BeFalse();
    }

    [Test]
    public void AuctionContractAddress_Always_ReturnsConfiguredValue()
    {
        using ExpressLaneTracker tracker = CreateTracker(currentRound: 1);

        tracker.AuctionContractAddress.Should().Be(TimeboostTestHelpers.TestAuctionContract);
    }

    [Test]
    public void IsWithinAuctionCloseWindow_OutsideWindow_ReturnsFalse()
    {
        // 30s into a 60s round with 15s close window → 30s remaining > 15s → outside window
        using ExpressLaneTracker tracker = CreateTracker(currentRound: 1);

        tracker.IsWithinAuctionCloseWindow(DateTime.UtcNow).Should().BeFalse();
    }

    private static ExpressLaneTracker CreateTracker(ulong currentRound)
        => TimeboostTestHelpers.CreateTracker(currentRound);

    private static ExpressLaneTracker CreateTrackerWithBridge(ulong currentRound, byte[] contractOutput)
    {
        BlockHeader header = Build.A.BlockHeader.WithNumber(1).TestObject;
        Block headBlock = new Block(header);
        IBlockTree blockTree = Substitute.For<IBlockTree>();
        blockTree.Head.Returns(headBlock);
        IBlockchainBridge bridge = Substitute.For<IBlockchainBridge>();
        bridge.Call(Arg.Any<BlockHeader>(), Arg.Any<Transaction>()).Returns(new CallOutput { OutputData = contractOutput });
        IBlockchainBridgeFactory bridgeFactory = Substitute.For<IBlockchainBridgeFactory>();
        bridgeFactory.CreateBlockchainBridge().Returns(bridge);
        return new ExpressLaneTracker(
            TimeboostTestHelpers.MakeRoundTiming(currentRound),
            blockTree,
            bridgeFactory,
            new ArbitrumConfig { TimeboostAuctionContractAddress = TestItem.AddressD.ToString(), TimeboostAuctionContractPollIntervalMs = 3_600_000 },
            LimboLogs.Instance);
    }
}
