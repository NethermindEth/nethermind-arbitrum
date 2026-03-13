// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Concurrent;
using System.Threading.Channels;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Sequencer.Queues;

/// <summary>
/// Bounded channel-based user transaction queue with per-tx result notification.
/// </summary>
public class TransactionQueue(IArbitrumConfig config, IExpressLaneTracker expressLaneTracker)
{
    private readonly int _expressLaneAdvantageMs = config.TimeboostEnabled ? config.TimeboostExpressLaneAdvantageMs : 0;

    private readonly Channel<TxQueueItem> _channel = Channel.CreateBounded<TxQueueItem>(new BoundedChannelOptions(config.SequencerMaxTxQueueSize)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true
    });
    private readonly ConcurrentQueue<TxQueueItem> _retryQueue = new();

    /// <summary>
    /// Enqueues an item and returns a task that completes when the tx is included in a block
    /// or rejected.
    /// </summary>
    public async Task<ResultWrapper<Hash256>> EnqueueAsync(TxQueueItem item, bool awaitForCompletion = true)
    {
        if (item.RlpEncoded.Length > config.SequencerMaxTxDataSize)
            return ResultWrapper<Hash256>.Fail($"Transaction data size {item.RlpEncoded.Length} exceeds maximum {config.SequencerMaxTxDataSize}", ErrorCodes.TransactionRejected);

        if (!item.IsTimeboosted && _expressLaneAdvantageMs > 0 && expressLaneTracker.CurrentRoundHasController())
            await Task.Delay(_expressLaneAdvantageMs, item.CancellationToken);

        try
        {
            await _channel.Writer.WriteAsync(item, item.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            return ResultWrapper<Hash256>.Fail("Transaction queue timeout", ErrorCodes.TransactionRejected);
        }

        if (awaitForCompletion && config.SequencerAwaitTxResult)
        {
            Exception? err = await item.ResultChannel.Task;
            if (err is not null)
                return ResultWrapper<Hash256>.Fail(err.Message, ErrorCodes.TransactionRejected);
        }

        return ResultWrapper<Hash256>.Success(item.Tx.Hash!);
    }

    /// <summary>
    /// Drains available items from the retry queue first, then the main channel.
    /// </summary>
    public List<TxQueueItem> DrainBatch()
    {
        List<TxQueueItem> items = new(config.SequencerMaxTxQueueSize);

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
