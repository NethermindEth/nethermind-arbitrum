// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Modules;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Core execution engine containing all Arbitrum block production and state management logic.
/// </summary>
public sealed class ArbitrumExecutionEngine(
    ArbitrumBlockTreeInitializer initializer,
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager,
    CachedL1PriceData cachedL1PriceData,
    IBlockProcessingQueue processingQueue,
    IArbitrumConfig arbitrumConfig,
    IBlocksConfig blocksConfig)
    : IArbitrumExecutionEngine
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumExecutionEngine>();

    public IBlockTree BlockTree { get; } = blockTree;
    public bool BuildBlocksOnMainState => blocksConfig.BuildBlocksOnMainState;

    private readonly SemaphoreSlim _createBlocksSemaphore = new(1, 1);
    private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, arbitrumConfig, logManager);
    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<Block>> _newBestSuggestedBlockEvents = new();
    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<BlockRemovedEventArgs>> _blockRemovedEvents = new();

    public Task<bool> TryAcquireSemaphoreAsync(int millisecondsTimeout = 0)
        => _createBlocksSemaphore.WaitAsync(millisecondsTimeout);

    public void ReleaseSemaphore()
        => _createBlocksSemaphore.Release();

    public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
    {
        BlockHeader? existingGenesis = BlockTree.Genesis;
        if (existingGenesis is not null)
        {
            if (_logger.IsDebug)
                _logger.Debug("Genesis already initialized, skipping DigestInitMessage");
            return ResultWrapper<MessageResult>.Success(new()
            {
                BlockHash = existingGenesis.Hash ?? throw new InvalidOperationException("Genesis hash is null"),
                SendRoot = Hash256.Zero
            });
        }

        if (message.InitialL1BaseFee.IsZero)
            return ResultWrapper<MessageResult>.Fail("InitialL1BaseFee must be greater than zero", ErrorCodes.InvalidParams);

        if (message.SerializedChainConfig is null || message.SerializedChainConfig.Length == 0)
            return ResultWrapper<MessageResult>.Fail("SerializedChainConfig must not be empty.", ErrorCodes.InvalidParams);

        if (!TryDeserializeChainConfig(message.SerializedChainConfig, out ChainConfig? chainConfig))
            return ResultWrapper<MessageResult>.Fail("Failed to deserialize ChainConfig.", ErrorCodes.InvalidParams);

        ParsedInitMessage initMessage = new(chainSpec.ChainId, message.InitialL1BaseFee, chainConfig, message.SerializedChainConfig);
        BlockHeader genesisHeader = initializer.Initialize(initMessage);

        return ResultWrapper<MessageResult>.Success(new()
        {
            BlockHash = genesisHeader.Hash ?? throw new InvalidOperationException("Genesis block hash must not be null"),
            SendRoot = Hash256.Zero
        });
    }

    public async Task<ResultWrapper<MessageResult>> DigestMessageAsync(DigestMessageParameters parameters)
    {
        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtMessageIndexAsync(parameters.Index);
        if (resultAtMessageIndex.Result == Result.Success)
            return resultAtMessageIndex;

        // Non-blocking attempt to acquire the semaphore.
        if (!await _createBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);

        try
        {
            long blockNumber = MessageIndexToBlockNumber(parameters.Index).Data;
            BlockHeader? headBlockHeader = BlockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
                return ResultWrapper<MessageResult>.Fail(
                    $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");

            if (blocksConfig.BuildBlocksOnMainState)
                return await ProduceBlockWithoutWaitingOnProcessingQueueAsync(parameters.Message, blockNumber, headBlockHeader);

            return await ProduceBlockWhileLockedAsync(parameters.Message, blockNumber, headBlockHeader);
        }
        finally
        {
            _createBlocksSemaphore.Release();
        }
    }

    public async Task<ResultWrapper<MessageResult[]>> ReorgAsync(ReorgParameters parameters)
    {
        // 1. Validate: Cannot reorg to genesis
        if (parameters.MsgIdxOfFirstMsgToAdd == 0)
            return ResultWrapper<MessageResult[]>.Fail("Cannot reorg to genesis", ErrorCodes.InternalError);

        // 2. Acquire semaphore (non-blocking, consistent with DigestMessage)
        if (!await _createBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult[]>.Fail("CreateBlock mutex held", ErrorCodes.InternalError);

        try
        {
            // 3. Convert message index to block number
            ResultWrapper<long> blockNumResult = MessageIndexToBlockNumber(parameters.MsgIdxOfFirstMsgToAdd - 1);
            if (blockNumResult.Result != Result.Success)
                return ResultWrapper<MessageResult[]>.Fail(blockNumResult.Result.Error ?? "Unknown error converting message index", blockNumResult.ErrorCode);

            long lastBlockNumToKeep = blockNumResult.Data;

            // 4. Validate target block exists
            BlockHeader? currentHead = BlockTree.Head?.Header;
            if (currentHead is null || lastBlockNumToKeep > currentHead.Number)
                return ResultWrapper<MessageResult[]>.Fail("Reorg target block not found", ErrorCodes.InternalError);

            // 5. Find the target block
            Block? blockToKeep = BlockTree.FindBlock(lastBlockNumToKeep, BlockTreeLookupOptions.RequireCanonical);
            if (blockToKeep is null)
                return ResultWrapper<MessageResult[]>.Fail("Reorg target block not found", ErrorCodes.InternalError);

            // 6. Clear safe/finalized blocks if below reorg target
            BlockHeader? safeBlock = BlockTree.FindSafeHeader();
            BlockHeader? finalBlock = BlockTree.FindFinalizedHeader();
            Hash256? newSafeHash = safeBlock is not null && safeBlock.Number > blockToKeep.Number ? null : BlockTree.SafeHash;
            Hash256? newFinalHash = finalBlock is not null && finalBlock.Number > blockToKeep.Number ? null : BlockTree.FinalizedHash;

            if (safeBlock is not null && safeBlock.Number > blockToKeep.Number && _logger.IsInfo)
                _logger.Info($"Reorg target block is below safe block. lastBlockNumToKeep:{blockToKeep.Number} currentSafeBlock:{safeBlock.Number}");

            if (finalBlock is not null && finalBlock.Number > blockToKeep.Number && _logger.IsInfo)
                _logger.Info($"Reorg target block is below finalized block. lastBlockNumToKeep:{blockToKeep.Number} currentFinalBlock:{finalBlock.Number}");

            // 7. Update fork choice with potentially cleared safe/finalized
            BlockTree.ForkChoiceUpdated(newFinalHash, newSafeHash);

            // 8. Reorg blockchain to target block
            BlockTree.UpdateMainChain([blockToKeep], wereProcessed: true, forceHeadBlock: true);

            // 9. Process new messages using simpler block production (no event waiting after reorg)
            MessageResult[] messageResults = new MessageResult[parameters.NewMessages.Length];
            for (int i = 0; i < parameters.NewMessages.Length; i++)
            {
                MessageWithMetadataAndBlockInfo message = parameters.NewMessages[i];
                BlockHeader headBlockHeader = BlockTree.Head!.Header;

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
                _logger.Error($"Error processing Reorg for message index {parameters.MsgIdxOfFirstMsgToAdd}: {ex.Message}", ex);
            return ResultWrapper<MessageResult[]>.Fail(ArbitrumRpcErrors.InternalError, ErrorCodes.InternalError);
        }
        finally
        {
            _createBlocksSemaphore.Release();
        }
    }

    public Task<ResultWrapper<MessageResult>> ResultAtMessageIndexAsync(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return Task.FromResult(ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Unknown error converting message index"));

            BlockHeader? blockHeader = BlockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.RequireCanonical);
            if (blockHeader == null)
                return Task.FromResult(ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound(blockNumberResult.Data)));

            if (_logger.IsTrace)
                _logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

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
                _logger.Error($"Error processing ResultAtMessageIndex for message index {messageIndex}: {ex.Message}", ex);
            return Task.FromResult(ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError));
        }
    }

    public Task<ResultWrapper<ulong>> HeadMessageIndexAsync()
    {
        BlockHeader? header = BlockTree.FindLatestHeader();

        return header is null
            ? Task.FromResult(ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError))
            : Task.FromResult(BlockNumberToMessageIndex((ulong)header.Number));
    }

    public ResultWrapper<long> MessageIndexToBlockNumber(ulong messageIndex)
    {
        try
        {
            long blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(messageIndex, specHelper);
            return ResultWrapper<long>.Success(blockNumber);
        }
        catch (OverflowException)
        {
            return ResultWrapper<long>.Fail(ArbitrumRpcErrors.Overflow);
        }
    }

    public ResultWrapper<ulong> BlockNumberToMessageIndex(ulong blockNumber)
    {
        try
        {
            ulong messageIndex = MessageBlockConverter.BlockNumberToMessageIndex(blockNumber, specHelper);
            return ResultWrapper<ulong>.Success(messageIndex);
        }
        catch (ArgumentOutOfRangeException)
        {
            ulong genesis = specHelper.GenesisBlockNum;
            return ResultWrapper<ulong>.Fail(
                $"blockNumber {blockNumber} < genesis {genesis}");
        }
    }

    public ResultWrapper<string> SetFinalityData(SetFinalityDataParams parameters)
    {
        try
        {
            if (_logger.IsDebug)
                _logger.Debug($"SetFinalityData called: safe={parameters.SafeFinalityData?.MsgIdx}, " +
                              $"finalized={parameters.FinalizedFinalityData?.MsgIdx}, " +
                              $"validated={parameters.ValidatedFinalityData?.MsgIdx}");

            // Convert RPC parameters to internal types
            ArbitrumFinalityData? safeFinalityData = parameters.SafeFinalityData?.ToArbitrumFinalityData();
            ArbitrumFinalityData? finalizedFinalityData = parameters.FinalizedFinalityData?.ToArbitrumFinalityData();
            ArbitrumFinalityData? validatedFinalityData = parameters.ValidatedFinalityData?.ToArbitrumFinalityData();

            // Set finality data
            _syncMonitor.SetFinalityData(safeFinalityData, finalizedFinalityData, validatedFinalityData);

            if (_logger.IsDebug)
                _logger.Debug("SetFinalityData completed successfully");

            return ResultWrapper<string>.Success("OK");
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"SetFinalityData failed: {ex.Message}", ex);

            return ResultWrapper<string>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<string> MarkFeedStart(ulong to)
    {
        try
        {
            cachedL1PriceData.MarkFeedStart(to);
            return ResultWrapper<string>.Success("OK");
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"MarkFeedStart failed: {ex.Message}", ex);

            return ResultWrapper<string>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<string> SetConsensusSyncData(SetConsensusSyncDataParams? parameters)
    {
        if (parameters is null)
            return ResultWrapper<string>.Fail("Parameters cannot be null", ErrorCodes.InvalidParams);

        try
        {
            _syncMonitor.SetConsensusSyncData(
                parameters.Synced,
                parameters.MaxMessageCount,
                parameters.SyncProgressMap,
                parameters.UpdatedAt);

            return ResultWrapper<string>.Success("OK");
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"SetConsensusSyncData failed: {ex.Message}", ex);

            return ResultWrapper<string>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<bool> Synced()
    {
        try
        {
            return ResultWrapper<bool>.Success(_syncMonitor.IsSynced());
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Synced failed: {ex.Message}", ex);
            return ResultWrapper<bool>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<Dictionary<string, object>> FullSyncProgressMap()
    {
        try
        {
            Dictionary<string, object> progressMap = _syncMonitor.GetFullSyncProgressMap();
            return ResultWrapper<Dictionary<string, object>>.Success(progressMap);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"FullSyncProgressMap failed: {ex.Message}", ex);
            return ResultWrapper<Dictionary<string, object>>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public Task<ResultWrapper<ulong>> ArbOSVersionForMessageIndexAsync(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return Task.FromResult(ResultWrapper<ulong>.Fail(
                    blockNumberResult.Result.Error ?? "Failed to convert message index to block number"));

            BlockHeader? blockHeader = BlockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.RequireCanonical);
            if (blockHeader == null)
                return Task.FromResult(ResultWrapper<ulong>.Fail(ArbitrumRpcErrors.BlockNotFound(blockNumberResult.Data)));

            if (_logger.IsTrace)
                _logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, _logger);

            return Task.FromResult(ResultWrapper<ulong>.Success(headerInfo.ArbOSFormatVersion));
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error processing ArbOSVersionForMessageIndex for message index {messageIndex}: {ex.Message}", ex);
            return Task.FromResult(ResultWrapper<ulong>.Fail(ArbitrumRpcErrors.InternalError));
        }
    }

    /// <summary>
    /// Produces a block while waiting for processing queue events.
    /// Used internally and by ArbitrumExecutionEngineWithComparison.
    /// </summary>
    public async Task<ResultWrapper<MessageResult>> ProduceBlockWhileLockedAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
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

        BlockTree.NewBestSuggestedBlock += OnNewBestSuggestedBlock;
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
            BlockTree.NewBestSuggestedBlock -= OnNewBestSuggestedBlock;
            processingQueue.BlockRemoved -= OnBlockRemoved;

            _newBestSuggestedBlockEvents.Clear();
            _blockRemovedEvents.Clear();
        }
    }

    public async Task<ResultWrapper<MessageResult>> ProduceBlockWithoutWaitingOnProcessingQueueAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
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

    private Hash256 GetSendRootFromBlock(Block block)
    {
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, _logger);

        // ArbitrumBlockHeaderInfo.Deserialize returns Empty if deserialization fails
        if (headerInfo == ArbitrumBlockHeaderInfo.Empty && _logger.IsWarn)
            _logger.Warn($"Block header info deserialization returned empty result for block {block.Hash}");

        return headerInfo.SendRoot;
    }

    private bool TryDeserializeChainConfig(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out ChainConfig? chainConfig)
    {
        try
        {
            chainConfig = JsonSerializer.Deserialize<ChainConfig>(bytes);
            return chainConfig != null;
        }
        catch (Exception exception)
        {
            _logger.Error("Failed to deserialize ChainConfig from bytes.", exception);
            chainConfig = null;
            return false;
        }
    }
}
