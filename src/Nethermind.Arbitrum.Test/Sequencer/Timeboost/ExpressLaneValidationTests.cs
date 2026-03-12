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
public class ExpressLaneValidationTests
{
    [Test]
    public void OversizedTx_SubmittedViaExpressLane_RejectedBeforeSequencing()
    {
        ExpressLaneService service = CreateService(out _, maxTxDataSize: 100);

        // Build a submission with a normal-sized tx that exceeds the very small maxTxDataSize
        Transaction tx = TimeboostTestHelpers.MakeTx();
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(tx, round: 1, seqNum: 0,
            signerKey: FullChainSimulationAccounts.AccountA);

        Func<Task> act = () => service.SequenceAsync(submission, currentBlockNumber: 1);
        act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum*");
    }

    [Test]
    public void WrongChainId_SubmittedViaExpressLane_RejectedWithClearError()
    {
        ExpressLaneService service = CreateService(out _);

        Transaction tx = TimeboostTestHelpers.MakeTx();
        // Build submission with wrong chain ID
        ExpressLaneSubmission submission = new()
        {
            Transaction = tx,
            Round = 1,
            SequenceNumber = 0,
            Signature = new byte[65],
            ChainId = 999,
            AuctionContractAddress = TimeboostTestHelpers.TestAuctionContract,
        };

        Func<Task> act = () => service.SequenceAsync(submission, currentBlockNumber: 1);
        act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("*chain ID*does not match*");
    }

    [Test]
    public async Task ValidSubmission_SubmittedViaExpressLane_PassesAllChecks()
    {
        ExpressLaneService service = CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker);
        tracker.ForceSetController(1, FullChainSimulationAccounts.AccountA.Address);

        Transaction tx = TimeboostTestHelpers.MakeTx();
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(tx, round: 1, seqNum: 0,
            signerKey: FullChainSimulationAccounts.AccountA);

        await service.SequenceAsync(submission, currentBlockNumber: 1);

        List<TxQueueItem> drained = txQueue.DrainBatch();
        drained.Should().HaveCount(1);
        drained[0].Tx.Should().BeSameAs(tx);
        drained[0].IsTimeboosted.Should().BeTrue();
    }

    [Test]
    public void ChainIdCheck_WrongChainWithValidController_RejectsWithChainIdError()
    {
        ExpressLaneService service = CreateService(out _);

        Transaction tx = TimeboostTestHelpers.MakeTx();
        // Wrong chain ID but with a valid controller signature (AccountA is controller for round 1)
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(tx, round: 1, seqNum: 0,
            signerKey: FullChainSimulationAccounts.AccountA);

        // Override the chain ID to wrong value after signing
        ExpressLaneSubmission wrongChainSubmission = new()
        {
            Transaction = submission.Transaction,
            Round = submission.Round,
            SequenceNumber = submission.SequenceNumber,
            Signature = submission.Signature,
            ChainId = 999, // Wrong chain ID
            AuctionContractAddress = submission.AuctionContractAddress,
        };

        Func<Task> act = () => service.SequenceAsync(wrongChainSubmission, currentBlockNumber: 1);
        // Should fail with chain ID error, not controller mismatch
        act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("*chain ID*does not match*");
    }

    private static ExpressLaneService CreateService(out TransactionQueue txQueue, int maxTxDataSize = 95_000, ulong currentRound = 1)
        => CreateService(out txQueue, out _, maxTxDataSize, currentRound);

    private static ExpressLaneService CreateService(out TransactionQueue txQueue, out ExpressLaneTracker tracker, int maxTxDataSize = 95_000, ulong currentRound = 1)
    {
        txQueue = new TransactionQueue(new ArbitrumConfig { SequencerAwaitTxResult = true }, new DisabledExpressLaneTracker());
        tracker = TimeboostTestHelpers.CreateTracker(currentRound);

        IArbitrumConfig config = Substitute.For<IArbitrumConfig>();
        config.TimeboostAuctionContractAddress.Returns(TimeboostTestHelpers.TestAuctionContract.ToString());
        config.TimeboostEarlySubmissionGraceMs.Returns(2000);
        config.SequencerMaxTxDataSize.Returns(maxTxDataSize);
        config.SequencerQueueTimeoutMs.Returns(12000);

        return new ExpressLaneService(
            TimeboostTestHelpers.MakeRoundTiming(currentRound),
            tracker,
            config,
            txQueue,
            new EthereumEcdsa(FullChainSimulationChainSpecProvider.ChainId),
            FullChainSimulationChainSpecProvider.Create(),
            LimboLogs.Instance);
    }
}
