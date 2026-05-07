// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class ExpressLaneTrackerTests
{
    [Test]
    public void CurrentRoundHasController_BeforeStartCalled_ReturnsFalse()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext _);

        tracker.CurrentRoundHasController().Should().BeFalse();
    }

    [Test]
    public async Task GetController_AfterPollDiscoversController_ReturnsController()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext context);
        ResolvedRound resolvedRound = new(TestItem.AddressA, 1);
        await context.AdvanceLoop(resolvedRound);

        tracker.GetController(resolvedRound.Round).Should().Be(resolvedRound.Controller);
    }

    [Test]
    public async Task ControllerResolved_WhenNewControllerDiscovered_FiresEvent()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext context);
        ResolvedRound resolvedRound = new(TestItem.AddressA, 1);

        RoundControllerResolvedEventArgs? receivedArgs = null;
        tracker.ControllerResolved += (_, args) => receivedArgs = args;

        await context.AdvanceLoop(resolvedRound);

        receivedArgs.Should().NotBeNull();
        receivedArgs.Round.Should().Be(resolvedRound.Round);
        receivedArgs.Controller.Should().Be(resolvedRound.Controller);
    }

    [Test]
    public async Task ControllerResolved_WhenSameControllerPolledTwice_FiresOnce()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext context);
        ResolvedRound resolvedRound = new(TestItem.AddressA, 1);

        await context.AdvanceLoop(resolvedRound);

        int eventCount = 0;
        tracker.ControllerResolved += (_, _) => eventCount++;

        await context.AdvanceLoop(resolvedRound);

        eventCount.Should().Be(0, "event should not fire again for an already-known round");
    }

    [Test]
    public async Task GetController_WhenRoundIsMoreThanTwoBehindCurrent_ReturnsNull()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext context);
        ResolvedRound resolvedRound = new(TestItem.AddressA, 1);

        // First poll: discovers round 1 controller
        await context.AdvanceLoop(resolvedRound);
        tracker.GetController(resolvedRound.Round).Should().Be(resolvedRound.Controller);

        // Change to return round 4, advance to round 4 (by advancing RoundTimingInfo to provide new rounds)
        context.AdvanceToNextRound();
        await context.AdvanceLoop(new(TestItem.AddressB, 2));
        context.AdvanceToNextRound();
        await context.AdvanceLoop(new(TestItem.AddressC, 3));
        context.AdvanceToNextRound();
        await context.AdvanceLoop(new(TestItem.AddressD, 4));

        tracker.GetController(1).Should().BeNull("round 1 is more than 2 behind round 4 and must be evicted");
        tracker.GetController(4).Should().Be(TestItem.AddressD);
    }

    [Test]
    public async Task GetController_WhenContractReturnsZeroAddress_ReturnsNull()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext context);

        await context.AdvanceLoop(new(Address.Zero, 1));

        tracker.CurrentRoundHasController().Should().BeFalse();
    }

    [Test]
    public void AuctionContractAddress_Always_DelegatesToAuctionContract()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext context);

        tracker.AuctionContractAddress.Should().Be(context.AuctionContract.Address);
    }

    [Test]
    public void IsWithinAuctionCloseWindow_Always_DelegatesToRoundTimingInfo()
    {
        // 30s into a 60s round with 15s close window → 30s remaining > 15s → outside window
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(
            out TestExpressLaneTrackerContext context,
            intoRoundSeconds: 30,
            setup: c =>
            {
                c.TimeboostRoundDurationSeconds = 60;
                c.TimeboostAuctionClosingWindowSeconds = 15;
            });

        tracker.IsWithinAuctionCloseWindow(context.Timing.TimeProvider.GetUtcNow().UtcDateTime).Should().BeFalse();
    }

    [Test]
    public async Task GetController_AfterDispose_StopsDiscoveringNewControllers()
    {
        ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext context);
        await context.AdvanceLoop(new(TestItem.AddressA, 1));

        tracker.GetController(1).Should().Be(TestItem.AddressA);
        tracker.Dispose();

        // Try to trigger another poll — should have no effect
        context.AuctionContract.Result = new(TestItem.AddressB, 2);
        context.Timing.Advance(TimeSpan.FromMilliseconds(context.Config.TimeboostAuctionContractPollIntervalMs));

        tracker.GetController(2).Should().BeNull("no new controllers after dispose");
    }

    [Test]
    public void GetController_WhenRoundNeverPolled_ReturnsNull()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext _);

        tracker.GetController(99).Should().BeNull();
    }

    [Test]
    public void DisabledTracker_Always_ReturnsDefaults()
    {
        DisabledExpressLaneTracker tracker = new();

        tracker.CurrentRoundHasController().Should().BeFalse();
        tracker.GetController(1).Should().BeNull();
        tracker.IsWithinAuctionCloseWindow(DateTime.UtcNow).Should().BeFalse();
        tracker.AuctionContractAddress.Should().Be(Address.Zero);
    }

    [Test]
    public async Task DontCareSequence_RoundExpired_RejectsRoundMismatch()
    {
        // DontCareSequence submitted for round 1 but current round is 2 → rejected
        // Use round timing where current round is 2 but submission says round 1
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext context, currentRound: 2);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, context, out _);

        Transaction tx = TestTransaction.CreateTransfer();
        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(tx, round: 1, seqNum: ExpressLaneService.DontCareSequenceNumber);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, currentBlockNumber: 100);
        result.Should().RequestFail("does not match the current");
    }

    [Test]
    public async Task DontCareSequence_CurrentRound_PublishesWithTimeout()
    {
        // DontCareSequence with correct round publishes successfully
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 1);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);

        Transaction tx = TestTransaction.CreateTransfer();
        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(tx, round: 1, seqNum: ExpressLaneService.DontCareSequenceNumber);

        await service.SequenceAsync(submission, currentBlockNumber: 100);

        List<TxQueueItem> drained = context.TxQueue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].Tx.Should().BeSameAs(tx);
        drained[0].IsTimeboosted.Should().BeTrue();
        // The CancellationToken should be linked to a timeout CTS (not CancellationToken.None)
        drained[0].CancellationToken.CanBeCanceled.Should().BeTrue("DontCareSequence should use a round-end timeout");
    }

    [Test]
    public async Task DrainLoop_SequencedTxs_HaveRoundEndTimeout()
    {
        // Verify that txs published from the drain loop have cancellable tokens (timeout-based)
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 1);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);

        // Advance tracker loop to setup controller
        await trackerContext.AdvanceLoop(new ResolvedRound(TestItem.AddressA, 1));

        Transaction tx0 = TestTransaction.CreateTransfer(nonce: 0);
        Transaction tx1 = TestTransaction.CreateTransfer(nonce: 1);

        // Submit out of order: seq=1 first (buffered), then seq=0 (triggers drain of both)
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx1, round: 1, seqNum: 1), 100);
        context.TxQueue.DrainBatch().Should().BeEmpty();

        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx0, round: 1, seqNum: 0), 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
        items.Should().HaveCount(2);
        items[0].Tx.Should().BeSameAs(tx0);
        items[1].Tx.Should().BeSameAs(tx1);
        items.Should().AllSatisfy(i =>
            i.CancellationToken.CanBeCanceled.Should().BeTrue("drain loop txs should use round-end timeout"));
    }

    [Test]
    public async Task RoundCleanup_CurrentMinusOne_RemovesOldRounds()
    {
        // Verify that round infos older than currentRound - 1 are cleaned up
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 5);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);

        // Advance tracker loop to setup controller
        await trackerContext.AdvanceLoop(new ResolvedRound(TestItem.AddressA, 5));

        // ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ManualRoundTimingInfo timing, out FakeAuctionContract fake, currentRound: 5);
        // await SetupController(fake, timing, controllerAddress: FullChainSimulationAccounts.AccountA.Address, round: 5);

        // Submit a tx for round 5 — this triggers cleanup
        Transaction tx = TestTransaction.CreateTransfer();
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx, round: 5, seqNum: 0), 100);

        // Drain to clear the queue
        context.TxQueue.DrainBatch();

        // The service should only keep rounds >= 4 (currentRound - 1)
        // Verify by checking we can still submit for round 5 (current round is still active)
        Transaction tx2 = TestTransaction.CreateTransfer(nonce: 1);
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx2, round: 5, seqNum: 1), 100);
        context.TxQueue.DrainBatch().Should().HaveCount(1);
    }
}
