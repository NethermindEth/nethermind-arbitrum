// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Concurrent;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Modules;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution;

public class ArbitrumBlockFactory(
    IBlockTree blockTree,
    IBlockProcessingQueue processingQueue,
    IManualBlockProductionTrigger trigger,
    IBlocksConfig blocksConfig,
    IArbitrumConfig arbitrumConfig,
    ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumExecutionEngine>();
    private readonly SemaphoreSlim _createBlocksSemaphore = new(1, 1);
    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<Block>> _newBestSuggestedBlockEvents = new();
    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<BlockRemovedEventArgs>> _blockRemovedEvents = new();

    public async Task<ResultWrapper<MessageResult>> DigestMessageAsync(long blockNumber, MessageWithMetadata message)
    {
        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtBlockNumberAsync(blockNumber);
        if (resultAtMessageIndex.Result == Result.Success)
            return resultAtMessageIndex;

        // Non-blocking attempt to acquire the semaphore.
        if (!await _createBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);

        try
        {
            BlockHeader? headBlockHeader = blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
                return ResultWrapper<MessageResult>.Fail(
                    $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");

            if (blocksConfig.BuildBlocksOnMainState)
                return await ProduceBlockWithoutWaitingOnProcessingQueueAsync(message, blockNumber, headBlockHeader);

            return await ProduceBlockWhileLockedAsync(message, blockNumber, headBlockHeader);
        }
        finally
        {
            _createBlocksSemaphore.Release();
        }
    }

    public async Task<ResultWrapper<MessageResult[]>> ReorgAsync(long blockNumber, MessageWithMetadataAndBlockInfo[] newMessages)
    {
        if (!await _createBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult[]>.Fail("CreateBlock mutex held", ErrorCodes.InternalError);

        try
        {
            long lastBlockNumToKeep = blockNumber;

            // 4. Validate target block exists
            BlockHeader? currentHead = blockTree.Head?.Header;
            if (currentHead is null || lastBlockNumToKeep > currentHead.Number)
                return ResultWrapper<MessageResult[]>.Fail("Reorg target block not found", ErrorCodes.InternalError);

            // 5. Find the target block
            Block? blockToKeep = blockTree.FindBlock(lastBlockNumToKeep, BlockTreeLookupOptions.RequireCanonical);
            if (blockToKeep is null)
                return ResultWrapper<MessageResult[]>.Fail("Reorg target block not found", ErrorCodes.InternalError);

            // 6. Clear safe/finalized blocks if below reorg target
            BlockHeader? safeBlock = blockTree.FindSafeHeader();
            BlockHeader? finalBlock = blockTree.FindFinalizedHeader();
            Hash256? newSafeHash = safeBlock is not null && safeBlock.Number > blockToKeep.Number ? null : blockTree.SafeHash;
            Hash256? newFinalHash = finalBlock is not null && finalBlock.Number > blockToKeep.Number ? null : blockTree.FinalizedHash;

            if (safeBlock is not null && safeBlock.Number > blockToKeep.Number && _logger.IsInfo)
                _logger.Info($"Reorg target block is below safe block. lastBlockNumToKeep:{blockToKeep.Number} currentSafeBlock:{safeBlock.Number}");

            if (finalBlock is not null && finalBlock.Number > blockToKeep.Number && _logger.IsInfo)
                _logger.Info($"Reorg target block is below finalized block. lastBlockNumToKeep:{blockToKeep.Number} currentFinalBlock:{finalBlock.Number}");

            // 7. Update fork choice with potentially cleared safe/finalized
            blockTree.ForkChoiceUpdated(newFinalHash, newSafeHash);

            // 8. Reorg blockchain to target block
            blockTree.UpdateMainChain([blockToKeep], wereProcessed: true, forceHeadBlock: true);

            // 9. Process new messages using simpler block production (no event waiting after reorg)
            MessageResult[] messageResults = new MessageResult[newMessages.Length];
            for (int i = 0; i < newMessages.Length; i++)
            {
                MessageWithMetadataAndBlockInfo message = newMessages[i];
                BlockHeader headBlockHeader = blockTree.Head!.Header;

                ResultWrapper<MessageResult> blockResult = await ProduceBlockWithoutWaitingOnProcessingQueueAsync(
                    message.MessageWithMeta,
                    headBlockHeader.Number + 1,
                    headBlockHeader);

                if (blockResult.Result != Result.Success)
                    return ResultWrapper<MessageResult[]>.Fail(blockResult.Result.Error ?? "Unknown error producing block", blockResult.ErrorCode);

                messageResults[i] = blockResult.Data;
            }

            // 10. Return results
            return ResultWrapper<MessageResult[]>.Success(messageResults);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error processing Reorg for block number {blockNumber}: {ex.Message}", ex);
            return ResultWrapper<MessageResult[]>.Fail(ArbitrumRpcErrors.InternalError, ErrorCodes.InternalError);
        }
        finally
        {
            _createBlocksSemaphore.Release();
        }
    }

    private async Task<ResultWrapper<MessageResult>> ProduceBlockWithoutWaitingOnProcessingQueueAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
    {
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber,
            PreviousArbosVersion = headBlockHeader != null ? ArbitrumBlockHeaderInfo.Deserialize(headBlockHeader, _logger).ArbOSFormatVersion : 0
        };

        try
        {
            Block? block = await trigger.BuildBlock(parentHeader: headBlockHeader, payloadAttributes: payload);
            if (block?.Hash is null)
                return ResultWrapper<MessageResult>.Fail("Failed to build block or block has no hash.", ErrorCodes.InternalError);

            return ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = block.Hash!,
                SendRoot = GetSendRootFromBlock(block)
            });
        }
        catch (TimeoutException)
        {
            return ResultWrapper<MessageResult>.Fail("Timeout waiting for block processing result.", ErrorCodes.Timeout);
        }
    }

    private async Task<ResultWrapper<MessageResult>> ProduceBlockWhileLockedAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
    {
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber,
            PreviousArbosVersion = headBlockHeader != null ? ArbitrumBlockHeaderInfo.Deserialize(headBlockHeader, _logger).ArbOSFormatVersion : 0
        };

        void OnNewBestSuggestedBlock(object? sender, BlockEventArgs e)
        {
            if (e.Block.Hash is null)
                return;

            _newBestSuggestedBlockEvents
                .GetOrAdd(e.Block.Hash, _ => new TaskCompletionSource<Block>())
                .TrySetResult(e.Block);
        }

        void OnBlockRemoved(object? sender, BlockRemovedEventArgs e)
        {
            _blockRemovedEvents
                .GetOrAdd(e.BlockHash, _ => new TaskCompletionSource<BlockRemovedEventArgs>())
                .TrySetResult(e);
        }

        blockTree.NewBestSuggestedBlock += OnNewBestSuggestedBlock;
        processingQueue.BlockRemoved += OnBlockRemoved;

        try
        {
            Block? block = await trigger.BuildBlock(parentHeader: headBlockHeader, payloadAttributes: payload);
            if (block?.Hash is null)
                return ResultWrapper<MessageResult>.Fail("Failed to build block or block has no hash.", ErrorCodes.InternalError);

            TaskCompletionSource<Block> newBestBlockTcs = _newBestSuggestedBlockEvents.GetOrAdd(block.Hash, _ => new TaskCompletionSource<Block>());
            TaskCompletionSource<BlockRemovedEventArgs> blockRemovedTcs = _blockRemovedEvents.GetOrAdd(block.Hash, _ => new TaskCompletionSource<BlockRemovedEventArgs>());

            using CancellationTokenSource processingTimeoutTokenSource = arbitrumConfig.BuildProcessingTimeoutTokenSource();
            await Task.WhenAll(newBestBlockTcs.Task, blockRemovedTcs.Task)
                .WaitAsync(processingTimeoutTokenSource.Token);

            BlockRemovedEventArgs resultArgs = blockRemovedTcs.Task.Result;

            if (resultArgs.ProcessingResult != ProcessingResult.Exception)
                return resultArgs.ProcessingResult switch
                {
                    ProcessingResult.Success => ResultWrapper<MessageResult>.Success(new MessageResult
                    {
                        BlockHash = block.Hash!,
                        SendRoot = GetSendRootFromBlock(block)
                    }),
                    ProcessingResult.ProcessingError => ResultWrapper<MessageResult>.Fail(resultArgs.Message ?? "Block processing failed.",
                        ErrorCodes.InternalError),
                    _ => ResultWrapper<MessageResult>.Fail($"Block processing ended in an unhandled state: {resultArgs.ProcessingResult}",
                        ErrorCodes.InternalError)
                };
            BlockchainException exception = new(
                resultArgs.Exception?.Message ?? "Block processing threw an unspecified exception.",
                resultArgs.Exception);

            if (_logger.IsError)
                _logger.Error($"Block processing failed for {block.Hash}", exception);

            return ResultWrapper<MessageResult>.Fail(exception.Message, ErrorCodes.InternalError);

        }
        catch (TimeoutException)
        {
            return ResultWrapper<MessageResult>.Fail("Timeout waiting for block processing result.", ErrorCodes.Timeout);
        }
        finally
        {
            blockTree.NewBestSuggestedBlock -= OnNewBestSuggestedBlock;
            processingQueue.BlockRemoved -= OnBlockRemoved;

            _newBestSuggestedBlockEvents.Clear();
            _blockRemovedEvents.Clear();
        }
    }

    private Hash256 GetSendRootFromBlock(Block block)
    {
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, _logger);

        // ArbitrumBlockHeaderInfo.Deserialize returns Empty if deserialization fails
        if (headerInfo == ArbitrumBlockHeaderInfo.Empty && _logger.IsWarn)
            _logger.Warn($"Block header info deserialization returned empty result for block {block.Hash}");

        return headerInfo.SendRoot;
    }

    private Task<ResultWrapper<MessageResult>> ResultAtBlockNumberAsync(long blockNumber)
    {
        try
        {
            BlockHeader? blockHeader = blockTree.FindHeader(blockNumber, BlockTreeLookupOptions.RequireCanonical);
            if (blockHeader == null)
                return Task.FromResult(ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound(blockNumber)));

            if (_logger.IsTrace)
                _logger.Trace($"Found block header for block {blockNumber}: hash={blockHeader.Hash}");

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, _logger);
            return Task.FromResult(ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = blockHeader.Hash ?? Hash256.Zero,
                SendRoot = headerInfo.SendRoot,
            }));
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error processing ResultAtMessageIndex for message index {blockNumber}: {ex.Message}", ex);
            return Task.FromResult(ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError));
        }
    }
}
