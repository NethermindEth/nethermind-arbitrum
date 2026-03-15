// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class ExpressLaneSubmissionTests
{
    private static readonly EthereumEcdsa Ecdsa = new(FullChainSimulationChainSpecProvider.ChainId);

    [Test]
    public void RecoverSender_ValidSignature_ReturnsSignerAddress()
    {
        PrivateKey controller = FullChainSimulationAccounts.AccountA;
        Transaction tx = TestTransaction.CreateTransfer(nonce: 0, signer: controller);
        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(tx, round: 5, seqNum: 0, signerKey: controller);

        submission.RecoverSender(Ecdsa).Should().Be(controller.Address);
    }

    [Test]
    public void RecoverSender_CalledTwice_ReturnsCachedReference()
    {
        PrivateKey controller = FullChainSimulationAccounts.AccountA;
        ExpressLaneSubmission submission = TestExpressLaneSubmission.Create(TestTransaction.CreateTransfer(), round: 1, seqNum: 0, signerKey: controller);

        Address first = submission.RecoverSender(Ecdsa);
        Address second = submission.RecoverSender(Ecdsa);

        first.Should().BeSameAs(second, "second call must return the cached reference");
    }

    [Test]
    public void RecoverSender_SignatureShorterThan65Bytes_Throws()
    {
        ExpressLaneSubmission submission = new()
        {
            Transaction = TestTransaction.CreateTransfer(),
            Round = 1,
            SequenceNumber = 0,
            Signature = new byte[64],
            ChainId = FullChainSimulationChainSpecProvider.ChainId,
            AuctionContractAddress = TestItem.AddressA
        };

        Action act = () => submission.RecoverSender(Ecdsa);

        act.Should().Throw<InvalidOperationException>().WithMessage("*65 bytes*");
    }

    [Test]
    public void ToMessageBytes_WithDifferentRounds_ProduceDifferentOutput()
    {
        Transaction tx = TestTransaction.CreateTransfer();
        ExpressLaneSubmission sub1 = BuildUnsigned(tx, round: 1, seqNum: 0);
        ExpressLaneSubmission sub2 = BuildUnsigned(tx, round: 2, seqNum: 0);

        sub1.ToMessageBytes().Should().NotBeEquivalentTo(sub2.ToMessageBytes());
    }

    [Test]
    public void ToMessageBytes_WithDifferentSequenceNumbers_ProduceDifferentOutput()
    {
        Transaction tx = TestTransaction.CreateTransfer();
        ExpressLaneSubmission sub1 = BuildUnsigned(tx, round: 1, seqNum: 0);
        ExpressLaneSubmission sub2 = BuildUnsigned(tx, round: 1, seqNum: 1);

        sub1.ToMessageBytes().Should().NotBeEquivalentTo(sub2.ToMessageBytes());
    }

    private static ExpressLaneSubmission BuildUnsigned(Transaction tx, ulong round, ulong seqNum)
        => new()
        {
            Transaction = tx,
            Round = round,
            SequenceNumber = seqNum,
            Signature = new byte[65],
            ChainId = FullChainSimulationChainSpecProvider.ChainId,
            AuctionContractAddress = TestItem.AddressA,
        };
}
