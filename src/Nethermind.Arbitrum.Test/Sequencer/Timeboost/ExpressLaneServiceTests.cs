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
public class ExpressLaneServiceTests
{
    private static readonly Address ControllerAddress = FullChainSimulationAccounts.AccountA.Address;

    [Test]
    public async Task SequenceAsync_InOrderSubmission_EnqueuesTransaction()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 5);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 5));
        Transaction tx = TestTransaction.CreateTransfer();

        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx, round: 5, seqNum: 0), currentBlockNumber: 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
        items.Should().HaveCount(1);
        items[0].Tx.Should().BeSameAs(tx);
        items[0].IsTimeboosted.Should().BeTrue();
        items[0].BlockStamp.Should().Be(100);
    }

    [Test]
    public async Task SequenceAsync_MultipleInOrderSubmissions_EnqueuesEachImmediately()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 1));
        Transaction tx0 = TestTransaction.CreateTransfer(nonce: 0);
        Transaction tx1 = TestTransaction.CreateTransfer(nonce: 1);

        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx0, round: 1, seqNum: 0), 100);
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx1, round: 1, seqNum: 1), 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
        items.Should().HaveCount(2);
        items[0].Tx.Should().BeSameAs(tx0);
        items[1].Tx.Should().BeSameAs(tx1);
        items.Should().AllSatisfy(i => i.IsTimeboosted.Should().BeTrue());
    }

    [Test]
    public async Task SequenceAsync_OutOfOrderSubmissions_BuffersUntilGapFilled()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 3);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 3));
        Transaction tx0 = TestTransaction.CreateTransfer(nonce: 0);
        Transaction tx1 = TestTransaction.CreateTransfer(nonce: 1);
        Transaction tx2 = TestTransaction.CreateTransfer(nonce: 2);

        // Submit seq=2 and seq=1 — buffered, waiting for seq=0
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx2, round: 3, seqNum: 2), 100);
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx1, round: 3, seqNum: 1), 100);
        context.TxQueue.DrainBatch().Should().BeEmpty("nothing published before seq=0 arrives");

        // seq=0 fills the gap — all three drain at once, in order
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx0, round: 3, seqNum: 0), 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
        items.Should().HaveCount(3);
        items[0].Tx.Should().BeSameAs(tx0);
        items[1].Tx.Should().BeSameAs(tx1);
        items[2].Tx.Should().BeSameAs(tx2);
        items.Should().AllSatisfy(i => i.IsTimeboosted.Should().BeTrue());
    }

    [Test]
    public async Task SequenceAsync_GapWithLaterBatch_DrainsBatchWhenGapFilled()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 1));
        Transaction tx0 = TestTransaction.CreateTransfer(nonce: 0);
        Transaction tx1 = TestTransaction.CreateTransfer(nonce: 1);
        Transaction tx2 = TestTransaction.CreateTransfer(nonce: 2);
        Transaction tx3 = TestTransaction.CreateTransfer(nonce: 3);

        // seq=0 published immediately; seq=3, seq=2 buffered (gap at 1)
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx0, round: 1, seqNum: 0), 100);
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx3, round: 1, seqNum: 3), 100);
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx2, round: 1, seqNum: 2), 100);
        context.TxQueue.DrainBatch().Should().HaveCount(1, "only seq=0 published so far");

        // seq=1 fills the gap — 1, 2, 3 drain together
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx1, round: 1, seqNum: 1), 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
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
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 2);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 2));

        // seq=1 goes into buffer (waiting for seq=0)
        ExpressLaneSubmission sub1 = TestExpressLaneSubmission.Create(TestTransaction.CreateTransfer(nonce: 1), round: 2, seqNum: 1);
        await service.SequenceAsync(sub1, 100);
        context.TxQueue.DrainBatch().Should().BeEmpty();

        // Resend the identical submission — detected as exact duplicate, no drain
        await service.SequenceAsync(sub1, 100);

        context.TxQueue.DrainBatch().Should().BeEmpty("exact duplicate must not trigger a drain");
    }

    [Test]
    public async Task SequenceAsync_BufferedSeqResentWithDifferentSig_Rejects()
    {
        // A second submission for a still-buffered sequence number with a different signature is rejected.
        // Mirrors Go's ErrDuplicateSequenceNumber path.
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 2);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 2));

        // seq=1 goes into buffer (waiting for seq=0)
        await service.SequenceAsync(TestExpressLaneSubmission.Create(TestTransaction.CreateTransfer(nonce: 1), round: 2, seqNum: 1), 100);

        // Different tx at the same seq=1 → conflicting submission must be rejected
        ExpressLaneSubmission conflict = TestExpressLaneSubmission.Create(TestTransaction.CreateTransfer(nonce: 99), round: 2, seqNum: 1);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(conflict, 100);

        result.Should().RequestFail("Conflicting");
    }

    [Test]
    public async Task SequenceAsync_AlreadyProcessedSeqResentWithSameSig_IsNoOp()
    {
        // A submission that was already drained (seq below nextSeq) is kept in AllSeen.
        // Re-sending with the same signature is still a no-op (idempotent), matching Go behaviour.
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 2);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 2));

        // seq=0 submitted and drained (nextSeq advances to 1)
        ExpressLaneSubmission sub0 = TestExpressLaneSubmission.Create(TestTransaction.CreateTransfer(nonce: 0), round: 2, seqNum: 0);
        await service.SequenceAsync(sub0, 100);
        context.TxQueue.DrainBatch().Should().HaveCount(1);

        // Re-send the exact same submission — must be a no-op, not throw
        await service.SequenceAsync(sub0, 100);

        context.TxQueue.DrainBatch().Should().BeEmpty("exact re-send after sequencing must not produce a second publish");
    }

    [Test]
    public async Task SequenceAsync_StaleSequenceNumber_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 2);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 2));

        // Advance nextSeq to 1 by sending seq=0
        await service.SequenceAsync(TestExpressLaneSubmission.Create(TestTransaction.CreateTransfer(nonce: 0), round: 2, seqNum: 0), 100);

        // A different transaction signed with seq=0 (below nextSeq=1) is stale — must fail
        ExpressLaneSubmission stale = TestExpressLaneSubmission.Create(
            TestTransaction.CreateTransfer(nonce: 1), round: 2, seqNum: 0);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(stale, 100);

        result.Should().RequestFail("too low");
    }

    [Test]
    public async Task SequenceAsync_SequenceNumberTooFarAhead_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 1));

        // MaxFutureSequenceDistance = 1000; anything beyond should be rejected
        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(
            TestTransaction.CreateTransfer(), round: 1, seqNum: 5000);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, 100);

        result.Should().RequestFail("too far in the future");
    }

    [Test]
    public async Task SequenceAsync_NonControllerSigner_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 1));

        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(
            TestTransaction.CreateTransfer(), round: 1, seqNum: 0, signerKey: FullChainSimulationAccounts.AccountB);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, 100);

        result.Should().RequestFail("not the express lane controller");
    }

    [Test]
    public async Task SequenceAsync_NoControllerRegisteredForRound_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        // No controller set up — fake returns default (Address.Zero, 0)

        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(
            TestTransaction.CreateTransfer(), round: 1, seqNum: 0);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, 100);

        result.Should().RequestFail("No controller for round");
    }

    [Test]
    public async Task SequenceAsync_WrongAuctionContract_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 1));
        Address wrongContract = TestItem.AddressD;

        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(
            TestTransaction.CreateTransfer(), round: 1, seqNum: 0, auctionContract: wrongContract);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, 100);

        result.Should().RequestFail("Wrong auction contract");
    }

    [Test]
    public async Task SequenceAsync_RoundMismatch_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 5);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 10));

        // round=10 is several rounds ahead of current round=5
        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(
            TestTransaction.CreateTransfer(), round: 10, seqNum: 0);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, 100);

        result.Should().RequestFail("does not match the current");
    }

    [Test]
    public async Task SequenceAsync_NullTransaction_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        ExpressLaneSubmission submission = new()
        {
            Transaction = null!,
            Round = 1,
            SequenceNumber = 0,
            Signature = new byte[65],
            ChainId = FullChainSimulationChainSpecProvider.ChainId,
            AuctionContractAddress = TestExpressLane.TestAuctionContract,
        };

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, 100);

        result.Should().RequestFail("Malformed");
    }

    [Test]
    public async Task SequenceAsync_NullSignature_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);
        ExpressLaneSubmission submission = new()
        {
            Transaction = TestTransaction.CreateTransfer(),
            Round = 1,
            SequenceNumber = 0,
            Signature = null!,
            ChainId = FullChainSimulationChainSpecProvider.ChainId,
            AuctionContractAddress = TestExpressLane.TestAuctionContract,
        };

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, 100);

        result.Should().RequestFail("Malformed");
    }

    [Test]
    public async Task SequenceAsync_NextRoundOutsideGracePeriod_Rejects()
    {
        // 30s remaining in round, 2s grace → outside grace window → immediate rejection.
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);

        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(
            TestTransaction.CreateTransfer(), round: 2, seqNum: ulong.MaxValue);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, 100);

        result.Should().RequestFail("does not match current round");
    }

    [Test]
    public async Task SequenceAsync_NextRoundWithinGracePeriod_AcceptsAfterDelay()
    {
        // ~1s remaining in a 60s round, 2s grace → within grace window.
        // Service waits for the round boundary then publishes the DontCare tx.
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 1, intoRoundSeconds: 59);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        Transaction tx = TestTransaction.CreateTransfer();
        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(tx, round: 2, seqNum: ulong.MaxValue);

        // Publish transaction for the 2nd round while we're still in round 1
        Task sequenceTask = service.SequenceAsync(submission, currentBlockNumber: 100);

        // Advance time so we're in round 2 now
        trackerContext.AdvanceTime(TimeSpan.FromSeconds(2));

        await sequenceTask;

        context.TxQueue.DrainBatch().Should().HaveCount(1, "tx for next round should be accepted once the round boundary passes");
    }

    [Test]
    public async Task SequenceAsync_DontCareSequenceNumber_DoesNotInterfereWithNormalSequence()
    {
        // DontCare and normal seq=0 coexist in the same round without interfering.
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 1));
        Transaction txDontCare = TestTransaction.CreateTransfer(nonce: 0);
        Transaction tx0 = TestTransaction.CreateTransfer(nonce: 1);

        await service.SequenceAsync(TestExpressLaneSubmission.Create(txDontCare, round: 1, seqNum: ulong.MaxValue), 100);
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx0, round: 1, seqNum: 0), 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
        items.Should().HaveCount(2);
        items[0].Tx.Should().BeSameAs(txDontCare, "DontCare is published immediately");
        items[1].Tx.Should().BeSameAs(tx0, "seq=0 is published without interference from DontCare");
    }

    [Test]
    public async Task SequenceAsync_DontCareWithBufferedNormalSequence_DoesNotUnblockBuffer()
    {
        // DontCare publishes immediately and does not fill the gap for buffered normal sequences.
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 1));
        Transaction tx1 = TestTransaction.CreateTransfer(nonce: 1);
        Transaction txDontCare = TestTransaction.CreateTransfer(nonce: 2);
        Transaction tx0 = TestTransaction.CreateTransfer(nonce: 0);

        // seq=1 goes into buffer — waiting for seq=0
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx1, round: 1, seqNum: 1), 100);
        context.TxQueue.DrainBatch().Should().BeEmpty();

        // DontCare publishes immediately without unblocking seq=1
        await service.SequenceAsync(TestExpressLaneSubmission.Create(txDontCare, round: 1, seqNum: ulong.MaxValue), 100);
        List<TxQueueItem> afterDontCare = context.TxQueue.DrainBatch();
        afterDontCare.Should().HaveCount(1);
        afterDontCare[0].Tx.Should().BeSameAs(txDontCare);

        // seq=0 fills the gap → seq=0 then seq=1 drain in order
        await service.SequenceAsync(TestExpressLaneSubmission.Create(tx0, round: 1, seqNum: 0), 100);
        List<TxQueueItem> drained = context.TxQueue.DrainBatch();
        drained.Should().HaveCount(2);
        drained[0].Tx.Should().BeSameAs(tx0);
        drained[1].Tx.Should().BeSameAs(tx1);
    }

    [Test]
    public async Task SequenceAsync_OversizedTransaction_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(
            out TestExpressLaneTrackerContext trackerContext,
            setup: c => c.SequencerMaxTxDataSize = 100);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);

        Transaction tx = TestTransaction.CreateTransfer();
        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(tx, round: 1, seqNum: 0);

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, currentBlockNumber: 1);

        result.Should().RequestFail("exceeds maximum");
    }

    [Test]
    public async Task SequenceAsync_WrongChainId_Rejects()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);

        ExpressLaneSubmission submission = new()
        {
            Transaction = TestTransaction.CreateTransfer(),
            Round = 1,
            SequenceNumber = 0,
            Signature = new byte[65],
            ChainId = 999,
            AuctionContractAddress = TestExpressLane.TestAuctionContract,
        };

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(submission, currentBlockNumber: 1);

        result.Should().RequestFail("chain ID");
    }

    [Test]
    public async Task SequenceAsync_WrongChainIdWithValidSignature_RejectsWithChainIdError()
    {
        // Chain ID validation must run before controller check — even with a valid signature,
        // wrong chain ID is caught first.
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out _);

        // Create a properly signed submission, then tamper with chain ID
        ExpressLaneSubmission signed = TestExpressLaneSubmission.Create(
            TestTransaction.CreateTransfer(), round: 1, seqNum: 0);

        ExpressLaneSubmission wrongChainSubmission = new()
        {
            Transaction = signed.Transaction,
            Round = signed.Round,
            SequenceNumber = signed.SequenceNumber,
            Signature = signed.Signature,
            ChainId = 999,
            AuctionContractAddress = signed.AuctionContractAddress,
        };

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(wrongChainSubmission, currentBlockNumber: 1);

        result.Should().RequestFail("chain ID");
    }

    [Test]
    public async Task SequenceAsync_WithValidConditionalOptions_Succeeds()
    {
        // Options with BlockNumberMax = 100 pass when L1 block is 0 (default header returns Empty → L1BlockNumber=0)
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 5);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 5));

        ConditionalOptions options = new() { BlockNumberMax = 100 };
        Transaction tx = TestTransaction.CreateTransfer();

        await service.SequenceAsync(
            TestExpressLaneSubmission.Create(tx, round: 5, seqNum: 0, options: options),
            currentBlockNumber: 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
        items.Should().HaveCount(1);
        items[0].Tx.Should().BeSameAs(tx);
        items[0].Options.Should().BeSameAs(options);
    }

    [Test]
    public async Task SequenceAsync_WithInvalidConditionalOptions_RejectedEarly()
    {
        // Options with BlockNumberMin = 100 fail when L1 block is 0 (default header returns Empty → L1BlockNumber=0)
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 5);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 5));

        ConditionalOptions options = new() { BlockNumberMin = 100 };

        ResultWrapper<EmptyResponse> result = await service.SequenceAsync(
            TestExpressLaneSubmission.Create(TestTransaction.CreateTransfer(), round: 5, seqNum: 0, options: options),
            currentBlockNumber: 100);

        result.Should().RequestFail("Conditional options check failed");
        context.TxQueue.DrainBatch().Should().BeEmpty("rejected tx should not be enqueued");
    }

    [Test]
    public async Task SequenceAsync_DontCare_ConditionalOptionsPassedThrough()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 1));

        ConditionalOptions options = new() { BlockNumberMax = 100 };
        Transaction tx = TestTransaction.CreateTransfer();

        await service.SequenceAsync(
            TestExpressLaneSubmission.Create(tx, round: 1, seqNum: ulong.MaxValue, options: options),
            currentBlockNumber: 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
        items.Should().HaveCount(1);
        items[0].Options.Should().BeSameAs(options);
    }

    [Test]
    public async Task SequenceAsync_GapFilling_ConditionalOptionsPreserved()
    {
        using ExpressLaneTracker tracker = TestExpressLane.CreateTracker(out TestExpressLaneTrackerContext trackerContext, currentRound: 3);
        ExpressLaneService service = TestExpressLane.CreateService(tracker, trackerContext, out TestExpressLaneServiceContext context);
        await trackerContext.AdvanceLoop(new ResolvedRound(ControllerAddress, 3));

        ConditionalOptions options0 = new() { BlockNumberMax = 200 };
        ConditionalOptions options1 = new() { BlockNumberMax = 300 };
        Transaction tx0 = TestTransaction.CreateTransfer(nonce: 0);
        Transaction tx1 = TestTransaction.CreateTransfer(nonce: 1);

        // Submit seq=1 first (buffered), then seq=0 fills the gap — both drain with their Options
        await service.SequenceAsync(
            TestExpressLaneSubmission.Create(tx1, round: 3, seqNum: 1, options: options1), 100);
        context.TxQueue.DrainBatch().Should().BeEmpty();

        await service.SequenceAsync(
            TestExpressLaneSubmission.Create(tx0, round: 3, seqNum: 0, options: options0), 100);

        List<TxQueueItem> items = context.TxQueue.DrainBatch();
        items.Should().HaveCount(2);
        items[0].Options.Should().BeSameAs(options0);
        items[1].Options.Should().BeSameAs(options1);
    }
}
