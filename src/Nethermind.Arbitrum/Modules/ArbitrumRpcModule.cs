// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
using Nethermind.Merge.Plugin;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Modules
{

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
        private readonly int _blockProcessingTimeout = arbitrumConfig.BlockProcessingTimeout;

        public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
        {
            if (message.InitialL1BaseFee.IsZero)
            {
                return ResultWrapper<MessageResult>.Fail("InitialL1BaseFee must be greater than zero", ErrorCodes.InvalidParams);
            }

            if (message.SerializedChainConfig is null || message.SerializedChainConfig.Length == 0)
            {
                return ResultWrapper<MessageResult>.Fail("SerializedChainConfig must not be empty.", ErrorCodes.InvalidParams);
            }

            if (!TryDeserializeChainConfig(message.SerializedChainConfig, out ChainConfig? chainConfig))
            {
                return ResultWrapper<MessageResult>.Fail("Failed to deserialize ChainConfig.", ErrorCodes.InvalidParams);
            }

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
            // Non-blocking attempt to acquire the semaphore.
            if (!await _createBlocksSemaphore.WaitAsync(0))
            {
                return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);
            }

            try
            {
                _ = txSource; // TODO: replace with the actual use

                long blockNumber = (await MessageIndexToBlockNumber(parameters.Number)).Data;
                BlockHeader? headBlockHeader = blockTree.Head?.Header;

                if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
                {
                    return ResultWrapper<MessageResult>.Fail(
                        $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");
                }

                return await ProduceBlockWhileLockedAsync(parameters.Message, blockNumber, headBlockHeader);
            }
            finally
            {
                // Ensure the semaphore is released, equivalent to Go's `defer Unlock()`.
                _createBlocksSemaphore.Release();
            }
        }

        public async Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters)
        {
            if (parameters.MsgIdxOfFirstMsgToAdd == 0)
                return ResultWrapper<MessageResult[]>.Fail("Cannot reorg out genesis", ErrorCodes.InternalError);

            await _createBlocksSemaphore.WaitAsync();

            var lastBlockNumToKeep = (await MessageIndexToBlockNumber(parameters.MsgIdxOfFirstMsgToAdd - 1)).Data;
            Block? blockToKeep = blockTree.FindBlock(lastBlockNumToKeep, BlockTreeLookupOptions.RequireCanonical);
            if (blockToKeep is null)
                return ResultWrapper<MessageResult[]>.Fail("Reorg target block not found");

            BlockHeader? safeBlock = blockTree.FindSafeHeader();
            if (safeBlock is not null)
            {
                if (safeBlock.Number > blockToKeep.Number)
                {
                    _logger.Info($"Reorg target block is below safe block lastBlockNumToKeep:{blockToKeep.Number} currentSafeBlock:{safeBlock.Number}");
                    blockTree.UpdateSafeHash(null);
                }
            }

            BlockHeader? finalBlock = blockTree.FindFinalizedHeader();
            if (finalBlock is not null)
            {
                if (finalBlock.Number > blockToKeep.Number)
                {
                    _logger.Info($"Reorg target block is below final block lastBlockNumToKeep:{blockToKeep.Number} currentFinalBlock:{finalBlock.Number}");
                    blockTree.UpdateFinalHash(null);
                }
            }

            blockTree.UpdateMainChain([blockToKeep], true, true);

            try
            {
                MessageResult[] messageResults = new MessageResult[parameters.NewMessages.Length];
                for (int i = 0; i < parameters.NewMessages.Length; i++)
                {
                    MessageWithMetadataAndBlockInfo message = parameters.NewMessages[i];
                    BlockHeader? headBlockHeader = blockTree.Head?.Header;

                    ResultWrapper<MessageResult> blockResult = await ProduceBlockWhileLockedAsync(message.MessageWithMeta,
                        headBlockHeader!.Number + 1, headBlockHeader);

                    if (blockResult.Result != Result.Success)
                        return ResultWrapper<MessageResult[]>.Fail(blockResult.Result.Error!, blockResult.ErrorCode);

                    messageResults[i] = blockResult.Data;
                }

                return ResultWrapper<MessageResult[]>.Success(messageResults);
            }
            finally
            {
                _createBlocksSemaphore.Release();
            }
        }

        public async Task<ResultWrapper<MessageResult>> ResultAtPos(ulong messageIndex)
        {
            try
            {
                var blockNumberResult = await MessageIndexToBlockNumber(messageIndex);
                if (blockNumberResult.Result != Result.Success)
                {
                    return ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Unknown error converting message index");
                }

                var blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.None);
                if (blockHeader == null)
                {
                    return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound);
                }

                if (_logger.IsTrace) _logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

                var headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, _logger);
                return ResultWrapper<MessageResult>.Success(new MessageResult
                {
                    BlockHash = blockHeader.Hash ?? Hash256.Zero,
                    SendRoot = headerInfo.SendRoot,
                });
            }
            catch (Exception ex)
            {
                if (_logger.IsError) _logger.Error($"Error processing ResultAtPos for message index {messageIndex}: {ex.Message}", ex);
                return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError);
            }
        }
        public Task<ResultWrapper<ulong>> HeadMessageNumber()
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
                if (_logger.IsDebug)
                {
                    _logger.Debug($"SetFinalityData called: safe={parameters.SafeFinalityData?.MsgIdx}, " +
                                  $"finalized={parameters.FinalizedFinalityData?.MsgIdx}, " +
                                  $"validated={parameters.ValidatedFinalityData?.MsgIdx}");
                }

                // Convert RPC parameters to internal types
                var safeFinalityData = parameters.SafeFinalityData?.ToArbitrumFinalityData();
                var finalizedFinalityData = parameters.FinalizedFinalityData?.ToArbitrumFinalityData();
                var validatedFinalityData = parameters.ValidatedFinalityData?.ToArbitrumFinalityData();

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
                Number = blockNumber,
            };

            TaskCompletionSource<BlockRemovedEventArgs?> blockProcessedTaskCompletionSource = new();
            EventHandler<BlockRemovedEventArgs>? onBlockRemovedHandler = null;


            void OnNewBestBlock(object? sender, BlockEventArgs blockEventArgs)
            {
                Hash256? blockHash = blockEventArgs.Block.Hash;
                onBlockRemovedHandler = (o, e) =>
                {
                    if (e.BlockHash == blockHash)
                    {
                        processingQueue.BlockRemoved -= onBlockRemovedHandler;
                        blockProcessedTaskCompletionSource.TrySetResult(e);
                    }
                };
                processingQueue.BlockRemoved += onBlockRemovedHandler;
            }

            blockTree.NewSuggestedBlock += OnNewBestBlock;
            try
            {
                Block? block = await trigger.BuildBlock(
                    parentHeader: headBlockHeader,
                    payloadAttributes: payload);

                if (block?.Hash is null)
                    return ResultWrapper<MessageResult>.Fail("Failed to build block or block has no hash.", ErrorCodes.InternalError);

                // adding to processing queue because the block would not directly be added to the queue when suggesting because blockTDD < bestSuggestTDD
                processingQueue.Enqueue(block, ProcessingOptions.StoreReceipts);

                if (_logger.IsTrace) _logger.Trace($"Built block: hash={block?.Hash}");
                BlockRemovedEventArgs? resultArgs = await blockProcessedTaskCompletionSource.Task
                    .WaitAsync(TimeSpan.FromSeconds(_blockProcessingTimeout));

                if (resultArgs.ProcessingResult == ProcessingResult.Exception)
                {
                    var exception = new BlockchainException(
                        resultArgs.Exception?.Message ?? "Block processing threw an unspecified exception.",
                        resultArgs.Exception);
                    if (_logger.IsError) _logger.Error($"Block processing failed for {block?.Hash}", exception);
                    return ResultWrapper<MessageResult>.Fail(exception.Message, ErrorCodes.InternalError);
                }

                return resultArgs.ProcessingResult switch
                {
                    ProcessingResult.Success => ResultWrapper<MessageResult>.Success(new MessageResult
                    {
                        BlockHash = block!.Hash!,
                        SendRoot = Hash256.Zero
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
                blockTree.NewBestSuggestedBlock -= OnNewBestBlock;
                if (onBlockRemovedHandler is not null) processingQueue.BlockRemoved -= onBlockRemovedHandler;
            }
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
}
