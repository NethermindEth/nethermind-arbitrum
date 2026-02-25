// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Facade;
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
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 5);
        service.ForceSetController(5, ControllerAddress);
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
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 1);
        service.ForceSetController(1, ControllerAddress);
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
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 3);
        service.ForceSetController(3, ControllerAddress);
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
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 1);
        service.ForceSetController(1, ControllerAddress);
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
    public async Task SequenceAsync_DontCareSequenceNumber_EnqueuesImmediatelyWithoutController()
    {
        // DontCare bypasses the controller check (validation only checks round + auction contract)
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 1);
        Transaction tx = TimeboostTestHelpers.MakeTx();

        await service.SequenceAsync(
            TimeboostTestHelpers.MakeSubmission(tx, round: 1, seqNum: ulong.MaxValue), currentBlockNumber: 100);

        List<TxQueueItem> items = txQueue.DrainBatch();
        items.Should().HaveCount(1);
        items[0].Tx.Should().BeSameAs(tx);
    }

    [Test]
    public async Task SequenceAsync_BufferedSeqResentWithSameSig_IsNoOp()
    {
        // Exact re-submission of a buffered entry (same seq, same sig) is idempotent.
        // Mirrors Go's msgBySequenceNumber check: bytes.Equal(prev.Signature, msg.Signature) → return nil.
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 2);
        service.ForceSetController(2, ControllerAddress);

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
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 2);
        service.ForceSetController(2, ControllerAddress);

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
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 2);
        service.ForceSetController(2, ControllerAddress);

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
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 2);
        service.ForceSetController(2, ControllerAddress);

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
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);
        service.ForceSetController(1, ControllerAddress);

        // MaxFutureSequenceDistance = 4096; anything beyond should be rejected
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 5000);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*too far in the future*");
    }

    [Test]
    public async Task SequenceAsync_NonControllerSigner_Throws()
    {
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);
        service.ForceSetController(1, ControllerAddress);

        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 0, signerKey: AttackerKey);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not the express lane controller*");
    }

    [Test]
    public async Task SequenceAsync_NoControllerRegisteredForRound_Throws()
    {
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);
        // ForceSetController intentionally not called

        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 0);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No controller for round*");
    }

    [Test]
    public async Task SequenceAsync_WrongAuctionContract_Throws()
    {
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);
        service.ForceSetController(1, ControllerAddress);
        Address wrongContract = TestItem.AddressD;

        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 0, auctionContract: wrongContract);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Wrong auction contract*");
    }

    [Test]
    public async Task SequenceAsync_RoundMismatch_Throws()
    {
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 5);
        service.ForceSetController(10, ControllerAddress);

        // round=10 is several rounds ahead of current round=5
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 10, seqNum: 0);

        Func<Task> act = () => service.SequenceAsync(submission, 100);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not match current round*");
    }

    // --- Controller polling tests (use TriggerPoll, no background timer) ---

    [Test]
    public void CurrentRoundHasController_BeforePolling_ReturnsFalse()
    {
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);

        service.CurrentRoundHasController().Should().BeFalse();
    }

    [Test]
    public void TriggerPoll_ValidContractOutput_RegistersController()
    {
        using ExpressLaneService service = CreateServiceWithBridge(
            out TransactionQueue _,
            currentRound: 1,
            contractOutput: BuildAbiOutput(ControllerAddress, round: 1));

        service.TriggerPoll();

        service.CurrentRoundHasController().Should().BeTrue();
    }

    [Test]
    public void TriggerPoll_OutputTooShort_DoesNotRegisterController()
    {
        using ExpressLaneService service = CreateServiceWithBridge(
            out TransactionQueue _,
            currentRound: 1,
            contractOutput: new byte[127]); // minimum is 128

        service.TriggerPoll();

        service.CurrentRoundHasController().Should().BeFalse();
    }

    [Test]
    public void TriggerPoll_ZeroControllerAddress_DoesNotRegisterController()
    {
        using ExpressLaneService service = CreateServiceWithBridge(
            out TransactionQueue _,
            currentRound: 1,
            contractOutput: BuildAbiOutput(Address.Zero, round: 1));

        service.TriggerPoll();

        service.CurrentRoundHasController().Should().BeFalse();
    }

    [Test]
    public void AuctionContractAddress_Always_ReturnsConfiguredValue()
    {
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);

        service.AuctionContractAddress.Should().Be(TimeboostTestHelpers.TestAuctionContract);
    }

    private static ExpressLaneService CreateService(out TransactionQueue txQueue, ulong currentRound = 1)
    {
        txQueue = new TransactionQueue(1024, 95_000);
        return new ExpressLaneService(
            MakeRoundTiming(currentRound),
            TimeboostTestHelpers.TestAuctionContract,
            txQueue,
            getHead: () => null,
            callContract: (_, __) => new CallOutput(),
            TimeSpan.FromMilliseconds(2000),
            LimboLogs.Instance,
            pollInterval: TimeSpan.FromHours(1));
    }

    private static ExpressLaneService CreateServiceWithBridge(
        out TransactionQueue txQueue,
        ulong currentRound,
        byte[] contractOutput)
    {
        txQueue = new TransactionQueue(1024, 95_000);
        BlockHeader head = Build.A.BlockHeader.WithNumber(1).TestObject;
        return new ExpressLaneService(
            MakeRoundTiming(currentRound),
            TimeboostTestHelpers.TestAuctionContract,
            txQueue,
            getHead: () => head,
            callContract: (_, __) => new CallOutput { OutputData = contractOutput },
            TimeSpan.FromMilliseconds(2000),
            LimboLogs.Instance,
            pollInterval: TimeSpan.FromHours(1));
    }

    private static RoundTimingInfo MakeRoundTiming(ulong currentRound)
    {
        // Place UtcNow 30s into round N by setting offset = now - N*60s - 30s
        DateTime offset = DateTime.UtcNow
            - TimeSpan.FromMinutes(1) * (long)currentRound
            - TimeSpan.FromSeconds(30);
        return new RoundTimingInfo(offset, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(15));
    }

    // Builds the 128-byte ABI output for resolvedRounds() with only the current round filled.
    // Upcoming round slot is left zero (no upcoming controller).
    private static byte[] BuildAbiOutput(Address controller, ulong round)
    {
        byte[] output = new byte[128];
        controller.Bytes.CopyTo(output.AsSpan(12, 20));
        BinaryPrimitives.WriteUInt64BigEndian(output.AsSpan(56, 8), round);
        return output;
    }
}
