// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Math;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumRpcModule(
    ArbitrumBlockTreeInitializer initializer,
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    ArbitrumRpcTxSource txSource,
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager,
    CachedL1PriceData cachedL1PriceData,
    IBlockProcessingQueue processingQueue,
    IArbitrumConfig arbitrumConfig,
    IBlocksConfig blocksConfig,
    IWorldStateManager worldStateManager,
    IProcessingTimeTracker processingTimeTracker) : IArbitrumRpcModule
{
    protected readonly SemaphoreSlim CreateBlocksSemaphore = new(1, 1);

    protected readonly ILogger Logger = logManager.GetClassLogger<ArbitrumRpcModule>();
    private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, arbitrumConfig, logManager);

    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<Block>> _newBestSuggestedBlockEvents = new();
    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<BlockRemovedEventArgs>> _blockRemovedEvents = new();

    private readonly Lock _maintenanceLock = new();
    private readonly TimeSpan _maintenanceThreshold = TimeSpan.FromMilliseconds(arbitrumConfig.TrieTimeLimitBeforeFlushMaintenanceMs);
    private volatile bool _runningMaintenance;

    public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
    {
        BlockHeader? existingGenesis = blockTree.Genesis;
        if (existingGenesis != null)
        {
            if (Logger.IsDebug)
                Logger.Debug($"Genesis already initialized, skipping DigestInitMessage");
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

    public virtual async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
    {
        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtMessageIndex(parameters.Index);
        if (resultAtMessageIndex.Result == Result.Success)
            return resultAtMessageIndex;

        // Non-blocking attempt to acquire the semaphore.
        if (!await CreateBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);

        try
        {
            _ = txSource; // TODO: replace with the actual use

            long blockNumber = (await MessageIndexToBlockNumber(parameters.Index)).Data;
            BlockHeader? headBlockHeader = blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
                return ResultWrapper<MessageResult>.Fail(
                    $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");

            if (blocksConfig.BuildBlocksOnMainState)
                return await ProduceBlockWithoutWaitingOnProcessingQueueAsync(parameters.Message, blockNumber, headBlockHeader);

            return await ProduceBlockWhileLockedAsync(parameters.Message, blockNumber, headBlockHeader);
        }
        finally
        {
            // Ensure the semaphore is released, equivalent to Go's `defer Unlock()`.
            CreateBlocksSemaphore.Release();
        }
    }

    public async Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters)
    {
        // 1. Validate: Cannot reorg to genesis
        if (parameters.MsgIdxOfFirstMsgToAdd == 0)
            return ResultWrapper<MessageResult[]>.Fail("Cannot reorg to genesis", ErrorCodes.InternalError);

        // 2. Acquire semaphore (non-blocking, consistent with DigestMessage)
        if (!await CreateBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult[]>.Fail("CreateBlock mutex held", ErrorCodes.InternalError);

        try
        {
            // 3. Convert message index to block number
            ResultWrapper<long> blockNumResult = await MessageIndexToBlockNumber(parameters.MsgIdxOfFirstMsgToAdd - 1);
            if (blockNumResult.Result != Result.Success)
                return ResultWrapper<MessageResult[]>.Fail(blockNumResult.Result.Error ?? "Unknown error converting message index", blockNumResult.ErrorCode);

            long lastBlockNumToKeep = blockNumResult.Data;

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

            if (safeBlock is not null && safeBlock.Number > blockToKeep.Number && Logger.IsInfo)
                Logger.Info($"Reorg target block is below safe block. lastBlockNumToKeep:{blockToKeep.Number} currentSafeBlock:{safeBlock.Number}");

            if (finalBlock is not null && finalBlock.Number > blockToKeep.Number && Logger.IsInfo)
                Logger.Info($"Reorg target block is below finalized block. lastBlockNumToKeep:{blockToKeep.Number} currentFinalBlock:{finalBlock.Number}");

            // 7. Update fork choice with potentially cleared safe/finalized
            blockTree.ForkChoiceUpdated(newFinalHash, newSafeHash);

            // 8. Reorg blockchain to target block
            blockTree.UpdateMainChain([blockToKeep], wereProcessed: true, forceHeadBlock: true);

            // 9. Process new messages using simpler block production (no event waiting after reorg)
            MessageResult[] messageResults = new MessageResult[parameters.NewMessages.Length];
            for (int i = 0; i < parameters.NewMessages.Length; i++)
            {
                MessageWithMetadataAndBlockInfo message = parameters.NewMessages[i];
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
            if (Logger.IsError)
                Logger.Error($"Error processing Reorg for message index {parameters.MsgIdxOfFirstMsgToAdd}: {ex.Message}", ex);
            return ResultWrapper<MessageResult[]>.Fail(ArbitrumRpcErrors.InternalError, ErrorCodes.InternalError);
        }
        finally
        {
            CreateBlocksSemaphore.Release();
        }
    }

    public async Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = await MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Unknown error converting message index");

            BlockHeader? blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.RequireCanonical);
            if (blockHeader == null)
                return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound(blockNumberResult.Data));

            if (Logger.IsTrace)
                Logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, Logger);
            return ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = blockHeader.Hash ?? Hash256.Zero,
                SendRoot = headerInfo.SendRoot,
            });
        }
        catch (Exception ex)
        {
            if (Logger.IsError)
                Logger.Error($"Error processing ResultAtMessageIndex for message index {messageIndex}: {ex.Message}", ex);
            return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }
    public Task<ResultWrapper<ulong>> HeadMessageIndex()
    {
        BlockHeader? header = blockTree.FindLatestHeader();

        return header is null
            ? ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError)
            : BlockNumberToMessageIndex((ulong)header.Number);
    }

    public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
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

    public Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber)
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

    public ResultWrapper<string> SetFinalityData(SetFinalityDataParams? parameters)
    {
        if (parameters is null)
            return ResultWrapper<string>.Fail(ArbitrumRpcErrors.FormatNullParameters(), ErrorCodes.InvalidParams);

        try
        {
            if (Logger.IsDebug)
                Logger.Debug($"SetFinalityData called: safe={parameters.SafeFinalityData?.MsgIdx}, " +
                              $"finalized={parameters.FinalizedFinalityData?.MsgIdx}, " +
                              $"validated={parameters.ValidatedFinalityData?.MsgIdx}");

            // Convert RPC parameters to internal types
            ArbitrumFinalityData? safeFinalityData = parameters.SafeFinalityData?.ToArbitrumFinalityData();
            ArbitrumFinalityData? finalizedFinalityData = parameters.FinalizedFinalityData?.ToArbitrumFinalityData();
            ArbitrumFinalityData? validatedFinalityData = parameters.ValidatedFinalityData?.ToArbitrumFinalityData();

            // Set finality data
            _syncMonitor.SetFinalityData(safeFinalityData, finalizedFinalityData, validatedFinalityData);

            if (Logger.IsDebug)
                Logger.Debug("SetFinalityData completed successfully");

            return ResultWrapper<string>.Success("OK");
        }
        catch (Exception ex)
        {
            if (Logger.IsError)
                Logger.Error($"SetFinalityData failed: {ex.Message}", ex);

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
            if (Logger.IsError)
                Logger.Error($"MarkFeedStart failed: {ex.Message}", ex);

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
            if (Logger.IsError)
                Logger.Error($"SetConsensusSyncData failed: {ex.Message}", ex);

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
            if (Logger.IsError)
                Logger.Error($"Synced failed: {ex.Message}", ex);
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
            if (Logger.IsError)
                Logger.Error($"FullSyncProgressMap failed: {ex.Message}", ex);
            return ResultWrapper<Dictionary<string, object>>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public async Task<ResultWrapper<ulong>> ArbOSVersionForMessageIndex(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = await MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return ResultWrapper<ulong>.Fail(
                    blockNumberResult.Result.Error ?? "Failed to convert message index to block number");

            BlockHeader? blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.RequireCanonical);
            if (blockHeader == null)
                return ResultWrapper<ulong>.Fail(ArbitrumRpcErrors.BlockNotFound(blockNumberResult.Data));

            if (Logger.IsTrace)
                Logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, Logger);

            return ResultWrapper<ulong>.Success(headerInfo.ArbOSFormatVersion);
        }
        catch (Exception ex)
        {
            if (Logger.IsError)
                Logger.Error($"Error processing ArbOSVersionForMessageIndex for message index {messageIndex}: {ex.Message}", ex);
            return ResultWrapper<ulong>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public Task<ResultWrapper<MaintenanceStatus>> MaintenanceStatus()
    {
        MaintenanceStatus status = new() { IsRunning = _runningMaintenance };
        return Task.FromResult(ResultWrapper<MaintenanceStatus>.Success(status));
    }

    public Task<ResultWrapper<bool>> ShouldTriggerMaintenance()
    {
        if (_runningMaintenance || _maintenanceThreshold <= TimeSpan.Zero)
            return Task.FromResult(ResultWrapper<bool>.Success(false));

        TimeSpan timeBeforeFlush = processingTimeTracker.TimeBeforeFlush;

        if (timeBeforeFlush <= _maintenanceThreshold / 2 && Logger.IsWarn)
            Logger.Warn($"Time before flush is low: {timeBeforeFlush}, maintenance should be triggered soon");

        bool shouldTrigger = timeBeforeFlush <= _maintenanceThreshold;
        return Task.FromResult(ResultWrapper<bool>.Success(shouldTrigger));
    }

    public Task<ResultWrapper<string>> TriggerMaintenance()
    {
        lock (_maintenanceLock)
        {
            if (_runningMaintenance)
            {
                if (Logger.IsInfo)
                    Logger.Info("Maintenance already running, skipping");
                return Task.FromResult(ResultWrapper<string>.Success("OK"));
            }
            _runningMaintenance = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                CreateBlocksSemaphore.Wait();
                try
                {
                    if (Logger.IsInfo)
                        Logger.Info("Starting trie flush maintenance");

                    worldStateManager.FlushCache(CancellationToken.None);
                    processingTimeTracker.Reset();

                    if (Logger.IsInfo)
                        Logger.Info("Trie flush maintenance completed");
                }
                finally
                {
                    CreateBlocksSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                if (Logger.IsError)
                    Logger.Error($"Trie flush maintenance failed: {ex.Message}", ex);
            }
            finally
            {
                _runningMaintenance = false;
            }
        });

        return Task.FromResult(ResultWrapper<string>.Success("OK"));
    }

    protected async Task<ResultWrapper<MessageResult>> ProduceBlockWhileLockedAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
    {
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber,
            PreviousArbosVersion = headBlockHeader != null ? ArbitrumBlockHeaderInfo.Deserialize(headBlockHeader, Logger).ArbOSFormatVersion : 0
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

            if (resultArgs.ProcessingResult == ProcessingResult.Exception)
            {
                BlockchainException exception = new(
                    resultArgs.Exception?.Message ?? "Block processing threw an unspecified exception.",
                    resultArgs.Exception);

                if (Logger.IsError)
                    Logger.Error($"Block processing failed for {block.Hash}", exception);

                return ResultWrapper<MessageResult>.Fail(exception.Message, ErrorCodes.InternalError);
            }

            return resultArgs.ProcessingResult switch
            {
                ProcessingResult.Success => ResultWrapper<MessageResult>.Success(new MessageResult
                {
                    BlockHash = block.Hash!,
                    SendRoot = GetSendRootFromBlock(block)
                }),
                ProcessingResult.ProcessingError => ResultWrapper<MessageResult>.Fail(resultArgs.Message ?? "Block processing failed.", ErrorCodes.InternalError),
                _ => ResultWrapper<MessageResult>.Fail($"Block processing ended in an unhandled state: {resultArgs.ProcessingResult}", ErrorCodes.InternalError)
            };
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

    protected async Task<ResultWrapper<MessageResult>> ProduceBlockWithoutWaitingOnProcessingQueueAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
    {
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber,
            PreviousArbosVersion = headBlockHeader != null ? ArbitrumBlockHeaderInfo.Deserialize(headBlockHeader, Logger).ArbOSFormatVersion : 0
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
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, Logger);

        // ArbitrumBlockHeaderInfo.Deserialize returns Empty if deserialization fails
        if (headerInfo == ArbitrumBlockHeaderInfo.Empty && Logger.IsWarn)
            Logger.Warn($"Block header info deserialization returned empty result for block {block.Hash}");

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
            Logger.Error("Failed to deserialize ChainConfig from bytes.", exception);
            chainConfig = null;
            return false;
        }
    }
}
