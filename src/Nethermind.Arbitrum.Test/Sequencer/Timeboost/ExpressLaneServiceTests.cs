// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class ExpressLaneServiceTests
{
    private static readonly Address ControllerAddress = FullChainSimulationAccounts.AccountA.Address;
    private static readonly PrivateKey AttackerKey = FullChainSimulationAccounts.AccountB;

    [Test]
    public async Task SequenceAsync_InOrderSubmission_EnqueuesTransaction()
    {
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 5);
        tracker.ForceSetController(5, ControllerAddress);
        Transaction tx = TimeboostTestHelpers.MakeTx();

        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx, round: 5, seqNum: 0), currentBlockNumber: 100);

        List<TxQueueItem> items = txQueue.DrainBatch();
        items.Should().HaveCount(1);
        items[0].Tx.Should().BeSameAs(tx);
        items[0].IsTimeboosted.Should().BeTrue();
        items[0].BlockStamp.Should().Be(100);
    }

    [Test]
    public async Task SequenceAsync_MultipleInOrderSubmissions_EnqueuesEachImmediately()
    {
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 1);
        tracker.ForceSetController(1, ControllerAddress);
        Transaction tx0 = TimeboostTestHelpers.MakeTx(nonce: 0);
        Transaction tx1 = TimeboostTestHelpers.MakeTx(nonce: 1);

        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx0, round: 1, seqNum: 0), 100);
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx1, round: 1, seqNum: 1), 100);

        List<TxQueueItem> items = txQueue.DrainBatch();
        items.Should().HaveCount(2);
        items[0].Tx.Should().BeSameAs(tx0);
        items[1].Tx.Should().BeSameAs(tx1);
        items.Should().AllSatisfy(i => i.IsTimeboosted.Should().BeTrue());
    }

    [Test]
    public async Task SequenceAsync_OutOfOrderSubmissions_BuffersUntilGapFilled()
    {
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 3);
        tracker.ForceSetController(3, ControllerAddress);
        Transaction tx0 = TimeboostTestHelpers.MakeTx(nonce: 0);
        Transaction tx1 = TimeboostTestHelpers.MakeTx(nonce: 1);
        Transaction tx2 = TimeboostTestHelpers.MakeTx(nonce: 2);

        // Submit seq=2 and seq=1 — buffered, waiting for seq=0
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx2, round: 3, seqNum: 2), 100);
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx1, round: 3, seqNum: 1), 100);
        txQueue.DrainBatch().Should().BeEmpty("nothing published before seq=0 arrives");

        // seq=0 fills the gap — all three drain at once, in order
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx0, round: 3, seqNum: 0), 100);

        List<TxQueueItem> items = txQueue.DrainBatch();
        items.Should().HaveCount(3);
        items[0].Tx.Should().BeSameAs(tx0);
        items[1].Tx.Should().BeSameAs(tx1);
        items[2].Tx.Should().BeSameAs(tx2);
        items.Should().AllSatisfy(i => i.IsTimeboosted.Should().BeTrue());
    }

    [Test]
    public async Task SequenceAsync_GapWithLaterBatch_DrainsBatchWhenGapFilled()
    {
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 1);
        tracker.ForceSetController(1, ControllerAddress);
        Transaction tx0 = TimeboostTestHelpers.MakeTx(nonce: 0);
        Transaction tx1 = TimeboostTestHelpers.MakeTx(nonce: 1);
        Transaction tx2 = TimeboostTestHelpers.MakeTx(nonce: 2);
        Transaction tx3 = TimeboostTestHelpers.MakeTx(nonce: 3);

        // seq=0 published immediately; seq=3, seq=2 buffered (gap at 1)
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx0, round: 1, seqNum: 0), 100);
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx3, round: 1, seqNum: 3), 100);
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx2, round: 1, seqNum: 2), 100);
        txQueue.DrainBatch().Should().HaveCount(1, "only seq=0 published so far");

        // seq=1 fills the gap — 1, 2, 3 drain together
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx1, round: 1, seqNum: 1), 100);

        List<TxQueueItem> items = txQueue.DrainBatch();
        items.Should().HaveCount(3);
        items[0].Tx.Should().BeSameAs(tx1);
        items[1].Tx.Should().BeSameAs(tx2);
        items[2].Tx.Should().BeSameAs(tx3);
    }

    [Test]
    public async Task SequenceAsync_BufferedSeqResentWithSameSig_IsNoOp()
    {
        // Exact re-submission of a buffered entry (same seq, same sig) is idempotent.
        // Mirrors Go's msgBySequenceNumber check: bytes.Equal(prev.Signature, msg.Signature) → return nil.
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 2);
        tracker.ForceSetController(2, ControllerAddress);

        // seq=1 goes into buffer (waiting for seq=0)
        ExpressLaneSubmission sub1 = TimeboostTestHelpers.MakeSubmission(TimeboostTestHelpers.MakeTx(nonce: 1), round: 2, seqNum: 1);
        await service.SequenceAsync(sub1, 100);
        txQueue.DrainBatch().Should().BeEmpty();

        // Resend the identical submission — detected as exact duplicate, no drain
        await service.SequenceAsync(sub1, 100);

        txQueue.DrainBatch().Should().BeEmpty("exact duplicate must not trigger a drain");
    }

    [Test]
    public async Task SequenceAsync_BufferedSeqResentWithDifferentSig_Throws()
    {
        // A second submission for a still-buffered sequence number with a different signature is rejected.
        // Mirrors Go's ErrDuplicateSequenceNumber path.
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 2);
        tracker.ForceSetController(2, ControllerAddress);

        // seq=1 goes into buffer (waiting for seq=0)
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(TimeboostTestHelpers.MakeTx(nonce: 1), round: 2, seqNum: 1), 100);
        txQueue.DrainBatch().Should().BeEmpty();

        // Different tx at the same seq=1 → conflicting submission must be rejected
        ExpressLaneSubmission conflict = TimeboostTestHelpers.MakeSubmission(TimeboostTestHelpers.MakeTx(nonce: 99), round: 2, seqNum: 1);

        Func<Task> act = () => service.SequenceAsync(conflict, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Conflicting*");
    }

    [Test]
    public async Task SequenceAsync_AlreadyProcessedSeqResentWithSameSig_IsNoOp()
    {
        // A submission that was already drained (seq below nextSeq) is kept in AllSeen.
        // Re-sending with the same signature is still a no-op (idempotent), matching Go behaviour.
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 2);
        tracker.ForceSetController(2, ControllerAddress);

        // seq=0 submitted and drained (nextSeq advances to 1)
        ExpressLaneSubmission sub0 = TimeboostTestHelpers.MakeSubmission(TimeboostTestHelpers.MakeTx(nonce: 0), round: 2, seqNum: 0);
        await service.SequenceAsync(sub0, 100);
        txQueue.DrainBatch().Should().HaveCount(1);

        // Re-send the exact same submission — must be a no-op, not throw
        await service.SequenceAsync(sub0, 100);

        txQueue.DrainBatch().Should().BeEmpty("exact re-send after sequencing must not produce a second publish");
    }

    [Test]
    public async Task SequenceAsync_StaleSequenceNumber_Throws()
    {
        ExpressLaneService service = CreateService(out TransactionQueue _, out ExpressLaneTracker tracker, currentRound: 2);
        tracker.ForceSetController(2, ControllerAddress);

        // Advance nextSeq to 1 by sending seq=0
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(TimeboostTestHelpers.MakeTx(nonce: 0), round: 2, seqNum: 0), 100);

        // A different transaction signed with seq=0 (below nextSeq=1) is stale — must throw
        ExpressLaneSubmission stale = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(nonce: 1), round: 2, seqNum: 0);

        Func<Task> act = () => service.SequenceAsync(stale, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*too low*");
    }

    [Test]
    public async Task SequenceAsync_SequenceNumberTooFarAhead_Throws()
    {
        ExpressLaneService service = CreateService(out TransactionQueue _, out ExpressLaneTracker tracker, currentRound: 1);
        tracker.ForceSetController(1, ControllerAddress);

        // MaxFutureSequenceDistance = 1000; anything beyond should be rejected
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 5000);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*too far in the future*");
    }

    [Test]
    public async Task SequenceAsync_NonControllerSigner_Throws()
    {
        ExpressLaneService service = CreateService(out TransactionQueue _, out ExpressLaneTracker tracker, currentRound: 1);
        tracker.ForceSetController(1, ControllerAddress);

        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 0, signerKey: AttackerKey);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not the express lane controller*");
    }

    [Test]
    public async Task SequenceAsync_NoControllerRegisteredForRound_Throws()
    {
        ExpressLaneService service = CreateService(out TransactionQueue _, out _, currentRound: 1);
        // ForceSetController intentionally not called on tracker

        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 0);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No controller for round*");
    }

    [Test]
    public async Task SequenceAsync_WrongAuctionContract_Throws()
    {
        ExpressLaneService service = CreateService(out TransactionQueue _, out ExpressLaneTracker tracker, currentRound: 1);
        tracker.ForceSetController(1, ControllerAddress);
        Address wrongContract = TestItem.AddressD;

        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 0, auctionContract: wrongContract);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Wrong auction contract*");
    }

    [Test]
    public async Task SequenceAsync_RoundMismatch_Throws()
    {
        ExpressLaneService service = CreateService(out TransactionQueue _, out ExpressLaneTracker tracker, currentRound: 5);
        tracker.ForceSetController(10, ControllerAddress);

        // round=10 is several rounds ahead of current round=5
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 10, seqNum: 0);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not match the current*");
    }

    [Test]
    public async Task SequenceAsync_NullTransaction_Throws()
    {
        ExpressLaneService service = CreateService(out TransactionQueue _, out _, currentRound: 1);
        ExpressLaneSubmission submission = new()
        {
            Transaction = null!,
            Round = 1,
            SequenceNumber = 0,
            Signature = new byte[65],
            ChainId = TimeboostTestHelpers.TestChainId,
            AuctionContractAddress = TimeboostTestHelpers.TestAuctionContract,
        };

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Malformed*");
    }

    [Test]
    public async Task SequenceAsync_NullSignature_Throws()
    {
        ExpressLaneService service = CreateService(out TransactionQueue _, out _, currentRound: 1);
        ExpressLaneSubmission submission = new()
        {
            Transaction = TimeboostTestHelpers.MakeTx(),
            Round = 1,
            SequenceNumber = 0,
            Signature = null!,
            ChainId = TimeboostTestHelpers.TestChainId,
            AuctionContractAddress = TimeboostTestHelpers.TestAuctionContract,
        };

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Malformed*");
    }

    [Test]
    public async Task SequenceAsync_NextRoundOutsideGracePeriod_Throws()
    {
        // 30s remaining in round, 2s grace → outside grace window → immediate rejection.
        ExpressLaneService service = CreateService(out TransactionQueue _, out _, currentRound: 1);

        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 2, seqNum: ulong.MaxValue);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not match current round*");
    }

    [Test]
    public async Task SequenceAsync_NextRoundWithinGracePeriod_AcceptsAfterDelay()
    {
        // ~1s remaining in a 60s round, 2s grace → within grace window.
        // Service waits for the round boundary then publishes the DontCare tx.
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ManualRoundTimingInfo manualTiming, out _, currentRound: 1, intoRoundSeconds: 59);
        Transaction tx = TimeboostTestHelpers.MakeTx();
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(tx, round: 2, seqNum: ulong.MaxValue);

        // Publish transaction for the 2nd round while we're still in round 1
        Task sequenceTask = service.SequenceAsync(submission, currentBlockNumber: 100);

        // Advance time so we're in round 2 now
        manualTiming.Advance(TimeSpan.FromSeconds(2));

        await sequenceTask;

        txQueue.DrainBatch().Should().HaveCount(1, "tx for next round should be accepted once the round boundary passes");
    }

    [Test]
    public async Task SequenceAsync_DontCareSequenceNumber_DoesNotInterfereWithNormalSequence()
    {
        // DontCare and normal seq=0 coexist in the same round without interfering.
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 1);
        tracker.ForceSetController(1, ControllerAddress);
        Transaction txDontCare = TimeboostTestHelpers.MakeTx(nonce: 0);
        Transaction tx0 = TimeboostTestHelpers.MakeTx(nonce: 1);

        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(txDontCare, round: 1, seqNum: ulong.MaxValue), 100);
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx0, round: 1, seqNum: 0), 100);

        List<TxQueueItem> items = txQueue.DrainBatch();
        items.Should().HaveCount(2);
        items[0].Tx.Should().BeSameAs(txDontCare, "DontCare is published immediately");
        items[1].Tx.Should().BeSameAs(tx0, "seq=0 is published without interference from DontCare");
    }

    [Test]
    public async Task SequenceAsync_DontCareWithBufferedNormalSequence_DoesNotUnblockBuffer()
    {
        // DontCare publishes immediately and does not fill the gap for buffered normal sequences.
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, currentRound: 1);
        tracker.ForceSetController(1, ControllerAddress);
        Transaction tx1 = TimeboostTestHelpers.MakeTx(nonce: 1);
        Transaction txDontCare = TimeboostTestHelpers.MakeTx(nonce: 2);
        Transaction tx0 = TimeboostTestHelpers.MakeTx(nonce: 0);

        // seq=1 goes into buffer — waiting for seq=0
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx1, round: 1, seqNum: 1), 100);
        txQueue.DrainBatch().Should().BeEmpty();

        // DontCare publishes immediately without unblocking seq=1
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(txDontCare, round: 1, seqNum: ulong.MaxValue), 100);
        List<TxQueueItem> afterDontCare = txQueue.DrainBatch();
        afterDontCare.Should().HaveCount(1);
        afterDontCare[0].Tx.Should().BeSameAs(txDontCare);

        // seq=0 fills the gap → seq=0 then seq=1 drain in order
        await service.SequenceAsync(TimeboostTestHelpers.MakeSubmission(tx0, round: 1, seqNum: 0), 100);
        List<TxQueueItem> drained = txQueue.DrainBatch();
        drained.Should().HaveCount(2);
        drained[0].Tx.Should().BeSameAs(tx0);
        drained[1].Tx.Should().BeSameAs(tx1);
    }

    private static ExpressLaneService CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, ulong currentRound = 1)
        => CreateService(out txQueue, out _, out tracker, currentRound);

    private static ExpressLaneService CreateService(
        out TransactionQueue txQueue,
        out ManualRoundTimingInfo manualTiming,
        out ExpressLaneTracker tracker,
        ulong currentRound = 1,
        int intoRoundSeconds = 30)
    {
        ArbitrumConfig config = new()
        {
            SequencerAwaitTxResult = true,
            TimeboostRoundDurationSeconds = 60,
            TimeboostAuctionClosingWindowSeconds = 15,
            TimeboostAuctionContractAddress = TimeboostTestHelpers.TestAuctionContract.ToString(),
            TimeboostEarlySubmissionGraceMs = 2000,
            SequencerMaxTxDataSize = 95000,
            SequencerQueueTimeoutMs = 12000
        };

        txQueue = new TransactionQueue(config, new DisabledExpressLaneTracker());
        manualTiming = new ManualRoundTimingInfo(config, DateTimeOffset.UtcNow, currentRound, TimeSpan.FromSeconds(intoRoundSeconds));
        tracker = TimeboostTestHelpers.CreateTracker(manualTiming);

        return new ExpressLaneService(
            manualTiming,
            tracker,
            config,
            txQueue,
            new EthereumEcdsa(TimeboostTestHelpers.TestChainId),
            TimeboostTestHelpers.TestChainId,
            LimboLogs.Instance);
    }
}
