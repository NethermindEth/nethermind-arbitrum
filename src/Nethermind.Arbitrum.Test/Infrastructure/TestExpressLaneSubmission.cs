// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestExpressLaneSubmission
{
    internal static ExpressLaneSubmission Create(
        Transaction tx,
        ulong round,
        ulong seqNum,
        PrivateKey? signerKey = null,
        Address? auctionContract = null)
    {
        Address contract = auctionContract ?? TestSequencer.TestAuctionContract;
        PrivateKey key = signerKey ?? FullChainSimulationAccounts.AccountA;
        byte[] sig = SignSubmission(tx, round, seqNum, FullChainSimulationChainSpecProvider.ChainId, contract, key);
        return new ExpressLaneSubmission
        {
            Transaction = tx,
            Round = round,
            SequenceNumber = seqNum,
            Signature = sig,
            ChainId = FullChainSimulationChainSpecProvider.ChainId,
            AuctionContractAddress = contract,
        };
    }

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
