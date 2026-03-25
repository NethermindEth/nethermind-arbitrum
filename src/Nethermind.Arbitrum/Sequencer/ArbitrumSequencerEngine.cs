// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Sequencer;

public sealed class ArbitrumSequencerEngine(
    ArbitrumBlockFactory factory,
    IBlockTree blockTree,
    IArbitrumSpecHelper specHelper,
    DelayedMessageQueue delayedMessageQueue,
    SequencerState sequencerState,
    CachedL1PriceData cachedL1PriceData,
    ILogManager logManager,
    IArbitrumConfig arbitrumConfig,
    IStateReader stateReader,
    TransactionQueue transactionQueue,
    IAuctionResolutionQueue auctionResolutionQueue)
    : IArbitrumSequencerEngine
{
    private const int MaxAuctionResolutionRetries = 3;

    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumSequencerEngine>();
    private readonly NonceCache _nonceCache = new(arbitrumConfig.SequencerNonceCacheSize);
    private readonly NonceFailureCache _nonceFailureCache = new(
        arbitrumConfig.SequencerNonceCacheSize,
        onEvict: (item, errorMessage) => OnNonceFailureEvict(sequencerState, item, errorMessage));

    // Pooled collections reused across PrecheckNonces calls to avoid per-block hash table/queue allocations.
    private readonly Dictionary<Address, ulong> _pendingNonces = new();
    private readonly Queue<TxQueueItem> _extraItems = new();

    private Block? _pendingBlock;
    private SequencedBlockInfo? _lastSequencedBlockInfo;
    private SequencedBlockInfo? _lastCreatedBlockWithRegularTxsInfo;
    private List<TxQueueItem>? _lastRegularTxQueueItems;

    public async Task<ResultWrapper<StartSequencingResult>> StartSequencingAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp)
    {
        SequencerStateSnapshot state = sequencerState.Current;
        if (state.Mode != SequencerMode.Active)
        {
            if (state is { Mode: SequencerMode.Forwarding, Forwarder: not null })
            {
                List<TxQueueItem> pendingItems = transactionQueue.DrainBatch();
                if (pendingItems.Count > 0)
                    await HandleInactiveAsync(pendingItems, state.Forwarder);
            }

            return ResultWrapper<StartSequencingResult>.Success(new StartSequencingResult(null, arbitrumConfig.SequencerInactiveWaitMs));
        }

        ResultWrapper<SequencedMsg?> delayedResult = await SequenceDelayedMessageAsync();
        if (delayedResult.Result != Result.Success)
            return ResultWrapper<StartSequencingResult>.Fail(delayedResult.Result.Error!, delayedResult.ErrorCode);
        if (delayedResult.Data is not null)
            return ResultWrapper<StartSequencingResult>.Success(new StartSequencingResult(delayedResult.Data, 0));

        // Timeboost: give auction resolution transactions priority over all other work
        if (auctionResolutionQueue.TryRead(out TxQueueItem? auctionItem))
        {
            SequencedMsg? auctionResult = await CreateBlockWithSingleTxAsync(auctionItem, l1BlockNumber, timestamp);
            if (auctionResult is not null)
                return ResultWrapper<StartSequencingResult>.Success(new StartSequencingResult(auctionResult, 0));
        }

        SequencedMsg? result = await CreateBlockWithRegularTxsAsync(l1BlockNumber, l1Timestamp, timestamp);
        if (result is not null)
            return ResultWrapper<StartSequencingResult>.Success(new StartSequencingResult(result, 0));

        return ResultWrapper<StartSequencingResult>.Success(new StartSequencingResult(null, arbitrumConfig.SequencerMaxBlockSpeedMs));
    }

    public async Task<ResultWrapper<EmptyResponse>> EndSequencingAsync(string? error)
    {
        if (_lastCreatedBlockWithRegularTxsInfo is null)
        {
            _pendingBlock = null;
            return ResultWrapper.EmptySuccess;
        }

        List<TxQueueItem>? queueItems = _lastRegularTxQueueItems;
        Block block = _lastCreatedBlockWithRegularTxsInfo.Block;

        _lastCreatedBlockWithRegularTxsInfo = null;
        _lastRegularTxQueueItems = null;

        if (queueItems is null)
            return ResultWrapper.EmptySuccess;

        // Retry-sequencer error: forward to backup if available, else re-queue locally
        if (IsRetrySequencerError(error))
        {
            _pendingBlock = null;

            SequencerStateSnapshot state = sequencerState.Current;
            if (state.Forwarder is not null)
            {
                await HandleInactiveAsync(queueItems, state.Forwarder);
                return ResultWrapper.EmptySuccess;
            }

            foreach (TxQueueItem item in queueItems)
                transactionQueue.PushRetry(item);

            return ResultWrapper.EmptySuccess;
        }

        // Non-retry error: return error to callers (don't re-queue)
        if (error is not null)
        {
            _pendingBlock = null;

            foreach (TxQueueItem item in queueItems)
                item.ReturnResult(new Exception(error));
            return ResultWrapper.EmptySuccess;
        }

        _nonceCache.Finalize(block);

        // Arbitrum includes all sequenced txs in the block; execution failures are visible via receipt StatusCode
        foreach (TxQueueItem item in queueItems)
            item.ReturnResult(null);

        return ResultWrapper.EmptySuccess;
    }

    public async Task<ResultWrapper<EmptyResponse>> AppendLastSequencedBlockAsync()
    {
        if (_pendingBlock is not null)
        {
            ResultWrapper<Block> finalizeResult = await factory.FinalizeBlockAsync(_pendingBlock);
            _pendingBlock = null;

            if (finalizeResult.Result != Result.Success)
                return ResultWrapper<EmptyResponse>.Fail($"Failed to finalize block: {finalizeResult.Result.Error}", finalizeResult.ErrorCode);
        }

        if (_lastSequencedBlockInfo is not null)
        {
            cachedL1PriceData.CacheL1PriceDataOfMsg(
                _lastSequencedBlockInfo.MsgIdx,
                Array.Empty<TxReceipt>(),
                _lastSequencedBlockInfo.Block,
                blockBuiltUsingDelayedMessage: true);

            _lastSequencedBlockInfo = null;
        }

        return ResultWrapper.EmptySuccess;
    }

    public ResultWrapper<EmptyResponse> EnqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx)
    {
        delayedMessageQueue.Enqueue(messages, firstMsgIdx);

        if (_logger.IsDebug)
            _logger.Debug($"Enqueued {messages.Length} delayed messages starting at index {firstMsgIdx}");

        return ResultWrapper.EmptySuccess;
    }

    public ResultWrapper<ulong> NextDelayedMessageNumber()
    {
        if (delayedMessageQueue.TryPeekTail(out DelayedMessage? tail))
            return ResultWrapper<ulong>.Success(tail!.MessageIndex + 1);

        return ResultWrapper<ulong>.Success(blockTree.Head!.Header.Nonce);
    }

    public async Task<ResultWrapper<SequencedMsg?>> ResequenceReorgedMessageAsync(MessageWithMetadata? msg)
    {
        if (msg?.Message.Header is null)
            return ResultWrapper<SequencedMsg?>.Success(null);

        BlockHeader currentHeader = blockTree.Head!.Header;

        if (msg.Message.Header.RequestId is not null)
        {
            ulong delayedMsgIdx = BinaryPrimitives.ReadUInt64BigEndian(msg.Message.Header.RequestId.Bytes.Slice(24));

            if (delayedMsgIdx != currentHeader.Nonce)
            {
                if (_logger.IsInfo)
                    _logger.Info($"Not resequencing delayed message due to unexpected index, expected {currentHeader.Nonce} found {delayedMsgIdx}");

                return ResultWrapper<SequencedMsg?>.Success(null);
            }

            ResultWrapper<SequencedMsg> resequencedDelayedMessage = await SequenceDelayedMessageWithBlockMutexAsync(msg.Message, delayedMsgIdx);
            if (resequencedDelayedMessage.Result != Result.Success)
                return ResultWrapper<SequencedMsg?>.Fail(resequencedDelayedMessage.Result.Error!, resequencedDelayedMessage.ErrorCode);

            return ResultWrapper<SequencedMsg?>.Success(resequencedDelayedMessage.Data);
        }

        ResultWrapper<SequencedMsg> resequencedMessage = await ResequenceRegularMessageWithBlockMutexAsync(msg);
        if (resequencedMessage.Result != Result.Success)
            return ResultWrapper<SequencedMsg?>.Fail(resequencedMessage.Result.Error!, resequencedMessage.ErrorCode);

        return ResultWrapper<SequencedMsg?>.Success(resequencedMessage.Data);
    }

    public ResultWrapper<EmptyResponse> Pause()
    {
        sequencerState.Pause();

        if (_logger.IsInfo)
            _logger.Info("Sequencer paused");

        return ResultWrapper.EmptySuccess;
    }

    public ResultWrapper<EmptyResponse> Activate()
    {
        sequencerState.Activate();

        if (_logger.IsInfo)
            _logger.Info("Sequencer activated");

        return ResultWrapper.EmptySuccess;
    }

    public ResultWrapper<EmptyResponse> ForwardTo(string url)
    {
        sequencerState.ForwardTo(url);

        if (_logger.IsInfo)
            _logger.Info($"Sequencer forwarding to {url}");

        return ResultWrapper.EmptySuccess;
    }

    private static void OnNonceFailureEvict(SequencerState sequencerState, TxQueueItem item, string errorMessage)
    {
        TransactionForwarder? forwarder = sequencerState.Forwarder;
        if (forwarder is null)
        {
            item.ReturnResult(new InvalidOperationException(errorMessage));
            return;
        }

        _ = Task.Run(async () =>
        {
            ResultWrapper<Hash256> result = await forwarder.ForwardTransactionAsync(
                item.RlpEncoded, item.Options, item.Tx.Hash!, item.CancellationToken);
            item.ReturnResult(result.Result != Result.Success
                ? new Exception(result.Result.Error!)
                : null);
        });
    }

    private static bool IsRetrySequencerError(string? error)
        => error is not null && error.Contains("retry sequencer", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Handles the inactive (forwarding) state by forwarding queued transactions to the backup sequencer.
    /// </summary>
    private async Task HandleInactiveAsync(List<TxQueueItem> queueItems, TransactionForwarder forwarder)
    {
        Task<(TxQueueItem Item, ResultWrapper<Hash256> Result)>[] forwardTasks = new Task<(TxQueueItem, ResultWrapper<Hash256>)>[queueItems.Count];

        for (int i = 0; i < queueItems.Count; i++)
        {
            TxQueueItem item = queueItems[i];
            forwardTasks[i] = ForwardSingleAsync(forwarder, item);
        }

        (TxQueueItem Item, ResultWrapper<Hash256> Result)[] results = await Task.WhenAll(forwardTasks);

        foreach ((TxQueueItem item, ResultWrapper<Hash256> result) in results)
            if (result.ErrorCode == ArbitrumSequencerErrors.NoSequencer)
                transactionQueue.PushRetry(item);
            else if (result.Result != Result.Success)
                item.ReturnResult(new Exception(result.Result.Error!));
            else
                item.ReturnResult(null);

        _nonceFailureCache.Clear();
    }

    private static async Task<(TxQueueItem Item, ResultWrapper<Hash256> Result)> ForwardSingleAsync(
        TransactionForwarder forwarder, TxQueueItem item)
    {
        ResultWrapper<Hash256> result = await forwarder.ForwardTransactionAsync(
            item.RlpEncoded, item.Options, item.Tx.Hash!, item.CancellationToken);
        return (item, result);
    }

    private async Task<SequencedMsg?> CreateBlockWithSingleTxAsync(TxQueueItem item, ulong l1BlockNumber, ulong timestamp)
    {
        ResultWrapper<SequencedMsg> sequencedMessage = await CreateBlockWithRegularTxsWithMutexAsync([item], l1BlockNumber, timestamp);
        if (sequencedMessage.Result == Result.Success)
            return sequencedMessage.Data;

        // Re-queue on any transient error if retries remain
        if (item.RetryCount < MaxAuctionResolutionRetries)
        {
            item.RetryCount++;
            await auctionResolutionQueue.WriteAsync(item);

            if (_logger.IsDebug)
                _logger.Debug($"Re-queued auction resolution tx (retry {item.RetryCount}/{MaxAuctionResolutionRetries}): {sequencedMessage.Result.Error}");

            return null;
        }

        item.ReturnResult(new Exception(sequencedMessage.Result.Error!));

        if (_logger.IsError)
            _logger.Error($"Failed to create block with auction resolution tx after {MaxAuctionResolutionRetries} retries: {sequencedMessage.Result.Error}");

        return null;
    }

    private async Task<SequencedMsg?> CreateBlockWithRegularTxsAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp)
    {
        List<TxQueueItem> queueItems = transactionQueue.DrainBatch();

        if (queueItems.Count == 0)
            return null;

        BlockHeader currentHead = blockTree.Head!.Header;
        ulong currentBlock = (ulong)currentHead.Number;

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

        // GasFeeCap validation: reject transactions whose MaxFeePerGas is below the current BaseFee
        UInt256 baseFee = currentHead.BaseFeePerGas;
        int feeWriteIdx = 0;
        for (int i = 0; i < queueItems.Count; i++)
        {
            if (queueItems[i].Tx.MaxFeePerGas < baseFee)
                queueItems[i].ReturnResult(new InvalidOperationException(
                    $"maxFeePerGas {queueItems[i].Tx.MaxFeePerGas} less than block baseFee {baseFee}"));
            else
                queueItems[feeWriteIdx++] = queueItems[i];
        }
        queueItems.RemoveRange(feeWriteIdx, queueItems.Count - feeWriteIdx);

        if (queueItems.Count == 0)
            return null;

        // Conditional options validation: evict txs whose state conditions are no longer met
        int optionsWriteIdx = 0;
        for (int i = 0; i < queueItems.Count; i++)
        {
            TxQueueItem it = queueItems[i];
            if (it.Options is not null)
            {
                Result checkResult = it.Options.Check(l1BlockNumber, timestamp, stateReader, currentHead);
                if (checkResult != Result.Success)
                {
                    it.ReturnResult(new InvalidOperationException(checkResult.Error));
                    continue;
                }
            }
            queueItems[optionsWriteIdx++] = it;
        }
        queueItems.RemoveRange(optionsWriteIdx, queueItems.Count - optionsWriteIdx);

        if (queueItems.Count == 0)
            return null;

        ulong timestampDelta = l1Timestamp > timestamp ? l1Timestamp - timestamp : timestamp - l1Timestamp;
        if (l1BlockNumber == 0 || timestampDelta > (ulong)arbitrumConfig.SequencerMaxAcceptableTimestampDelta)
        {
            foreach (TxQueueItem item in queueItems)
                transactionQueue.PushRetry(item);

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

        ResultWrapper<SequencedMsg> sequencedMessage = await CreateBlockWithRegularTxsWithMutexAsync(queueItems, l1BlockNumber, timestamp);
        if (sequencedMessage.Result == Result.Success)
            return sequencedMessage.Data;

        if (sequencedMessage.ErrorCode == ArbitrumBlockFactoryErrors.CreateBlockMutexHeld)
        {
            foreach (TxQueueItem item in queueItems)
                transactionQueue.PushRetry(item);

            if (_logger.IsDebug)
                _logger.Debug("Could not acquire block creation semaphore for user transaction sequencing");

            return null;
        }

        foreach (TxQueueItem item in queueItems)
            transactionQueue.PushRetry(item);

        if (_logger.IsError)
            _logger.Error($"Failed to create block with regular transactions: {sequencedMessage.Result.Error}");

        return null;
    }

    private async Task<ResultWrapper<SequencedMsg>> CreateBlockWithRegularTxsWithMutexAsync(List<TxQueueItem> queueItems, ulong l1BlockNumber, ulong timestamp)
    {
        byte[][] rlpEncodedTxs = new byte[queueItems.Count][];
        HashSet<Hash256>? timeboostedTxHashes = arbitrumConfig.TimeboostEnabled ? new() : null;

        for (int i = 0; i < queueItems.Count; i++)
        {
            rlpEncodedTxs[i] = queueItems[i].RlpEncoded;

            if (timeboostedTxHashes is not null && queueItems[i].IsTimeboosted && queueItems[i].Tx.Hash is not null)
                timeboostedTxHashes.Add(queueItems[i].Tx.Hash!);
        }

        BlockHeader? currentHead = blockTree.Head?.Header;
        if (currentHead is null)
            return ResultWrapper<SequencedMsg>.Fail("Unable to build block as block tree head is null.", ErrorCodes.InternalError);

        long blockNumber = currentHead.Number + 1;
        MessageWithMetadata messageWithMetadata =
            L2MessageAssembler.AssembleFromSignedTransactions(rlpEncodedTxs, l1BlockNumber, timestamp, currentHead.Nonce);

        ResultWrapper<Block> blockResult = await factory.DigestMessageAsync(blockNumber, messageWithMetadata, deferSuggestion: true);
        if (blockResult.Result != Result.Success)
            return ResultWrapper<SequencedMsg>.Fail($"Failed to build block for message: {blockResult.Result.Error}", blockResult.ErrorCode);

        ulong msgIdx = MessageBlockConverter.BlockNumberToMessageIndex((ulong)blockNumber, specHelper);
        _pendingBlock = blockResult.Data;
        _lastCreatedBlockWithRegularTxsInfo = new SequencedBlockInfo(blockResult.Data, msgIdx);
        _lastRegularTxQueueItems = queueItems;

        if (_logger.IsDebug)
            _logger.Debug($"Created block {blockResult.Data.Hash} with {queueItems.Count} user txs, msgIdx={msgIdx}, blockNumber={blockResult.Data.Number}");

        SequencedMsg sequencedMessage = BuildSequencedMsg(blockResult.Data, msgIdx, messageWithMetadata, timeboostedTxHashes);

        return ResultWrapper<SequencedMsg>.Success(sequencedMessage);
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

    private async Task<ResultWrapper<SequencedMsg>> ResequenceRegularMessageWithBlockMutexAsync(MessageWithMetadata msg)
    {
        BlockHeader? currentHead = blockTree.Head?.Header;
        if (currentHead is null)
            return ResultWrapper<SequencedMsg>.Fail("Unable to build block as block tree head is null.", ErrorCodes.InternalError);

        long blockNumber = currentHead.Number + 1;
        ulong msgIdx = MessageBlockConverter.BlockNumberToMessageIndex((ulong)blockNumber, specHelper);

        ResultWrapper<Block> blockResult = await factory.DigestMessageAsync(blockNumber, msg, deferSuggestion: true);
        if (blockResult.Result != Result.Success)
            return ResultWrapper<SequencedMsg>.Fail($"Failed to build block for delayed message: {blockResult.Result.Error}", blockResult.ErrorCode);

        _pendingBlock = blockResult.Data;

        if (_logger.IsInfo)
            _logger.Info($"Resequenced regular message, msgIdx={msgIdx}, blockNumber={blockResult.Data.Number}");

        SequencedMsg sequencedMessage = BuildSequencedMsg(blockResult.Data, msgIdx, msg, null);

        return ResultWrapper<SequencedMsg>.Success(sequencedMessage);
    }

    private async Task<ResultWrapper<SequencedMsg?>> SequenceDelayedMessageAsync()
    {
        if (!delayedMessageQueue.TryDequeue(out DelayedMessage? delayedMessage))
            return ResultWrapper<SequencedMsg?>.Success(null);

        ResultWrapper<SequencedMsg> sequencedMessage = await SequenceDelayedMessageWithBlockMutexAsync(delayedMessage!.Message, delayedMessage.MessageIndex);
        if (sequencedMessage.Result != Result.Success)
        {
            delayedMessageQueue.Clear();
            return ResultWrapper<SequencedMsg?>.Fail($"Error sequencing delayed message at index {delayedMessage.MessageIndex}: {sequencedMessage.Result.Error}");
        }

        return ResultWrapper<SequencedMsg?>.Success(sequencedMessage.Data);
    }

    private async Task<ResultWrapper<SequencedMsg>> SequenceDelayedMessageWithBlockMutexAsync(L1IncomingMessage message, ulong delayedMsgIdx)
    {
        BlockHeader? currentHead = blockTree.Head?.Header;
        if (currentHead is null)
            return ResultWrapper<SequencedMsg>.Fail("Unable to build block as block tree head is null.", ErrorCodes.InternalError);

        ulong expectedDelayedMsgIdx = currentHead.Nonce;
        if (expectedDelayedMsgIdx != delayedMsgIdx)
            return ResultWrapper<SequencedMsg>.Fail($"Wrong delayed message sequenced got {delayedMsgIdx} expected {expectedDelayedMsgIdx}");

        long blockNumber = currentHead.Number + 1;
        MessageWithMetadata messageWithMetadata = new(message, delayedMsgIdx + 1);

        ResultWrapper<Block> blockResult = await factory.DigestMessageAsync(blockNumber, messageWithMetadata, deferSuggestion: true);
        if (blockResult.Result != Result.Success)
            return ResultWrapper<SequencedMsg>.Fail($"Failed to build block for delayed message: {blockResult.Result.Error}", blockResult.ErrorCode);

        ulong msgIdx = MessageBlockConverter.BlockNumberToMessageIndex((ulong)blockNumber, specHelper);
        _pendingBlock = blockResult.Data;
        _lastSequencedBlockInfo = new SequencedBlockInfo(blockResult.Data, msgIdx);

        if (_logger.IsDebug)
            _logger.Debug($"Added DelayedMessage, msgIdx={msgIdx}, delayedMsgIdx={delayedMsgIdx}, blockNumber={blockResult.Data.Number}");

        SequencedMsg sequencedDelayedMessage = BuildSequencedMsg(blockResult.Data, msgIdx, messageWithMetadata, null);

        return ResultWrapper<SequencedMsg>.Success(sequencedDelayedMessage);
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
