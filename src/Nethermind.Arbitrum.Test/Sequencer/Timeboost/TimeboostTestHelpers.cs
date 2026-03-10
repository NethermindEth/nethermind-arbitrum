// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

internal static class TimeboostTestHelpers
{
    internal const ulong TestChainId = 412346;
    internal static readonly Address TestAuctionContract = TestItem.AddressA;

    internal static Transaction MakeTx(ulong nonce = 0, PrivateKey? signer = null)
        => Build.A.Transaction
            .WithNonce(nonce)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(TestItem.AddressC)
            .WithChainId(TestChainId)
            .SignedAndResolved(signer ?? FullChainSimulationAccounts.AccountA)
            .TestObject;

    internal static ExpressLaneSubmission MakeSubmission(
        Transaction tx,
        ulong round,
        ulong seqNum,
        PrivateKey? signerKey = null,
        Address? auctionContract = null)
    {
        Address contract = auctionContract ?? TestAuctionContract;
        PrivateKey key = signerKey ?? FullChainSimulationAccounts.AccountA;
        byte[] sig = SignSubmission(tx, round, seqNum, TestChainId, contract, key);
        return new ExpressLaneSubmission
        {
            Transaction = tx,
            Round = round,
            SequenceNumber = seqNum,
            Signature = sig,
            ChainId = TestChainId,
            AuctionContractAddress = contract,
        };
    }

    // Signs an ExpressLaneSubmission; returns a 65-byte array: r (32) | s (32) | v (recoveryId + 27).
    private static byte[] SignSubmission(
        Transaction tx,
        ulong round,
        ulong sequenceNumber,
        ulong chainId,
        Address auctionContract,
        PrivateKey signerKey)
    {
        ExpressLaneSubmission template = new()
        {
            Transaction = tx,
            Round = round,
            SequenceNumber = sequenceNumber,
            Signature = new byte[65],
            ChainId = chainId,
            AuctionContractAddress = auctionContract,
        };

        ValueHash256 hash = template.ComputeSigningHash();
        Signature sig = new Ecdsa().Sign(signerKey, in hash);

        byte[] result = new byte[65];
        sig.Bytes.CopyTo(result.AsSpan(0, 64));
        result[64] = (byte)(sig.RecoveryId + 27);
        return result;
    }
}
