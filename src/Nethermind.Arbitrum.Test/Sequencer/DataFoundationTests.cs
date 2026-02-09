// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Sequencer;

public class DataFoundationTests
{
    [Test]
    public void SequencedMsg_Serialization_RoundTrip()
    {
        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.L2Message,
            Address.SystemUser,
            100UL,
            1000UL,
            Hash256.Zero,
            UInt256.One);
        MessageWithMetadata msgWithMeta = new(
            new L1IncomingMessage(header, new byte[] { 1, 2, 3 }, null, null),
            5UL);
        MessageResultForRpc msgResult = new() { Hash = Keccak.Compute("test"), SendRoot = Keccak.Compute("root") };
        byte[] blockMetadata = new byte[] { 10, 20, 30 };

        SequencedMsg original = new(42UL, msgWithMeta, msgResult, blockMetadata);

        string json = JsonSerializer.Serialize(original, EthereumJsonSerializer.JsonOptions);
        SequencedMsg? deserialized = JsonSerializer.Deserialize<SequencedMsg>(json, EthereumJsonSerializer.JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.MsgIdx.Should().Be(42UL);
        deserialized.MsgWithMeta.DelayedMessagesRead.Should().Be(5UL);
        deserialized.BlockMetadata.Should().BeEquivalentTo(blockMetadata);
    }

    [Test]
    public void DelayedMessageQueue_EnqueueDequeue_FifoOrder()
    {
        DelayedMessageQueue queue = new();
        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.L2Message,
            Address.SystemUser,
            1UL, 1000UL, null, UInt256.Zero);
        L1IncomingMessage[] messages =
        [
            new(header, new byte[] { 1 }, null, null),
            new(header, new byte[] { 2 }, null, null),
            new(header, new byte[] { 3 }, null, null)
        ];

        queue.Enqueue(messages, 10UL);

        queue.TryDequeue(out DelayedMessage? first).Should().BeTrue();
        first!.MessageIndex.Should().Be(10UL);
        first.Message.L2Msg.Should().BeEquivalentTo(new byte[] { 1 });

        queue.TryDequeue(out DelayedMessage? second).Should().BeTrue();
        second!.MessageIndex.Should().Be(11UL);
        second.Message.L2Msg.Should().BeEquivalentTo(new byte[] { 2 });

        queue.TryDequeue(out DelayedMessage? third).Should().BeTrue();
        third!.MessageIndex.Should().Be(12UL);
        third.Message.L2Msg.Should().BeEquivalentTo(new byte[] { 3 });

        queue.TryDequeue(out _).Should().BeFalse();
    }

    [Test]
    public void DelayedMessageQueue_Clear_EmptiesQueue()
    {
        DelayedMessageQueue queue = new();
        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.L2Message,
            Address.SystemUser,
            1UL, 1000UL, null, UInt256.Zero);
        L1IncomingMessage[] messages =
        [
            new(header, new byte[] { 1 }, null, null),
            new(header, new byte[] { 2 }, null, null)
        ];
        queue.Enqueue(messages, 0UL);

        queue.Clear();

        queue.TryDequeue(out _).Should().BeFalse();
        queue.TryPeekTail(out _).Should().BeFalse();
    }

    [Test]
    public void DelayedMessageQueue_TryPeekTail_ReturnsLast()
    {
        DelayedMessageQueue queue = new();
        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.L2Message,
            Address.SystemUser,
            1UL, 1000UL, null, UInt256.Zero);
        L1IncomingMessage[] messages =
        [
            new(header, new byte[] { 1 }, null, null),
            new(header, new byte[] { 2 }, null, null),
            new(header, new byte[] { 3 }, null, null)
        ];

        queue.Enqueue(messages, 5UL);

        queue.TryPeekTail(out DelayedMessage? tail).Should().BeTrue();
        tail!.MessageIndex.Should().Be(7UL);
        tail.Message.L2Msg.Should().BeEquivalentTo(new byte[] { 3 });
    }

    [Test]
    public void SequencerState_Transitions_FollowStateMachine()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.Mode.Should().Be(SequencerMode.Inactive);

        state.Activate();
        state.Mode.Should().Be(SequencerMode.Active);
        state.Forwarder.Should().BeNull();

        state.Pause();
        state.Mode.Should().Be(SequencerMode.Paused);
        state.Forwarder.Should().BeNull();

        state.ForwardTo("https://sequencer.example.com");
        state.Mode.Should().Be(SequencerMode.Forwarding);
        state.Forwarder.Should().NotBeNull();
        state.Forwarder!.PrimaryTarget.Should().Be("https://sequencer.example.com");

        state.Activate();
        state.Mode.Should().Be(SequencerMode.Active);
        state.Forwarder.Should().BeNull();
    }

    [Test]
    public void SequencerState_IsActive_OnlyWhenActive()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.IsActive.Should().BeFalse();

        state.Activate();
        state.IsActive.Should().BeTrue();

        state.Pause();
        state.IsActive.Should().BeFalse();

        state.ForwardTo("https://sequencer.example.com");
        state.IsActive.Should().BeFalse();
    }
}
