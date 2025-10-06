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
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

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
    IArbitrumConfig arbitrumConfig)
    : IArbitrumRpcModule
{
    // This semaphore acts as the `createBlocksMutex` from the Go implementation.
    // It ensures that block creation (DigestMessage) and reorgs are serialized.
    private readonly SemaphoreSlim _createBlocksSemaphore = new(1, 1);

    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumRpcModule>();
    private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, arbitrumConfig, logManager);

    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<Block>> _newBestSuggestedBlockEvents = new();
    private readonly ConcurrentDictionary<Hash256, TaskCompletionSource<BlockRemovedEventArgs>> _blockRemovedEvents = new();

    public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
    {
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

    public async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
    {
        if (_logger.IsTrace)
        {
            _logger.Trace($"DigestMessage called for message index {parameters.Index}");
        }
        
        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtMessageIndex(parameters.Index);
        if (resultAtMessageIndex.Result == Result.Success)
        {
            if (_logger.IsTrace)
            {
                _logger.Trace($"Message {parameters.Index} already processed, returning cached result");
            }
            return resultAtMessageIndex;
        }

        // Blocking acquisition of the semaphore to serialize DigestMessage calls.
        // This matches the Go implementation's blocking mutex behavior.
        await _createBlocksSemaphore.WaitAsync();

        try
        {
            _ = txSource; // TODO: replace with the actual use

            ResultWrapper<long> blockNumberResult = await MessageIndexToBlockNumber(parameters.Index);
            if (blockNumberResult.Result != Result.Success)
            {
                return ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Failed to get block number");
            }
            
            long blockNumber = blockNumberResult.Data;
            BlockHeader? headBlockHeader = blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
            {
                string error = $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number + 1}";
                _logger.Error(error);
                return ResultWrapper<MessageResult>.Fail(error);
            }

            ResultWrapper<MessageResult> result = await ProduceBlockWhileLockedAsync(parameters.Message, blockNumber, headBlockHeader);
            
            if (result.Result != Result.Success && _logger.IsError)
            {
                _logger.Error($"Failed to produce block for message {parameters.Index}: {result.Result.Error}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"DigestMessage failed for message {parameters.Index}: {ex.Message}", ex);
            return ResultWrapper<MessageResult>.Fail($"DigestMessage failed: {ex.Message}");
        }
        finally
        {
            // Ensure the semaphore is released, equivalent to Go's `defer Unlock()`.
            _createBlocksSemaphore.Release();
        }
    }

    public async Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = await MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Unknown error converting message index");

            BlockHeader? blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.None);
            if (blockHeader == null)
                return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound);

            if (_logger.IsTrace)
                _logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, _logger);
            return ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = blockHeader.Hash ?? Hash256.Zero,
                SendRoot = headerInfo.SendRoot,
            });
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error processing ResultAtMessageIndex for message index {messageIndex}: {ex.Message}", ex);
            return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }
    public Task<ResultWrapper<ulong>> HeadMessageIndex()
    {
        BlockHeader? headHeader = blockTree.Head?.Header;
        
        if (headHeader is null)
        {
            // Check if there are ANY blocks in the database
            BlockHeader? latestHeader = blockTree.FindLatestHeader();
            
            if (latestHeader is null)
            {
                // No blocks at all - valid initial state for external execution client.
                // Return 0 to indicate we're at the genesis state before DigestInitMessage.
                if (_logger.IsTrace)
                {
                    _logger.Trace("HeadMessageIndex: no blocks in database, returning 0");
                }
                return Task.FromResult(ResultWrapper<ulong>.Success(0UL));
            }
            
            // Blocks exist but Head is not set - this is an error condition
            if (_logger.IsError)
            {
                _logger.Error("HeadMessageIndex: blocks exist but Head is null");
            }
            return Task.FromResult(ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError));
        }
        
        ulong blockNumber = (ulong)headHeader.Number;
        ulong messageIndex = MessageBlockConverter.BlockNumberToMessageIndex(blockNumber, specHelper);
        
        return Task.FromResult(ResultWrapper<ulong>.Success(messageIndex));
    }

    public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
    {
        try
        {
            long blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(messageIndex, specHelper);
            return Task.FromResult(ResultWrapper<long>.Success(blockNumber));
        }
        catch (OverflowException)
        {
            return Task.FromResult(ResultWrapper<long>.Fail(ArbitrumRpcErrors.Overflow));
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
            if (_logger.IsDebug)
            {
                _logger.Debug($"SetFinalityData called: safe={parameters.SafeFinalityData?.MsgIdx}, " +
                              $"finalized={parameters.FinalizedFinalityData?.MsgIdx}, " +
                              $"validated={parameters.ValidatedFinalityData?.MsgIdx}");
            }

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

    private async Task<ResultWrapper<MessageResult>> ProduceBlockWhileLockedAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
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

        blockTree.NewBestSuggestedBlock += OnNewBestSuggestedBlock;
        processingQueue.BlockRemoved += OnBlockRemoved;

        try
        {
            Block? block = await trigger.BuildBlock(parentHeader: headBlockHeader, payloadAttributes: payload);
            if (block?.Hash is null)
                return ResultWrapper<MessageResult>.Fail("Failed to build block or block has no hash.", ErrorCodes.InternalError);

            TaskCompletionSource<Block> newBestBlockTcs = _newBestSuggestedBlockEvents.GetOrAdd(block.Hash, _ => new TaskCompletionSource<Block>());
            TaskCompletionSource<BlockRemovedEventArgs> blockRemovedTcs = _blockRemovedEvents.GetOrAdd(block.Hash, _ => new TaskCompletionSource<BlockRemovedEventArgs>());

            // CRITICAL FIX: Manually suggest the block since ArbitrumBlockProducer extends PostMergeBlockProducer
            // and ProducedBlockSuggester skips post-merge blocks (expecting Engine API to suggest them).
            // But Arbitrum uses DigestMessage RPC, not Engine API, so we must manually suggest here.
            blockTree.SuggestBlock(block);

            using CancellationTokenSource processingTimeoutTokenSource = arbitrumConfig.BuildProcessingTimeoutTokenSource();
            await Task.WhenAll(newBestBlockTcs.Task, blockRemovedTcs.Task)
                .WaitAsync(processingTimeoutTokenSource.Token);

            BlockRemovedEventArgs resultArgs = blockRemovedTcs.Task.Result;

            if (resultArgs.ProcessingResult == ProcessingResult.Exception)
            {
                BlockchainException exception = new(
                    resultArgs.Exception?.Message ?? "Block processing threw an unspecified exception.",
                    resultArgs.Exception);

                if (_logger.IsError)
                    _logger.Error($"Block processing failed for {block.Hash}", exception);

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
