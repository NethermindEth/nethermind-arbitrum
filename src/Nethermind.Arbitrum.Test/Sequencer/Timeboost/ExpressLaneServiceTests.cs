// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Facade;
using Nethermind.Logging;
using NSubstitute;

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

    [Test]
    public async Task SequenceAsync_NullTransaction_Throws()
    {
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);
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
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);
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
        using ExpressLaneService service = CreateService(out TransactionQueue _, currentRound: 1);

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
        using ExpressLaneService service = CreateServiceNearRoundEnd(out TransactionQueue txQueue, currentRound: 1);
        Transaction tx = TimeboostTestHelpers.MakeTx();
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(tx, round: 2, seqNum: ulong.MaxValue);

        await service.SequenceAsync(submission, currentBlockNumber: 100);

        txQueue.DrainBatch().Should().HaveCount(1, "tx for next round should be accepted once the round boundary passes");
    }

    [Test]
    public async Task SequenceAsync_DontCareSequenceNumber_DoesNotInterfereWithNormalSequence()
    {
        // DontCare and normal seq=0 coexist in the same round without interfering.
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 1);
        service.ForceSetController(1, ControllerAddress);
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
        using ExpressLaneService service = CreateService(out TransactionQueue txQueue, currentRound: 1);
        service.ForceSetController(1, ControllerAddress);
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

    [Test]
    public void TriggerPoll_OldRoundControllers_AreCleanedUp()
    {
        // At round 4, controllers from rounds < (4 - 2) = 2 are evicted.
        using ExpressLaneService service = CreateServiceWithBridge(
            out TransactionQueue _,
            currentRound: 4,
            contractOutput: BuildAbiOutput(ControllerAddress, round: 4));

        service.ForceSetController(1, ControllerAddress);
        service.HasControllerForRound(1).Should().BeTrue();

        service.TriggerPoll();

        service.HasControllerForRound(1).Should().BeFalse("round 1 is more than 2 behind round 4 and must be evicted");
        service.HasControllerForRound(4).Should().BeTrue("round 4 controller should be registered from the poll");
    }

    private static ExpressLaneService CreateService(out TransactionQueue txQueue, ulong currentRound = 1)
    {
        txQueue = new TransactionQueue(1024, 95_000, awaitTxResult: true);
        IBlockTree blockTree = Substitute.For<IBlockTree>();
        blockTree.Head.Returns((Block?)null);
        IBlockchainBridge bridge = Substitute.For<IBlockchainBridge>();
        bridge.Call(Arg.Any<BlockHeader>(), Arg.Any<Transaction>()).Returns(new CallOutput());
        IBlockchainBridgeFactory bridgeFactory = Substitute.For<IBlockchainBridgeFactory>();
        bridgeFactory.CreateBlockchainBridge().Returns(bridge);
        return new ExpressLaneService(
            MakeRoundTiming(currentRound),
            MakeArbitrumConfig(),
            txQueue,
            blockTree,
            bridgeFactory,
            LimboLogs.Instance,
            pollInterval: TimeSpan.FromHours(1));
    }

    private static ExpressLaneService CreateServiceWithBridge(
        out TransactionQueue txQueue,
        ulong currentRound,
        byte[] contractOutput)
    {
        txQueue = new TransactionQueue(1024, 95_000, awaitTxResult: true);
        BlockHeader header = Build.A.BlockHeader.WithNumber(1).TestObject;
        Block headBlock = new Block(header);
        IBlockTree blockTree = Substitute.For<IBlockTree>();
        blockTree.Head.Returns(headBlock);
        IBlockchainBridge bridge = Substitute.For<IBlockchainBridge>();
        bridge.Call(Arg.Any<BlockHeader>(), Arg.Any<Transaction>()).Returns(new CallOutput { OutputData = contractOutput });
        IBlockchainBridgeFactory bridgeFactory = Substitute.For<IBlockchainBridgeFactory>();
        bridgeFactory.CreateBlockchainBridge().Returns(bridge);
        return new ExpressLaneService(
            MakeRoundTiming(currentRound),
            MakeArbitrumConfig(),
            txQueue,
            blockTree,
            bridgeFactory,
            LimboLogs.Instance,
            pollInterval: TimeSpan.FromHours(1));
    }

    private static ExpressLaneService CreateServiceNearRoundEnd(out TransactionQueue txQueue, ulong currentRound)
    {
        txQueue = new TransactionQueue(1024, 95_000, awaitTxResult: true);
        // Place UtcNow ~1s before the end of the current round so timeTilNext ≈ 1s < 2s grace.
        DateTime offset = DateTime.UtcNow
            - TimeSpan.FromMinutes(1) * (long)currentRound
            - TimeSpan.FromSeconds(59);
        RoundTimingInfo timing = new(offset, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(15));
        IBlockTree blockTree = Substitute.For<IBlockTree>();
        blockTree.Head.Returns((Block?)null);
        IBlockchainBridge bridge = Substitute.For<IBlockchainBridge>();
        bridge.Call(Arg.Any<BlockHeader>(), Arg.Any<Transaction>()).Returns(new CallOutput());
        IBlockchainBridgeFactory bridgeFactory = Substitute.For<IBlockchainBridgeFactory>();
        bridgeFactory.CreateBlockchainBridge().Returns(bridge);
        return new ExpressLaneService(
            timing,
            MakeArbitrumConfig(),
            txQueue,
            blockTree,
            bridgeFactory,
            LimboLogs.Instance,
            pollInterval: TimeSpan.FromHours(1));
    }

    private static IArbitrumConfig MakeArbitrumConfig()
    {
        IArbitrumConfig config = Substitute.For<IArbitrumConfig>();
        config.TimeboostAuctionContractAddress.Returns(TimeboostTestHelpers.TestAuctionContract.ToString());
        config.TimeboostEarlySubmissionGraceMs.Returns(2000);
        return config;
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
