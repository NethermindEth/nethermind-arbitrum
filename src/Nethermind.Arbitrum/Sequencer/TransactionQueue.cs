// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Threading.Channels;
using Nethermind.Core;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Bounded channel-based user transaction queue with per-tx result notification.
/// </summary>
public class TransactionQueue(int capacity, int maxTxDataSize)
{
    private readonly Channel<TxQueueItem> _channel = Channel.CreateBounded<TxQueueItem>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });
    private readonly ConcurrentQueue<TxQueueItem> _retryQueue = new();

    /// <summary>
    /// Enqueues a transaction and returns a task that completes when the tx is included in a block
    /// or rejected. Returns null on success, an exception on failure.
    /// </summary>
    public async Task<Exception?> EnqueueAsync(Transaction tx, CancellationToken ct)
    {
        int txSize = Rlp.Encode(tx).Bytes.Length;
        if (txSize > maxTxDataSize)
            return new InvalidOperationException($"Transaction data size {txSize} exceeds maximum {maxTxDataSize}");

        TxQueueItem item = new(tx, ct);

        if (!_channel.Writer.TryWrite(item))
            return new InvalidOperationException("Transaction queue is full");

        return await item.ResultChannel.Task;
    }

    /// <summary>
    /// Drains available items from the retry queue first, then the main channel.
    /// </summary>
    public List<TxQueueItem> DrainBatch()
    {
        List<TxQueueItem> items = new();

        while (_retryQueue.TryDequeue(out TxQueueItem? retryItem))
            items.Add(retryItem);

        if (items.Count == 0)
        {
            if (_channel.Reader.TryRead(out TxQueueItem? firstItem))
                items.Add(firstItem);
            else
                return items;
        }

        while (_channel.Reader.TryRead(out TxQueueItem? item))
            items.Add(item);

        return items;
    }

    /// <summary>
    /// Push a transaction back to the retry queue for the next block attempt.
    /// </summary>
    public void PushRetry(TxQueueItem item)
    {
        _retryQueue.Enqueue(item);
    }
}
