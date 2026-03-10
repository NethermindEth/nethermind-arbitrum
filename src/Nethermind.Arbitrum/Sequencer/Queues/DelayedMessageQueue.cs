// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Concurrent;
using Nethermind.Arbitrum.Data;

namespace Nethermind.Arbitrum.Sequencer.Queues;

public class DelayedMessageQueue
{
    private volatile ConcurrentQueue<DelayedMessage> _queue = new();
    private volatile DelayedMessage? _tail;

    public void Enqueue(L1IncomingMessage[] messages, ulong firstMsgIdx)
    {
        ConcurrentQueue<DelayedMessage> queue = _queue;
        for (int i = 0; i < messages.Length; i++)
        {
            DelayedMessage msg = new(messages[i], firstMsgIdx + (ulong)i);
            queue.Enqueue(msg);
            _tail = msg;
        }
    }

    public bool TryDequeue(out DelayedMessage? message)
        => _queue.TryDequeue(out message);

    public bool TryPeekTail(out DelayedMessage? message)
    {
        message = _tail;
        return message is not null;
    }

    public void Clear()
    {
        _tail = null;
        _queue = new();
    }
}
