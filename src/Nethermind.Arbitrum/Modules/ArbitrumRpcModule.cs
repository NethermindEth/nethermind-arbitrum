// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Math;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumRpcModule : IArbitrumRpcModule
{
    protected readonly SemaphoreSlim CreateBlocksSemaphore = new(1, 1);

    protected readonly ILogger Logger;
    private readonly ArbitrumSyncMonitor _syncMonitor;

    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<Block>> _newBestSuggestedBlockEvents = new();
    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<BlockRemovedEventArgs>> _blockRemovedEvents = new();

    private readonly ArbitrumBlockTreeInitializer _initializer;
    private readonly IBlockTree _blockTree;
    private readonly IManualBlockProductionTrigger _trigger;
    private readonly ArbitrumRpcTxSource _txSource;
    private readonly ChainSpec _chainSpec;
    private readonly IArbitrumSpecHelper _specHelper;
    private readonly CachedL1PriceData _cachedL1PriceData;
    private readonly IBlockProcessingQueue _processingQueue;
    private readonly IArbitrumConfig _arbitrumConfig;
    private readonly IBlocksConfig _blocksConfig;
    protected readonly ArbitrumBlockProducer? _blockProducer;

    protected Task? _blockPreWarmTask;
    protected CancellationTokenSource? _prewarmCancellation;

    public ArbitrumRpcModule(ArbitrumBlockTreeInitializer initializer,
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
        IBlockProducer? blockProducer)
    {
        _initializer = initializer;
        _blockTree = blockTree;
        _trigger = trigger;
        _txSource = txSource;
        _chainSpec = chainSpec;
        _specHelper = specHelper;
        _cachedL1PriceData = cachedL1PriceData;
        _processingQueue = processingQueue;
        _arbitrumConfig = arbitrumConfig;
        _blocksConfig = blocksConfig;
        _blockProducer = blockProducer as ArbitrumBlockProducer;
        Logger = logManager.GetClassLogger<ArbitrumRpcModule>();
        _syncMonitor = new ArbitrumSyncMonitor(blockTree, specHelper, arbitrumConfig, logManager);
    }

    public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
    {
        if (message.InitialL1BaseFee.IsZero)
            return ResultWrapper<MessageResult>.Fail("InitialL1BaseFee must be greater than zero", ErrorCodes.InvalidParams);

        if (message.SerializedChainConfig is null || message.SerializedChainConfig.Length == 0)
            return ResultWrapper<MessageResult>.Fail("SerializedChainConfig must not be empty.", ErrorCodes.InvalidParams);

        if (!TryDeserializeChainConfig(message.SerializedChainConfig, out ChainConfig? chainConfig))
            return ResultWrapper<MessageResult>.Fail("Failed to deserialize ChainConfig.", ErrorCodes.InvalidParams);

        ParsedInitMessage initMessage = new(_chainSpec.ChainId, message.InitialL1BaseFee, chainConfig, message.SerializedChainConfig);
        BlockHeader genesisHeader = _initializer.Initialize(initMessage);

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
            _ = _txSource; // TODO: replace with the actual use

            long blockNumber = (await MessageIndexToBlockNumber(parameters.Index)).Data;
            BlockHeader? headBlockHeader = _blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
                return ResultWrapper<MessageResult>.Fail(
                    $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");


            if (_blockProducer is not null)
            {
                if (_prewarmCancellation is not null)
                {
                    CancellationTokenExtensions.CancelDisposeAndClear(ref _prewarmCancellation);
                    _prewarmCancellation = null;
                }
                _blockPreWarmTask?.GetAwaiter().GetResult();
                _blockPreWarmTask = null;
            }

            ResultWrapper<MessageResult> result = _blocksConfig.BuildBlocksOnMainState ?
                await ProduceBlockWithoutWaitingOnProcessingQueueAsync(parameters.Message, blockNumber, headBlockHeader, parameters.MessageForPrefetch) :
                await ProduceBlockWhileLockedAsync(parameters.Message, blockNumber, headBlockHeader);

            if (_blockProducer is not null)
            {
                _blockProducer.ClearPreWarmCaches();

                if (result.Result != Result.Success || parameters.MessageForPrefetch is null)
                    return result;

                headBlockHeader = _blockTree.Head?.Header;
                ArbitrumPayloadAttributes payload = new()
                {
                    MessageWithMetadata = parameters.MessageForPrefetch,
                    Number = blockNumber + 1
                };
                _prewarmCancellation = new();
                _blockPreWarmTask = _blockProducer.PreWarmBlock(headBlockHeader, null, payload, IBlockProducer.Flags.None, _prewarmCancellation.Token);
            }
            return result;
        }
        finally
        {
            // Ensure the semaphore is released, equivalent to Go's `defer Unlock()`.
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

            BlockHeader? blockHeader = _blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.None);
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
        BlockHeader? header = _blockTree.FindLatestHeader();

        return header is null
            ? ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError)
            : BlockNumberToMessageIndex((ulong)header.Number);
    }

    public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
    {
        try
        {
            long blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(messageIndex, _specHelper);
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
            ulong messageIndex = MessageBlockConverter.BlockNumberToMessageIndex(blockNumber, _specHelper);
            return ResultWrapper<ulong>.Success(messageIndex);
        }
        catch (ArgumentOutOfRangeException)
        {
            ulong genesis = _specHelper.GenesisBlockNum;
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
            _cachedL1PriceData.MarkFeedStart(to);
            return ResultWrapper<string>.Success("OK");
        }
        catch (Exception ex)
        {
            if (Logger.IsError)
                Logger.Error($"MarkFeedStart failed: {ex.Message}", ex);

            return ResultWrapper<string>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    protected async Task<ResultWrapper<MessageResult>> ProduceBlockWhileLockedAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
    {
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber
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

        _blockTree.NewBestSuggestedBlock += OnNewBestSuggestedBlock;
        _processingQueue.BlockRemoved += OnBlockRemoved;

        try
        {
            Block? block = await _trigger.BuildBlock(parentHeader: headBlockHeader, payloadAttributes: payload);
            if (block?.Hash is null)
                return ResultWrapper<MessageResult>.Fail("Failed to build block or block has no hash.", ErrorCodes.InternalError);

            TaskCompletionSource<Block> newBestBlockTcs = _newBestSuggestedBlockEvents.GetOrAdd(block.Hash, _ => new TaskCompletionSource<Block>());
            TaskCompletionSource<BlockRemovedEventArgs> blockRemovedTcs = _blockRemovedEvents.GetOrAdd(block.Hash, _ => new TaskCompletionSource<BlockRemovedEventArgs>());

            using CancellationTokenSource processingTimeoutTokenSource = _arbitrumConfig.BuildProcessingTimeoutTokenSource();
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
            _blockTree.NewBestSuggestedBlock -= OnNewBestSuggestedBlock;
            _processingQueue.BlockRemoved -= OnBlockRemoved;

            _newBestSuggestedBlockEvents.Clear();
            _blockRemovedEvents.Clear();
        }
    }

    protected async Task<ResultWrapper<MessageResult>> ProduceBlockWithoutWaitingOnProcessingQueueAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader, MessageWithMetadata? messageForPrefetch)
    {
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber
        };

        try
        {
            Block? block = await _trigger.BuildBlock(parentHeader: headBlockHeader, payloadAttributes: payload);

            if (_prewarmCancellation is not null)
            {
                CancellationTokenExtensions.CancelDisposeAndClear(ref _prewarmCancellation);
                _prewarmCancellation = null;
            }
            _blockPreWarmTask?.GetAwaiter().GetResult();
            _blockPreWarmTask = null;

            _blockProducer?.ClearPreWarmCaches();

            if (block?.Hash is null)
                return ResultWrapper<MessageResult>.Fail("Failed to build block or block has no hash.", ErrorCodes.InternalError);

            ResultWrapper<MessageResult> result = ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = block.Hash!,
                SendRoot = GetSendRootFromBlock(block)
            });

            if (messageForPrefetch is null)
                return result;

            ArbitrumPayloadAttributes prefetchPayload = new()
            {
                MessageWithMetadata = messageForPrefetch,
                Number = block.Number + 1
            };
            _prewarmCancellation = new();
            _blockPreWarmTask = _blockProducer?.PreWarmBlock(block.Header, null, prefetchPayload, IBlockProducer.Flags.None, _prewarmCancellation.Token);

            return result;
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
