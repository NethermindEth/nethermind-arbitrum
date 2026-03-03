// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestSequencer
{
    public static SequencedMsg ExpectedSequencedMessage(BlockHeader header, StartSequencingEnvironment env, byte[] timeboostBlockMetadata)
    {
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(header, NullLogger.Instance);

        L1IncomingMessageHeader l1MessageHeader = new(ArbitrumL1MessageKind.L2Message, ArbosAddresses.BatchPosterAddress, env.L1BLockNumber, env.L2Timestamp, null, null);
        L1IncomingMessage l1Message = new(l1MessageHeader, null, null, null);

        return new SequencedMsg(
            (ulong)header.Number,
            new MessageWithMetadata(l1Message, header.Nonce),
            new MessageResultForRpc { Hash = header.Hash, SendRoot = headerInfo.SendRoot },
            timeboostBlockMetadata);
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
