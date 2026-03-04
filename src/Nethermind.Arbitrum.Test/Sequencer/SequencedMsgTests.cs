// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencedMsgTests
{
    [Test]
    public void SequencedMsg_Serialization_RoundTrip()
    {
        L1IncomingMessageHeader header = new(ArbitrumL1MessageKind.L2Message, Address.SystemUser, 100UL, 1000UL, Hash256.Zero, UInt256.One);
        L1IncomingMessage l1IncomingMessage = new(header, [1, 2, 3], null, null);
        MessageWithMetadata msgWithMeta = new(l1IncomingMessage, 5UL);
        MessageResultForRpc msgResult = new() { Hash = Keccak.Compute("test"), SendRoot = Keccak.Compute("root") };
        byte[] blockMetadata = [10, 20, 30];

        SequencedMsg original = new(42UL, msgWithMeta, msgResult, blockMetadata);

        string json = JsonSerializer.Serialize(original, EthereumJsonSerializer.JsonOptions);
        SequencedMsg? deserialized = JsonSerializer.Deserialize<SequencedMsg>(json, EthereumJsonSerializer.JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized.MsgIdx.Should().Be(42UL);
        deserialized.MsgWithMeta.DelayedMessagesRead.Should().Be(5UL);
        deserialized.BlockMetadata.Should().BeEquivalentTo(blockMetadata);
    }
}
