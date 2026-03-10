// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Sequencer.Queues;

[TestFixture]
public class DelayedMessageQueueTests
{
    [Test]
    public void DelayedMessageQueue_EnqueueDequeue_FifoOrder()
    {
        DelayedMessageQueue queue = new();
        L1IncomingMessageHeader header = new(ArbitrumL1MessageKind.L2Message, Address.SystemUser, 1UL, 1000UL, null, UInt256.Zero);
        L1IncomingMessage[] messages =
        [
            new(header, [1], null, null),
            new(header, [2], null, null),
            new(header, [3], null, null)
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
        L1IncomingMessageHeader header = new(ArbitrumL1MessageKind.L2Message, Address.SystemUser, 1UL, 1000UL, null, UInt256.Zero);
        L1IncomingMessage[] messages =
        [
            new(header, [1], null, null),
            new(header, [2], null, null)
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
        L1IncomingMessageHeader header = new(ArbitrumL1MessageKind.L2Message, Address.SystemUser, 1UL, 1000UL, null, UInt256.Zero);
        L1IncomingMessage[] messages =
        [
            new(header, [1], null, null),
            new(header, [2], null, null),
            new(header, [3], null, null)
        ];

        queue.Enqueue(messages, 5UL);

        queue.TryPeekTail(out DelayedMessage? tail).Should().BeTrue();
        tail!.MessageIndex.Should().Be(7UL);
        tail.Message.L2Msg.Should().BeEquivalentTo(new byte[] { 3 });
    }

    [Test]
    public void DelayedMessageQueue_EnqueueAfterClear_Works()
    {
        DelayedMessageQueue queue = new();
        L1IncomingMessageHeader header = new(ArbitrumL1MessageKind.L2Message, Address.SystemUser, 1UL, 1000UL, null, UInt256.Zero);

        queue.Enqueue([new(header, [1], null, null), new(header, [2], null, null)], 0UL);
        queue.Clear();

        queue.TryDequeue(out _).Should().BeFalse();
        queue.TryPeekTail(out _).Should().BeFalse();

        queue.Enqueue([new(header, [10], null, null), new(header, [20], null, null)], 50UL);

        queue.TryDequeue(out DelayedMessage? first).Should().BeTrue();
        first!.MessageIndex.Should().Be(50UL);
        first.Message.L2Msg.Should().BeEquivalentTo(new byte[] { 10 });

        queue.TryDequeue(out DelayedMessage? second).Should().BeTrue();
        second!.MessageIndex.Should().Be(51UL);
        second.Message.L2Msg.Should().BeEquivalentTo(new byte[] { 20 });

        queue.TryPeekTail(out DelayedMessage? tail).Should().BeTrue();
        tail!.MessageIndex.Should().Be(51UL);
    }
}
