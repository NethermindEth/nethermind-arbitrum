// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class ExpressLaneSubmissionTests
{
    [Test]
    public void RecoverSender_ValidSignature_ReturnsSignerAddress()
    {
        PrivateKey controller = FullChainSimulationAccounts.AccountA;
        Transaction tx = TimeboostTestHelpers.MakeTx(nonce: 0, signer: controller);
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(tx, round: 5, seqNum: 0, signerKey: controller);

        submission.RecoverSender().Should().Be(controller.Address);
    }

    [Test]
    public void RecoverSender_CalledTwice_ReturnsCachedReference()
    {
        PrivateKey controller = FullChainSimulationAccounts.AccountA;
        ExpressLaneSubmission submission = TimeboostTestHelpers.MakeSubmission(
            TimeboostTestHelpers.MakeTx(), round: 1, seqNum: 0, signerKey: controller);

        Address first = submission.RecoverSender();
        Address second = submission.RecoverSender();

        ReferenceEquals(first, second).Should().BeTrue("second call must return the cached reference");
    }

    [Test]
    public void RecoverSender_SignatureShorterThan65Bytes_Throws()
    {
        ExpressLaneSubmission submission = new()
        {
            Transaction = TimeboostTestHelpers.MakeTx(),
            Round = 1,
            SequenceNumber = 0,
            Signature = new byte[64],
            ChainId = TimeboostTestHelpers.TestChainId,
            AuctionContractAddress = TimeboostTestHelpers.TestAuctionContract,
        };

        Action act = () => submission.RecoverSender();

        act.Should().Throw<InvalidOperationException>().WithMessage("*65 bytes*");
    }

    [Test]
    public void ToMessageBytes_WithDifferentRounds_ProduceDifferentOutput()
    {
        Transaction tx = TimeboostTestHelpers.MakeTx();
        ExpressLaneSubmission sub1 = BuildUnsigned(tx, round: 1, seqNum: 0);
        ExpressLaneSubmission sub2 = BuildUnsigned(tx, round: 2, seqNum: 0);

        sub1.ToMessageBytes().Should().NotEqual(sub2.ToMessageBytes());
    }

    [Test]
    public void ToMessageBytes_WithDifferentSequenceNumbers_ProduceDifferentOutput()
    {
        Transaction tx = TimeboostTestHelpers.MakeTx();
        ExpressLaneSubmission sub1 = BuildUnsigned(tx, round: 1, seqNum: 0);
        ExpressLaneSubmission sub2 = BuildUnsigned(tx, round: 1, seqNum: 1);

        sub1.ToMessageBytes().Should().NotEqual(sub2.ToMessageBytes());
    }

    private static ExpressLaneSubmission BuildUnsigned(Transaction tx, ulong round, ulong seqNum) => new()
    {
        Transaction = tx,
        Round = round,
        SequenceNumber = seqNum,
        Signature = new byte[65],
        ChainId = TimeboostTestHelpers.TestChainId,
        AuctionContractAddress = TimeboostTestHelpers.TestAuctionContract,
    };
}
