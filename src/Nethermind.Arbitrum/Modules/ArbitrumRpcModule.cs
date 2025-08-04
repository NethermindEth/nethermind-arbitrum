// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
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
        IBlockProcessingQueue processingQueue)
        : IArbitrumRpcModule
    {
        private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumRpcModule>();
        // TODO: implement configuration for ArbitrumRpcModule
        private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, new ArbitrumSyncMonitorConfig(), logManager);

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
            _ = txSource; // TODO: replace with the actual use

            long blockNumber = (await MessageIndexToBlockNumber(parameters.Number)).Data;
            BlockHeader? headBlockHeader = blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
            {
                return ResultWrapper<MessageResult>.Fail(
                    $"wrong message number in digest got {parameters.Number} expected {headBlockHeader.Number}");
            }

            return await ProduceBlock(parameters.Message, blockNumber, headBlockHeader);
        }

        private async Task<ResultWrapper<MessageResult>> ProduceBlock(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
        {
            var payload = new ArbitrumPayloadAttributes()
            {
                MessageWithMetadata = messageWithMetadata,
                Number = blockNumber,
            };

            TaskCompletionSource<BlockRemovedEventArgs?> blockProcessedTaskCompletionSource = new();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            cts.Token.Register(() => blockProcessedTaskCompletionSource.TrySetCanceled());

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

            blockTree.NewBestSuggestedBlock += OnNewBestBlock;
            try
            {
                Block? block = await trigger.BuildBlock(
                    parentHeader: headBlockHeader,
                    payloadAttributes: payload);

                if (block?.Hash is null)
                {
                    return ResultWrapper<MessageResult>.Fail("Failed to build block or block has no hash.", ErrorCodes.InternalError);
                }

                if (_logger.IsTrace) _logger.Trace($"Built block: hash={block?.Hash}");
                BlockRemovedEventArgs? resultArgs = await blockProcessedTaskCompletionSource.Task;
                if (resultArgs.ProcessingResult == ProcessingResult.Exception)
                {
                    var exception = new BlockchainException(
                        resultArgs.Exception?.Message ?? "Block processing threw an unspecified exception.",
                        resultArgs.Exception);
                    if (_logger.IsError) _logger.Error("Block processing failed for {BlockHash}", exception);
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
            catch (TaskCanceledException)
            {
                return ResultWrapper<MessageResult>.Fail("Timeout waiting for block processing result.", ErrorCodes.Timeout);
            }
            finally
            {
                blockTree.NewBestSuggestedBlock -= OnNewBestBlock;
                if (onBlockRemovedHandler is not null) processingQueue.BlockRemoved -= onBlockRemovedHandler;
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
                checked
                {
                    ulong blockNumber = GetGenesisBlockNumber() + messageIndex;
                    if (blockNumber > long.MaxValue)
                    {
                        return ResultWrapper<long>.Fail(ArbitrumRpcErrors.FormatExceedsLongMax(blockNumber));
                    }
                    return ResultWrapper<long>.Success((long)blockNumber);
                }
            }
            catch (OverflowException)
            {
                return ResultWrapper<long>.Fail(ArbitrumRpcErrors.Overflow);
            }
        }

        public Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber)
        {
            ulong genesis = GetGenesisBlockNumber();

            if (blockNumber < genesis)
            {
                return ResultWrapper<ulong>.Fail($"blockNumber {blockNumber} < genesis {genesis}");
            }

            return ResultWrapper<ulong>.Success(blockNumber - genesis);
        }

        private ulong GetGenesisBlockNumber()
        {
            return specHelper.GenesisBlockNum;
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

        public void MarkFeedStart(ulong to)
        {
            cachedL1PriceData.MarkFeedStart(to);
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

        private BlockHeader? GetParentBlockHeader(long blockNumber)
        {
            var parentBlockNumber = blockNumber - 1;
            Hash256? blockHash = blockTree.FindBlockHash(parentBlockNumber);
            return blockHash is null ? null : blockTree.FindHeader(blockHash, parentBlockNumber);
        }
    }
}
