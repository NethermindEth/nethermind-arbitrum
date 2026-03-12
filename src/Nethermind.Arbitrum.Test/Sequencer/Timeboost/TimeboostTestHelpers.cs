// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Facade;
using Nethermind.Logging;
using NSubstitute;

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

    internal static RoundTimingInfo MakeRoundTiming(ulong currentRound)
    {
        // Place UtcNow 30s into round N by setting offset = now - N*60s - 30s
        DateTime offset = DateTime.UtcNow
            - TimeSpan.FromMinutes(1) * (long)currentRound
            - TimeSpan.FromSeconds(30);
        ArbitrumConfig config = new() { TimeboostRoundDurationSeconds = 60, TimeboostAuctionClosingWindowSeconds = 15 };
        return new RoundTimingInfo(config, offset);
    }

    // Builds the 128-byte ABI output for resolvedRounds() with only the current round filled.
    internal static byte[] BuildAbiOutput(Address controller, ulong round)
    {
        byte[] output = new byte[128];
        controller.Bytes.CopyTo(output.AsSpan(12, 20));
        BinaryPrimitives.WriteUInt64BigEndian(output.AsSpan(56, 8), round);
        return output;
    }

    internal static ExpressLaneTracker CreateTracker(ulong currentRound)
    {
        IBlockTree blockTree = Substitute.For<IBlockTree>();
        blockTree.Head.Returns((Block?)null);
        IBlockchainBridge bridge = Substitute.For<IBlockchainBridge>();
        bridge.Call(Arg.Any<BlockHeader>(), Arg.Any<Transaction>()).Returns(new CallOutput());
        IBlockchainBridgeFactory bridgeFactory = Substitute.For<IBlockchainBridgeFactory>();
        bridgeFactory.CreateBlockchainBridge().Returns(bridge);
        return new ExpressLaneTracker(
            MakeRoundTiming(currentRound),
            blockTree,
            bridgeFactory,
            new ArbitrumConfig { TimeboostAuctionContractAddress = TestAuctionContract.ToString() },
            LimboLogs.Instance,
            pollInterval: TimeSpan.FromHours(1));
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
