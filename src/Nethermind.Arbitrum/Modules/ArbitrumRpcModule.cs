// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Merge.Plugin;
using Nethermind.Specs.ChainSpecStyle;
using ZstdSharp.Unsafe;

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
        bool reorgSequencingEnabled = true)
        : IArbitrumRpcModule
    {

        // This semaphore acts as the `createBlocksMutex` from the Go implementation.
        // It ensures that block creation (DigestMessage) and reorgs are serialized.
        private readonly SemaphoreSlim _createBlocksSemaphore = new(1, 1);

        private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumRpcModule>();
        // TODO: implement configuration for ArbitrumRpcModule
        private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, new ArbitrumSyncMonitorConfig(), logManager);

        /// <summary>
        /// Fired after old messages have been re-processed during a reorg.
        /// </summary>
        public event EventHandler<MessagesResequencedEventArgs>? MessagesResequenced;

        /// <summary>
        /// Notifies that a resequencing (reorg) operation is about to start.
        /// </summary>
        public event EventHandler<ResequenceOperationNotifier>? ResequenceOperationStarting;

        public void Init()
        {
            MessagesResequenced += OnResequencingMessages;
        }

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

        public async Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters)
        {
            if (parameters.MsgIdxOfFirstMsgToAdd == 0)
            {
                return ResultWrapper<MessageResult[]>.Fail("cannot reorg out genesis", ErrorCodes.InternalError);
            }

            await _createBlocksSemaphore.WaitAsync();
            bool resequencing = false;

            var lastBlockNumToKeep = (await MessageIndexToBlockNumber(parameters.MsgIdxOfFirstMsgToAdd)).Data;
            BlockHeader? blockToKeep = blockTree.FindHeader(lastBlockNumToKeep, BlockTreeLookupOptions.RequireCanonical);
            if (blockToKeep is null) return ResultWrapper<MessageResult[]>.Fail("reorg target block not found");

            BlockHeader? safeBlock = blockTree.FindSafeHeader();
            if (safeBlock is not null)
            {
                if (safeBlock.Number > blockToKeep.Number)
                {
                    _logger.Info($"reorg target block is below safe block lastBlockNumToKeep:{blockToKeep.Number} currentSafeBlock:{safeBlock.Number}");
                    // TODO: set safe block to nil - do we need this?
                }
            }

            BlockHeader? finalBlock = blockTree.FindFinalizedHeader();
            if (finalBlock is not null)
            {
                if (finalBlock.Number > blockToKeep.Number)
                {
                    _logger.Info($"reorg target block is below final block lastBlockNumToKeep:{blockToKeep.Number} currentFinalBlock:{finalBlock.Number}");
                    // TODO: set final block to nil - do we need this?
                }
            }

            blockTree.UpdateHeadBlock(blockToKeep.Hash!);

            // TODO: implement stylus api
            // tag := s.bc.StateCache().WasmCacheTag()
            // // reorg Rust-side VM state
            // C.stylus_reorg_vm(C.uint64_t(lastBlockNumToKeep), C.uint32_t(tag))

            ResequenceOperationStarting?.Invoke(this, new ResequenceOperationNotifier());

            try
            {
                MessageResult[] messageResults = new MessageResult[parameters.NewMessages.Length];
                for (int i = 0; i < parameters.NewMessages.Length; i++)
                {
                    MessageWithMetadataAndBlockInfo message = parameters.NewMessages[i];
                    BlockHeader? headBlockHeader = blockTree.Head?.Header;
                    messageResults[i] = (await ProduceBlockWhileLockedAsync(message.MessageWithMeta, headBlockHeader.Number + 1, headBlockHeader)).Data;
                }

                // TODO: reorg the recorder - implement the recorder
                // if s.recorder != nil {
                //     s.recorder.ReorgTo(lastBlockToKeep.Header())
                // }

                if (parameters.OldMessages.Length > 0)
                {
                    MessagesResequenced?.Invoke(this, new MessagesResequencedEventArgs(parameters.OldMessages));
                    resequencing = true;
                }

                return ResultWrapper<MessageResult[]>.Success(messageResults);
            }
            finally
            {
                if (!resequencing) _createBlocksSemaphore.Release();
            }
        }

        private async void OnResequencingMessages(object? caller, MessagesResequencedEventArgs messages)
        {
            try
            {
                await _resequenceReorgedMessages(messages.OldMessages);
            }
            finally
            {
                // we are here with a lock acquired by Reorg call and its not released here, we need to release this here
                _createBlocksSemaphore.Release();
            }
        }

        private async Task _resequenceReorgedMessages(MessageWithMetadata[] oldMessages)
        {
            if(!reorgSequencingEnabled) return;

            _logger.Info($"Trying to resequence {oldMessages.Length} messages.");
            BlockHeader? lastBlockHeader = blockTree.Head?.Header;

            if (lastBlockHeader is null)
            {
                _logger.Error("Block header not found during resequence.");
                return;
            }

            ulong nextDelayedMsgIdx = lastBlockHeader.Nonce;

            foreach (var msg in oldMessages)
            {
                if (msg?.Message?.Header is null) continue;

                L1IncomingMessageHeader header = msg.Message.Header;

                // Is it a delayed message from L1?
                if (header.RequestId is not null)
                {
                    ulong delayedMsgIdx = (ulong)(new UInt256(header.RequestId.Bytes));
                    if (delayedMsgIdx != nextDelayedMsgIdx)
                    {
                        _logger.Info(
                            $"Not resequencing delayed message due to unexpected index. Expected: {nextDelayedMsgIdx}, Found: {delayedMsgIdx}");
                        continue;
                    }

                    await SequenceDelayedMessageWhileLockedAsync(msg.Message, delayedMsgIdx);
                    nextDelayedMsgIdx++;
                    continue;
                }

                // Is it a standard sequencer batch of user transactions?
                if (header.Kind == ArbitrumL1MessageKind.L2Message || header.Sender != ArbosAddresses.BatchPosterAddress)
                {
                    _logger.Warn($"Skipping non-standard sequencer message found from reorg {header}");
                    return;
                }

                IReadOnlyList<Transaction> txns;
                try
                {
                    txns = NitroL2MessageParser.ParseTransactions(msg.Message, chainSpec.ChainId, _logger);
                }
                catch (Exception e)
                {
                    _logger.Error("Failed to parse sequencer message found from reorg.", e);
                    return;
                }

                await SequenceTransactionsWhileLockedAsync(header, txns);
            }
        }

        private async Task SequenceTransactionsWhileLockedAsync(L1IncomingMessageHeader header, object txes)
        {
            throw new NotImplementedException();
        }

        private async Task SequenceDelayedMessageWhileLockedAsync(L1IncomingMessage msgMessage, ulong delayedMsgIdx)
        {
            throw new NotImplementedException();
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
                BlockRemovedEventArgs? resultArgs = await blockProcessedTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(1));
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

    }
}

public class MessagesResequencedEventArgs(MessageWithMetadata[] messages) : EventArgs
{
    public MessageWithMetadata[] OldMessages { get; } = messages;
}

public readonly struct ResequenceOperationNotifier;
