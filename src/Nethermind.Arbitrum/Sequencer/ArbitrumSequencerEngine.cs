// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Blockchain;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Sequencer;

public class ArbitrumSequencerEngine(
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    IArbitrumSpecHelper specHelper,
    DelayedMessageQueue delayedMessageQueue,
    SequencerState sequencerState,
    SemaphoreSlim createBlocksSemaphore,
    CachedL1PriceData cachedL1PriceData,
    ILogManager logManager,
    IArbitrumConfig arbitrumConfig,
    IStateReader stateReader,
    TransactionQueue transactionQueue,
    IExpressLaneService? expressLaneService = null,
    AuctionResolutionQueue? auctionResolutionQueue = null)
{

    private readonly NonceCache _nonceCache = new(arbitrumConfig.SequencerNonceCacheSize);
    private readonly NonceFailureCache _nonceFailureCache = new(arbitrumConfig.SequencerNonceCacheSize);
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumSequencerEngine>();

    // Pooled collections reused across PrecheckNonces calls to avoid per-block hash table/queue allocations.
    private readonly Dictionary<Address, ulong> _pendingNonces = new();
    private readonly Queue<TxQueueItem> _extraItems = new();

    private SequencedBlockInfo? _lastSequencedBlockInfo;
    private SequencedBlockInfo? _lastCreatedBlockWithRegularTxsInfo;
    private List<TxQueueItem>? _lastRegularTxQueueItems;

    public TransactionQueue TransactionQueue { get; } = transactionQueue;

    public async Task<StartSequencingResult> StartSequencingAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp)
    {
        if (!sequencerState.IsActive)
        {
            if (sequencerState.Mode == SequencerMode.Forwarding)
            {
                List<TxQueueItem> pendingItems = TransactionQueue.DrainBatch();
                if (pendingItems.Count > 0)
                    await HandleInactiveAsync(pendingItems);
            }

            return new StartSequencingResult(null, arbitrumConfig.SequencerInactiveWaitMs);
        }

        SequencedMsg? result = await SequenceDelayedMessageAsync();
        if (result is not null)
            return new StartSequencingResult(result, 0);

        // Timeboost: give auction resolution transactions priority over all other work
        if (auctionResolutionQueue is not null && auctionResolutionQueue.Reader.TryRead(out TxQueueItem? auctionItem))
        {
            result = await CreateBlockWithSingleTxAsync(auctionItem, l1BlockNumber, l1Timestamp, timestamp);
            if (result is not null)
                return new StartSequencingResult(result, 0);
        }

        result = await CreateBlockWithRegularTxsAsync(l1BlockNumber, l1Timestamp, timestamp);
        if (result is not null)
            return new StartSequencingResult(result, 0);

        _logger.Warn("Nothing to sequence, waiting...");

        return new StartSequencingResult(null, arbitrumConfig.SequencerMaxBlockSpeedMs);
    }

    public void EndSequencing(string? error)
    {
        if (_lastCreatedBlockWithRegularTxsInfo is null)
            return;

        List<TxQueueItem>? queueItems = _lastRegularTxQueueItems;
        Block block = _lastCreatedBlockWithRegularTxsInfo.Block;

        _lastCreatedBlockWithRegularTxsInfo = null;
        _lastRegularTxQueueItems = null;

        if (queueItems is null)
            return;

        if (error is not null)
        {
            foreach (TxQueueItem item in queueItems)
                TransactionQueue.PushRetry(item);
            return;
        }

        _nonceCache.Finalize(block);

        // Arbitrum includes all sequenced txs in the block; execution failures are visible via receipt StatusCode
        foreach (TxQueueItem item in queueItems)
            item.ReturnResult(null);
    }

    public async Task AppendLastSequencedBlockAsync()
    {
        await createBlocksSemaphore.WaitAsync();
        try
        {
            if (_lastSequencedBlockInfo is null)
            {
                if (_logger.IsWarn)
                    _logger.Warn("AppendLastSequencedBlock called but no sequenced block info available");
                return;
            }

            cachedL1PriceData.CacheL1PriceDataOfMsg(
                _lastSequencedBlockInfo.MsgIdx,
                Array.Empty<TxReceipt>(),
                _lastSequencedBlockInfo.Block,
                blockBuiltUsingDelayedMessage: true);

            _lastSequencedBlockInfo = null;
        }
        finally
        {
            createBlocksSemaphore.Release();
        }
    }

    public void EnqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx)
    {
        delayedMessageQueue.Enqueue(messages, firstMsgIdx);

        if (_logger.IsDebug)
            _logger.Debug($"Enqueued {messages.Length} delayed messages starting at index {firstMsgIdx}");
    }

    public ulong NextDelayedMessageNumber()
    {
        if (delayedMessageQueue.TryPeekTail(out DelayedMessage? tail))
            return tail!.MessageIndex + 1;

        return blockTree.Head!.Header.Nonce;
    }

    public async Task<SequencedMsg?> ResequenceReorgedMessageAsync(MessageWithMetadata? msg)
    {
        if (msg?.Message?.Header is null)
            return null;

        await createBlocksSemaphore.WaitAsync();
        try
        {
            BlockHeader currentHeader = blockTree.Head!.Header;

            if (msg.Message.Header.RequestId is not null)
            {
                ulong delayedMsgIdx = BinaryPrimitives.ReadUInt64BigEndian(msg.Message.Header.RequestId.Bytes.Slice(24));

                if (delayedMsgIdx != currentHeader.Nonce)
                {
                    if (_logger.IsInfo)
                        _logger.Info($"Not resequencing delayed message due to unexpected index, expected {currentHeader.Nonce} found {delayedMsgIdx}");
                    return null;
                }

                return await SequenceDelayedMessageWithBlockMutexAsync(msg.Message, delayedMsgIdx);
            }

            return await ResequenceRegularMessageWithBlockMutexAsync(msg);
        }
        finally
        {
            createBlocksSemaphore.Release();
        }
    }

    public void Pause()
    {
        sequencerState.Pause();

        if (_logger.IsInfo)
            _logger.Info("Sequencer paused");
    }

    public void Activate()
    {
        sequencerState.Activate();

        if (_logger.IsInfo)
            _logger.Info("Sequencer activated");
    }

    public void ForwardTo(string url)
    {
        sequencerState.ForwardTo(url);

        if (_logger.IsInfo)
            _logger.Info($"Sequencer forwarding to {url}");
    }

    /// <summary>
    /// Handles the inactive (forwarding) state by forwarding queued transactions to the backup sequencer.
    /// Mirrors Go handleInactive in sequencer.go.
    /// </summary>
    private async Task HandleInactiveAsync(List<TxQueueItem> queueItems)
    {
        TransactionForwarder? forwarder = sequencerState.Forwarder;
        if (forwarder is null)
            return;

        Task<(TxQueueItem Item, Exception? Error)>[] forwardTasks = new Task<(TxQueueItem, Exception?)>[queueItems.Count];

        for (int i = 0; i < queueItems.Count; i++)
        {
            TxQueueItem item = queueItems[i];
            forwardTasks[i] = ForwardSingleAsync(forwarder, item);
        }

        (TxQueueItem Item, Exception? Error)[] results = await Task.WhenAll(forwardTasks);

        foreach ((TxQueueItem item, Exception? error) in results)
            if (error is NoSequencerException)
                TransactionQueue.PushRetry(item);
            else
                item.ReturnResult(error);

        _nonceFailureCache.Clear();
    }

    private static async Task<(TxQueueItem Item, Exception? Error)> ForwardSingleAsync(
        TransactionForwarder forwarder, TxQueueItem item)
    {
        Exception? error = await forwarder.ForwardTransactionAsync(item.RlpEncoded, item.CancellationToken);
        return (item, error);
    }

    private async Task<SequencedMsg?> CreateBlockWithSingleTxAsync(TxQueueItem item, ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp)
    {
        if (!await createBlocksSemaphore.WaitAsync(0))
        {
            // Return auction resolution tx to queue for retry
            if (auctionResolutionQueue is not null)
                await auctionResolutionQueue.Writer.WriteAsync(item);

            return null;
        }

        try
        {
            return await CreateBlockWithRegularTxsWithMutexAsync([item], l1BlockNumber, timestamp);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error creating block with auction resolution tx: {ex.Message}", ex);

            item.ReturnResult(ex);
            return null;
        }
        finally
        {
            createBlocksSemaphore.Release();
        }
    }

    private async Task<SequencedMsg?> CreateBlockWithRegularTxsAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp)
    {
        // Timeboost: if a controller exists for the current round, delay regular txs
        // to give express lane transactions time to arrive first.
        if (arbitrumConfig.TimeboostEnabled && expressLaneService?.CurrentRoundHasController() == true)
            await Task.Delay(arbitrumConfig.TimeboostExpressLaneAdvantageMs);

        List<TxQueueItem> queueItems = TransactionQueue.DrainBatch();

        if (queueItems.Count == 0)
            return null;

        ulong currentBlock = (ulong)blockTree.Head!.Header.Number;

        // Timeboost: evict expired timeboosted txs before nonce check
        if (arbitrumConfig.TimeboostEnabled)
        {
            int writeIdx = 0;
            for (int i = 0; i < queueItems.Count; i++)
            {
                TxQueueItem it = queueItems[i];
                if (it.IsTimeboosted && it.BlockStamp != 0
                    && currentBlock >= it.BlockStamp + arbitrumConfig.TimeboostQueueTimeoutInBlocks)
                {
                    it.ReturnResult(new InvalidOperationException("Timeboosted tx expired (block-based timeout)"));
                    continue;
                }
                queueItems[writeIdx++] = it;
            }
            queueItems.RemoveRange(writeIdx, queueItems.Count - writeIdx);
        }

        int cancelWriteIdx = 0;
        for (int i = 0; i < queueItems.Count; i++)
        {
            if (queueItems[i].CancellationToken.IsCancellationRequested)
                queueItems[i].ReturnResult(new OperationCanceledException());
            else
                queueItems[cancelWriteIdx++] = queueItems[i];
        }
        queueItems.RemoveRange(cancelWriteIdx, queueItems.Count - cancelWriteIdx);

        if (queueItems.Count == 0)
            return null;

        ulong timestampDelta = l1Timestamp > timestamp ? l1Timestamp - timestamp : timestamp - l1Timestamp;
        if (l1BlockNumber == 0 || timestampDelta > (ulong)arbitrumConfig.SequencerMaxAcceptableTimestampDelta)
        {
            foreach (TxQueueItem item in queueItems)
                TransactionQueue.PushRetry(item);

            if (_logger.IsError)
                _logger.Error($"Cannot sequence: unknown L1 block or L1 timestamp too far from local clock time, " +
                    $"l1Block={l1BlockNumber}, l1Timestamp={l1Timestamp}, localTimestamp={timestamp}");
            return null;
        }

        _nonceCache.BeginNewBlock();
        _nonceFailureCache.EvictExpired();
        queueItems = PrecheckNonces(queueItems);

        if (queueItems.Count == 0)
            return null;

        if (!await createBlocksSemaphore.WaitAsync(0))
        {
            foreach (TxQueueItem item in queueItems)
                TransactionQueue.PushRetry(item);

            if (_logger.IsDebug)
                _logger.Debug("Could not acquire block creation semaphore for user transaction sequencing");
            return null;
        }

        try
        {
            return await CreateBlockWithRegularTxsWithMutexAsync(queueItems, l1BlockNumber, timestamp);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error creating block with regular transactions: {ex.Message}", ex);

            foreach (TxQueueItem item in queueItems)
                TransactionQueue.PushRetry(item);

            return null;
        }
        finally
        {
            createBlocksSemaphore.Release();
        }
    }

    private async Task<SequencedMsg?> CreateBlockWithRegularTxsWithMutexAsync(List<TxQueueItem> queueItems, ulong l1BlockNumber, ulong timestamp)
    {
        BlockHeader head = blockTree.Head?.Header
            ?? throw new InvalidOperationException("BlockTree.Head is null");

        Transaction[] transactions = new Transaction[queueItems.Count];
        byte[][] rlpEncodedTxs = new byte[queueItems.Count][];
        HashSet<Hash256>? timeboostedTxHashes = arbitrumConfig.TimeboostEnabled ? new() : null;

        for (int i = 0; i < queueItems.Count; i++)
        {
            transactions[i] = queueItems[i].Tx;
            rlpEncodedTxs[i] = queueItems[i].RlpEncoded;
            if (timeboostedTxHashes is not null && queueItems[i].IsTimeboosted && queueItems[i].Tx.Hash is not null)
                timeboostedTxHashes.Add(queueItems[i].Tx.Hash!);
        }

        MessageWithMetadata messageWithMetadata =
            L2MessageAssembler.AssembleFromSignedTransactions(rlpEncodedTxs, l1BlockNumber, timestamp, head.Nonce);

        long blockNumber = head.Number + 1;
        ulong msgIdx = MessageBlockConverter.BlockNumberToMessageIndex((ulong)blockNumber, specHelper);

        ArbitrumBlockHeaderInfo prevHeaderInfo = ArbitrumBlockHeaderInfo.Deserialize(head, _logger);
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber,
            PreviousArbosVersion = prevHeaderInfo.ArbOSFormatVersion
        };

        Block? block = await trigger.BuildBlock(parentHeader: head, payloadAttributes: payload);
        if (block?.Hash is null)
        {
            foreach (TxQueueItem item in queueItems)
                TransactionQueue.PushRetry(item);
            return null;
        }

        _logger.Warn($"Created block with regular {transactions.Length} transactions");

        _lastCreatedBlockWithRegularTxsInfo = new SequencedBlockInfo(block, msgIdx);
        _lastRegularTxQueueItems = queueItems;

        if (_logger.IsInfo)
            _logger.Info($"Created block with {queueItems.Count} user txs, msgIdx={msgIdx}, blockNumber={block.Number}");

        return BuildSequencedMsg(block, msgIdx, messageWithMetadata, timeboostedTxHashes);
    }

    /// <summary>
    /// Validates transaction nonces against the nonce cache.
    /// </summary>
    private List<TxQueueItem> PrecheckNonces(List<TxQueueItem> queueItems)
    {
        BlockHeader head = blockTree.Head!.Header;
        List<TxQueueItem> output = new(queueItems.Count);
        _pendingNonces.Clear();
        _extraItems.Clear();

        int idx = 0;
        while (idx < queueItems.Count || _extraItems.Count > 0)
        {
            TxQueueItem item;
            if (_extraItems.Count > 0)
                item = _extraItems.Dequeue();
            else
            {
                item = queueItems[idx];
                idx++;
            }

            Address? sender = item.Tx.SenderAddress;
            if (sender is null)
            {
                item.ReturnResult(new InvalidOperationException("Transaction has no sender"));
                continue;
            }

            ulong stateNonce = _nonceCache.Get(head, stateReader, sender);
            if (!_pendingNonces.TryGetValue(sender, out ulong pendingNonce))
                pendingNonce = stateNonce;

            ulong txNonce = (ulong)item.Tx.Nonce;

            if (txNonce == pendingNonce)
            {
                _pendingNonces[sender] = txNonce + 1;
                _nonceCache.Update(head, sender, txNonce + 1);

                if (_nonceFailureCache.TryRevive(sender, txNonce + 1, out TxQueueItem? revived))
                {
                    if (revived!.CancellationToken.IsCancellationRequested)
                        revived.ReturnResult(new OperationCanceledException());
                    else
                        _extraItems.Enqueue(revived);
                }

                output.Add(item);
            }
            else if (txNonce < stateNonce)
                item.ReturnResult(new InvalidOperationException($"Nonce too low: sender={sender}, tx nonce={txNonce}, state nonce={stateNonce}"));
            else if (txNonce > pendingNonce)
                _nonceFailureCache.Add(sender, txNonce, item);
            else
                // May succeed if earlier txs in this batch fail
                output.Add(item);
        }

        return output;
    }

    private async Task<SequencedMsg?> ResequenceRegularMessageWithBlockMutexAsync(MessageWithMetadata msg)
    {
        BlockHeader head = blockTree.Head?.Header
            ?? throw new InvalidOperationException("Unable to sequence regular message as BlockTree.Head is null");

        long blockNumber = head.Number + 1;
        ulong msgIdx = MessageBlockConverter.BlockNumberToMessageIndex((ulong)blockNumber, specHelper);

        ArbitrumBlockHeaderInfo prevHeaderInfo = ArbitrumBlockHeaderInfo.Deserialize(head, _logger);
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = msg,
            Number = blockNumber,
            PreviousArbosVersion = prevHeaderInfo.ArbOSFormatVersion
        };

        Block? block = await trigger.BuildBlock(parentHeader: head, payloadAttributes: payload);
        if (block?.Hash is null)
            throw new InvalidOperationException($"Failed to build block {blockNumber} or block has no hash.");

        if (_logger.IsInfo)
            _logger.Info($"Resequenced regular message, msgIdx={msgIdx}, blockNumber={block.Number}");

        return BuildSequencedMsg(block, msgIdx, msg, null);
    }

    private async Task<SequencedMsg?> SequenceDelayedMessageAsync()
    {
        if (!delayedMessageQueue.TryDequeue(out DelayedMessage? delayedMessage))
            return null;

        if (!await createBlocksSemaphore.WaitAsync(0))
        {
            if (_logger.IsDebug)
                _logger.Debug("Could not acquire block creation semaphore for delayed message sequencing");
            return null;
        }

        try
        {
            return await SequenceDelayedMessageWithBlockMutexAsync(delayedMessage!.Message, delayedMessage.MessageIndex);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error sequencing delayed message at index {delayedMessage!.MessageIndex}: {ex.Message}", ex);
            delayedMessageQueue.Clear();
            throw;
        }
        finally
        {
            createBlocksSemaphore.Release();
        }
    }

    private async Task<SequencedMsg?> SequenceDelayedMessageWithBlockMutexAsync(L1IncomingMessage message, ulong delayedMsgIdx)
    {
        BlockHeader head = blockTree.Head?.Header
            ?? throw new InvalidOperationException($"Unable to sequence delayed message {delayedMsgIdx} as BlockTree.Head is null");

        ulong expectedDelayedMsgIdx = head.Nonce;
        if (expectedDelayedMsgIdx != delayedMsgIdx)
        {
            if (_logger.IsError)
                _logger.Error($"Wrong delayed message sequenced got {delayedMsgIdx} expected {expectedDelayedMsgIdx}");
            return null;
        }

        long blockNumber = head.Number + 1;
        ulong msgIdx = MessageBlockConverter.BlockNumberToMessageIndex((ulong)blockNumber, specHelper);

        MessageWithMetadata messageWithMetadata = new(message, delayedMsgIdx + 1);

        ArbitrumBlockHeaderInfo prevHeaderInfo = ArbitrumBlockHeaderInfo.Deserialize(head, _logger);
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber,
            PreviousArbosVersion = prevHeaderInfo.ArbOSFormatVersion
        };

        Block? block = await trigger.BuildBlock(parentHeader: head, payloadAttributes: payload);
        if (block?.Hash is null)
            throw new InvalidOperationException($"Failed to build block {blockNumber} or block has no hash for delayed message index {delayedMsgIdx}.");

        _lastSequencedBlockInfo = new SequencedBlockInfo(block, msgIdx);

        _logger.Warn($"Sequenced block with delayed message {msgIdx}");

        if (_logger.IsInfo)
            _logger.Info($"Added DelayedMessage, msgIdx={msgIdx}, delayedMsgIdx={delayedMsgIdx}, blockNumber={block.Number}");

        return BuildSequencedMsg(block, msgIdx, messageWithMetadata, null);
    }

    private SequencedMsg BuildSequencedMsg(
        Block block,
        ulong msgIdx,
        MessageWithMetadata messageWithMetadata,
        HashSet<Hash256>? timeboostedTxHashes)
    {
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, _logger);
        byte[] blockMetadata = new byte[1 + (block.Transactions.Length + 7) / 8];

        // Populate timeboosted bitmap: byte 0 = flags, bytes 1..N = bitmap (1 bit per tx)
        if (timeboostedTxHashes is not null)
        {
            for (int i = 0; i < block.Transactions.Length; i++)
            {
                Hash256? hash = block.Transactions[i].Hash;
                if (hash is not null && timeboostedTxHashes.Contains(hash))
                    blockMetadata[1 + i / 8] |= (byte)(1 << (i % 8));
            }
        }

        MessageResultForRpc msgResult = new()
        {
            Hash = block.Hash!,
            SendRoot = headerInfo.SendRoot
        };

        return new SequencedMsg(msgIdx, messageWithMetadata, msgResult, blockMetadata);
    }

    private record SequencedBlockInfo(Block Block, ulong MsgIdx);
}
