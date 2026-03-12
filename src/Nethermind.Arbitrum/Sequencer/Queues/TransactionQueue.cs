// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Concurrent;
using System.Threading.Channels;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Sequencer.Queues;

/// <summary>
/// Bounded channel-based user transaction queue with per-tx result notification.
/// </summary>
public class TransactionQueue(int capacity, int maxTxDataSize, bool awaitTxResult, int expressLaneAdvantageMs, IExpressLaneTracker expressLaneTracker)
{
    private readonly Channel<TxQueueItem> _channel = Channel.CreateBounded<TxQueueItem>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });
    private readonly ConcurrentQueue<TxQueueItem> _retryQueue = new();

    /// <summary>
    /// Enqueues an item and returns a task that completes when the tx is included in a block
    /// or rejected.
    /// </summary>
    public async Task<ResultWrapper<Hash256>> EnqueueAsync(TxQueueItem item)
    {
        if (item.RlpEncoded.Length > maxTxDataSize)
            return ResultWrapper<Hash256>.Fail($"Transaction data size {item.RlpEncoded.Length} exceeds maximum {maxTxDataSize}", ErrorCodes.TransactionRejected);

        if (!item.IsTimeboosted && expressLaneAdvantageMs > 0 && expressLaneTracker.CurrentRoundHasController())
            await Task.Delay(expressLaneAdvantageMs, item.CancellationToken);

        try
        {
            await _channel.Writer.WriteAsync(item, item.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultWrapper<Hash256>.Fail("Transaction queue timeout", ErrorCodes.TransactionRejected);
        }

        if (awaitTxResult)
        {
            Exception? err = await item.ResultChannel.Task;
            if (err is not null)
                return ResultWrapper<Hash256>.Fail(err.Message, ErrorCodes.TransactionRejected);
        }

        return ResultWrapper<Hash256>.Success(item.Tx.Hash!);
    }

    /// <summary>
    /// Writes an item to the channel without awaiting block inclusion.
    /// </summary>
    public async Task<ResultWrapper<Hash256>> WriteChannelAsync(TxQueueItem item)
    {
        if (item.RlpEncoded.Length > maxTxDataSize)
            return ResultWrapper<Hash256>.Fail($"Transaction data size {item.RlpEncoded.Length} exceeds maximum {maxTxDataSize}", ErrorCodes.TransactionRejected);

        try
        {
            await _channel.Writer.WriteAsync(item, item.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultWrapper<Hash256>.Fail("Transaction queue timeout", ErrorCodes.TransactionRejected);
        }

        return ResultWrapper<Hash256>.Success(item.Tx.Hash!);
    }

    /// <summary>
    /// Drains available items from the retry queue first, then the main channel.
    /// </summary>
    public List<TxQueueItem> DrainBatch()
    {
        List<TxQueueItem> items = new(capacity);

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
