// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading.Channels;
using Nethermind.Arbitrum.Data;

namespace Nethermind.Arbitrum.Sequencer;

public class DelayedMessageQueue
{
    private readonly Lock _lock = new();
    private Channel<DelayedMessage> _channel = Channel.CreateUnbounded<DelayedMessage>(
        new UnboundedChannelOptions { SingleReader = true });
    private DelayedMessage? _tail;

    public void Enqueue(L1IncomingMessage[] messages, ulong firstMsgIdx)
    {
        lock (_lock)
        {
            for (int i = 0; i < messages.Length; i++)
            {
                DelayedMessage msg = new(messages[i], firstMsgIdx + (ulong)i);
                _channel.Writer.TryWrite(msg);
                _tail = msg;
            }
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
        lock (_lock)
        {
            message = _tail;
            return message is not null;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _channel.Writer.Complete();
            _channel = Channel.CreateUnbounded<DelayedMessage>(
                new UnboundedChannelOptions { SingleReader = true });
            _tail = null;
        }
    }
}
