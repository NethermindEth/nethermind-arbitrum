// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Threading.Channels;
using Nethermind.Arbitrum.Data;

namespace Nethermind.Arbitrum.Sequencer.Queues;

public class DelayedMessageQueue
{
    private volatile Channel<DelayedMessage> _channel = CreateChannel();
    private volatile DelayedMessage? _tail;

    public void Enqueue(L1IncomingMessage[] messages, ulong firstMsgIdx)
    {
        Channel<DelayedMessage> channel = _channel;
        for (int i = 0; i < messages.Length; i++)
        {
            DelayedMessage msg = new(messages[i], firstMsgIdx + (ulong)i);
            channel.Writer.TryWrite(msg);
            _tail = msg;
        }
    }

    public bool TryDequeue(out DelayedMessage? message)
    {
        bool result = _channel.Reader.TryRead(out DelayedMessage? msg);
        message = msg;
        return result;
    }

    public bool TryPeekTail(out DelayedMessage? message)
    {
        message = _tail;
        return message is not null;
    }

    public void Clear()
    {
        _tail = null;
        Channel<DelayedMessage> old = _channel;
        _channel = CreateChannel();
        old.Writer.Complete();
    }

    private static Channel<DelayedMessage> CreateChannel() =>
        Channel.CreateUnbounded<DelayedMessage>(new UnboundedChannelOptions { SingleReader = true });
}
