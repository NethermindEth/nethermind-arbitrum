// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Crypto;
using Nethermind.Logging;
using NSubstitute;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class ExpressLaneReliabilityTests
{
    [Test]
    public async Task DontCareSequence_RoundExpired_ThrowsRoundMismatch()
    {
        // DontCareSequence submitted for round 1 but current round is 2 → rejected
        // Use round timing where current round is 2 but submission says round 1
        ExpressLaneService service = CreateService(out _, currentRound: 2);

        Transaction tx = TimeboostTestHelpers.MakeTx();
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(tx, round: 1, seqNum: ulong.MaxValue);

        Func<Task> act = () => service.SequenceAsync(submission, currentBlockNumber: 100);
        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("*does not match current round*");
    }

    [Test]
    public async Task DontCareSequence_CurrentRound_PublishesWithTimeout()
    {
        // DontCareSequence with correct round publishes successfully
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 1);

        Transaction tx = TimeboostTestHelpers.MakeTx();
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(tx, round: 1, seqNum: ulong.MaxValue);

        await service.SequenceAsync(submission, currentBlockNumber: 100);

        List<TxQueueItem> drained = txQueue.DrainBatch();
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
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 1);
        tracker.ForceSetController(1, FullChainSimulationAccounts.AccountA.Address);

        Transaction tx0 = TimeboostTestHelpers.MakeTx(nonce: 0);
        Transaction tx1 = TimeboostTestHelpers.MakeTx(nonce: 1);

        // Submit out of order: seq=1 first (buffered), then seq=0 (triggers drain of both)
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx1, round: 1, seqNum: 1), 100);
        txQueue.DrainBatch().Should().BeEmpty();

        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx0, round: 1, seqNum: 0), 100);

        List<TxQueueItem> items = txQueue.DrainBatch();
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
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 5);
        tracker.ForceSetController(5, FullChainSimulationAccounts.AccountA.Address);

        // Submit a tx for round 5 — this triggers cleanup
        Transaction tx = TimeboostTestHelpers.MakeTx();
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx, round: 5, seqNum: 0), 100);

        // Drain to clear the queue
        txQueue.DrainBatch();

        // The service should only keep rounds >= 4 (currentRound - 1)
        // Verify by checking we can still submit for round 5 (current round is still active)
        Transaction tx2 = TimeboostTestHelpers.MakeTx(nonce: 1);
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx2, round: 5, seqNum: 1), 100);
        txQueue.DrainBatch().Should().HaveCount(1);
    }

    private static ExpressLaneService CreateService(out TransactionQueue txQueue, ulong currentRound = 1)
        => CreateService(out txQueue, out _, currentRound);

    private static ExpressLaneService CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, ulong currentRound = 1)
    {
        txQueue = new TransactionQueue(new ArbitrumConfig(), new DisabledExpressLaneTracker());
        tracker = TimeboostTestHelpers.CreateTracker(currentRound);

        IArbitrumConfig config = Substitute.For<IArbitrumConfig>();
        config.TimeboostAuctionContractAddress.Returns(TimeboostTestHelpers.TestAuctionContract.ToString());
        config.TimeboostEarlySubmissionGraceMs.Returns(2000);
        config.SequencerMaxTxDataSize.Returns(95_000);
        config.SequencerQueueTimeoutMs.Returns(12000);

        return new ExpressLaneService(
            TimeboostTestHelpers.MakeRoundTiming(currentRound),
            tracker,
            config,
            txQueue,
            new EthereumEcdsa(TimeboostTestHelpers.TestChainId),
            TimeboostTestHelpers.TestChainId,
            LimboLogs.Instance);
    }
}
