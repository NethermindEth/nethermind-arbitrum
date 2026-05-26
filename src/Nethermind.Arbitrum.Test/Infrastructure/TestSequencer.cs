// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestSequencer
{
    public static readonly Address TestAuctionContract = TestItem.AddressF;

    public static ArbitrumConfig DefaultConfig(Action<ArbitrumConfig>? setup = null)
    {
        ArbitrumConfig config = new()
        {
            SequencerMaxTxDataSize = 95_000,
            SequencerQueueTimeoutMs = 12000,
            TimeboostRoundDurationSeconds = 60,
            TimeboostAuctionClosingWindowSeconds = 15,
            TimeboostAuctionContractPollIntervalMs = 100,
            TimeboostAuctionContractAddress = TestAuctionContract.ToString(),
            TimeboostEarlySubmissionGraceMs = 2000,
        };

        setup?.Invoke(config);

        return config;
    }

    public static SequencedMsg ExpectedSequencedMessage(BlockHeader header, StartSequencingEnvironment env, byte[][] transactionRlps, byte[] timeboostBlockMetadata)
    {
        MessageWithMetadata messageWithMetadata =
            L2MessageAssembler.AssembleFromSignedTransactions(transactionRlps, env.L1BLockNumber, env.L2Timestamp, header.Nonce);

        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(header, NullLogger.Instance);
        MessageResultForRpc messageResultForRpc = new() { Hash = header.Hash, SendRoot = headerInfo.SendRoot };

        return new SequencedMsg((ulong)header.Number, messageWithMetadata, messageResultForRpc, timeboostBlockMetadata);
    }

    public static SequencedMsg ExpectedSequencedMessage(BlockHeader header, L1IncomingMessage delayedMessage, ulong delayedMessagesRead, byte[] timeboostBlockMetadata)
    {
        MessageWithMetadata messageWithMetadata = new(delayedMessage, delayedMessagesRead);

        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(header, NullLogger.Instance);
        MessageResultForRpc messageResultForRpc = new() { Hash = header.Hash, SendRoot = headerInfo.SendRoot };

        return new SequencedMsg((ulong)header.Number, messageWithMetadata, messageResultForRpc, timeboostBlockMetadata);
    }
}

public record StartSequencingEnvironment(ulong L1BLockNumber, ulong L1Timestamp, ulong L2Timestamp)
{
    public static StartSequencingEnvironment FromNowUtc(ulong l1BlockNumber = 1)
    {
        ulong now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new(l1BlockNumber, now - 500, now);
    }
}
