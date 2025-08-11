// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Exceptions;
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
                return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);

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

                ResultWrapper<MessageResult> msgResult;
                try
                {
                    Block? block = await ProduceBlockWhileLockedAsync(parameters.Message, headBlockHeader.Number + 1, headBlockHeader);
                    msgResult = ResultWrapper<MessageResult>.Success(new MessageResult
                    {
                        BlockHash = block!.Hash!,
                        SendRoot = Hash256.Zero
                    });
                }
                catch (ArbitrumBlockProductionException e)
                {
                    msgResult = ResultWrapper<MessageResult>.Fail(e.Message, e.ErrorCode);
                }

                return msgResult;
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
            if (!GetMessageIndexFromBlockNumber(blockNumber, out ulong? messageIndex))
                return ResultWrapper<ulong>.Fail($"blockNumber {blockNumber} < genesis {GetGenesisBlockNumber()}");

            return ResultWrapper<ulong>.Success(messageIndex!.Value);
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

        public async Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters)
        {
            if (parameters.MsgIdxOfFirstMsgToAdd == 0)
                return ResultWrapper<MessageResult[]>.Fail("Cannot reorg out genesis", ErrorCodes.InternalError);


            await _createBlocksSemaphore.WaitAsync();
            bool resequencing = false;

            var lastBlockNumToKeep = (await MessageIndexToBlockNumber(parameters.MsgIdxOfFirstMsgToAdd)).Data;
            BlockHeader? blockToKeep = blockTree.FindHeader(lastBlockNumToKeep, BlockTreeLookupOptions.RequireCanonical);
            if (blockToKeep is null) return ResultWrapper<MessageResult[]>.Fail("Reorg target block not found");

            BlockHeader? safeBlock = blockTree.FindSafeHeader();
            if (safeBlock is not null)
            {
                if (safeBlock.Number > blockToKeep.Number)
                {
                    _logger.Info($"Reorg target block is below safe block lastBlockNumToKeep:{blockToKeep.Number} currentSafeBlock:{safeBlock.Number}");
                    // TODO: set safe block to nil - do we need this?
                }
            }

            BlockHeader? finalBlock = blockTree.FindFinalizedHeader();
            if (finalBlock is not null)
            {
                if (finalBlock.Number > blockToKeep.Number)
                {
                    _logger.Info($"Reorg target block is below final block lastBlockNumToKeep:{blockToKeep.Number} currentFinalBlock:{finalBlock.Number}");
                    // TODO: set final block to nil - do we need this?
                }
            }

            blockTree.UpdateHeadBlock(blockToKeep.Hash!);

            // TODO: implement stylus api
            //      tag := s.bc.StateCache().WasmCacheTag()
            //      // reorg Rust-side VM state
            //      C.stylus_reorg_vm(C.uint64_t(lastBlockNumToKeep), C.uint32_t(tag))

            ResequenceOperationStarting?.Invoke(this, new ResequenceOperationNotifier());

            try
            {
                MessageResult[] messageResults = new MessageResult[parameters.NewMessages.Length];
                for (int i = 0; i < parameters.NewMessages.Length; i++)
                {
                    MessageWithMetadataAndBlockInfo message = parameters.NewMessages[i];
                    BlockHeader? headBlockHeader = blockTree.Head?.Header;

                    MessageResult msgResult;
                    try
                    {
                        Block? block = await ProduceBlockWhileLockedAsync(message.MessageWithMeta,
                            headBlockHeader.Number + 1, headBlockHeader);
                        msgResult = new MessageResult
                        {
                            BlockHash = block!.Hash!,
                            SendRoot = Hash256.Zero
                        };
                    }
                    catch (ArbitrumBlockProductionException e)
                    {
                        return ResultWrapper<MessageResult[]>.Fail(e.Message, e.ErrorCode);
                    }
                    catch (Exception)
                    {
                        return ResultWrapper<MessageResult[]>.Fail("Unknown error", ErrorCodes.InternalError);
                    }

                    messageResults[i] = msgResult;
                }

                // TODO: reorg the recorder - implement the recorder
                //      if s.recorder != nil {
                //          s.recorder.ReorgTo(lastBlockToKeep.Header())
                //      }

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

        public async Task<ResultWrapper<MessageResult>> SequenceDelayedMessage(SequenceDelayedMessageParameters parameters)
        {
            await _createBlocksSemaphore.WaitAsync();
            try
            {
                return await SequenceDelayedMessageWhileLockedAsync(parameters.Message, parameters.Number);
            }
            finally
            {
                _createBlocksSemaphore.Release();
            }
        }

        private async void OnResequencingMessages(object? caller, MessagesResequencedEventArgs messages)
        {
            try
            {
                await ResequenceReorgedMessages(messages.OldMessages);
            }
            finally
            {
                // we are here with a lock acquired by Reorg call and its not released here, we need to release this here
                _createBlocksSemaphore.Release();
            }
        }



        private async Task ResequenceReorgedMessages(MessageWithMetadata[] oldMessages)
        {
            if (!reorgSequencingEnabled) return;

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
                if (header.Kind != ArbitrumL1MessageKind.L2Message || header.Sender != ArbosAddresses.BatchPosterAddress)
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

                await SequenceTransactionsWhileLockedAsync(header, txns, null);
            }
        }

        private async Task<ResultWrapper<Block?>> SequenceTransactionsWhileLockedAsync(L1IncomingMessageHeader header, IReadOnlyList<Transaction> txes, HashSet<Hash256AsKey>? timeBoostedTxn)
        {
            BlockHeader? headBlockHeader = blockTree.Head?.Header;
            if (headBlockHeader is null)
            {
                return ResultWrapper<Block?>.Fail("Could not get head block for sequencing transactions.");
            }

            var blockNumber = headBlockHeader.Number + 1;
            if (!GetMessageIndexFromBlockNumber((ulong)blockNumber, out ulong? msgIndex))
            {
                return ResultWrapper<Block?>.Fail($"blockNumber {blockNumber} < genesis {GetGenesisBlockNumber()}");
            }
            L1IncomingMessage? l1Message = NitroL2MessageParser.ParseMessageFromTransactions(header, txes);
            if (l1Message is null) return ResultWrapper<Block?>.Fail("Failed to construct L1 message from transactions.");
            MessageWithMetadata messageWithMetadata = new(l1Message, headBlockHeader.Nonce);

            // this step is a bit redundant - we need an alternative method to produce blocks with previously decoded transactions
            ResultWrapper<MessageResult> msgResult;
            Block? block = null;
            try
            {
                block = await ProduceBlockWhileLockedAsync(messageWithMetadata, blockNumber, headBlockHeader);
                msgResult = ResultWrapper<MessageResult>.Success(new MessageResult
                {
                    BlockHash = block!.Hash!,
                    SendRoot = Hash256.Zero
                });
            }
            catch (ArbitrumBlockProductionException e)
            {
                return ResultWrapper<Block?>.Fail(e.Message, e.ErrorCode);
            }
            // if len(receipts) == 0 {
            //     return nil, nil
            // }
            //
            // allTxsErrored := true
            // for _, err := range hooks.TxErrors {
            //     if err == nil {
            //         allTxsErrored = false
            //         break
            //     }
            // }
            // if allTxsErrored {
            //     return nil, nil
            // }

            byte[] blockMetadata = _blockMetadataFromBlock(block!, timeBoostedTxn);

            // TODO: need to figure out on how to implement this functionality - calling back to nitro from nethermind
            // _, err = s.consensus.WriteMessageFromSequencer(msgIdx, msgWithMeta, *msgResult, blockMetadata).Await(s.GetContext())
            // if err != nil {
            //     return nil, err
            // }


            // s.cacheL1PriceDataOfMsg(msgIdx, receipts, block, false)

            return ResultWrapper<Block?>.Success(block);
        }

        private async Task<ResultWrapper<MessageResult>> SequenceDelayedMessageWhileLockedAsync(L1IncomingMessage msgMessage, ulong delayedMsgIdx)
        {
            // if s.syncTillBlock > 0 && s.latestBlock != nil && s.latestBlock.NumberU64() >= s.syncTillBlock {
            //     return nil, ExecutionEngineBlockCreationStopped
            // }

            BlockHeader? headBlockHeader = blockTree.Head?.Header;
            if (headBlockHeader is null)
            {
                return ResultWrapper<MessageResult>.Fail("Could not get head block for sequencing delayed message.");
            }

            if (headBlockHeader.Nonce != delayedMsgIdx)
            {
                return ResultWrapper<MessageResult>.Fail($"Wrong delayed message sequenced. Got {delayedMsgIdx}, expected {headBlockHeader.Nonce}");
            }

            var blockNumber = headBlockHeader.Number + 1;
            if (!GetMessageIndexFromBlockNumber((ulong)blockNumber, out ulong? msgIndex))
            {
                return ResultWrapper<MessageResult>.Fail($"blockNumber {blockNumber} < genesis {GetGenesisBlockNumber()}");
            }

            MessageWithMetadata messageWithMetadata = new(msgMessage, delayedMsgIdx + 1);

            ResultWrapper<MessageResult> msgResult;
            try
            {
                Block? block = await ProduceBlockWhileLockedAsync(messageWithMetadata, headBlockHeader.Number + 1, headBlockHeader);
                msgResult = ResultWrapper<MessageResult>.Success(new MessageResult
                {
                    BlockHash = block!.Hash!,
                    SendRoot = Hash256.Zero
                });
            }
            catch (ArbitrumBlockProductionException e)
            {
                msgResult = ResultWrapper<MessageResult>.Fail(e.Message, e.ErrorCode);
            }

            // TODO: need to figure out on how to implement this functionality - calling back to nitro from nethermind
            // _, err = s.consensus.WriteMessageFromSequencer(msgIdx, messageWithMeta, *msgResult, s.blockMetadataFromBlock(block, nil)).Await(s.GetContext())
            // if err != nil {
            //     return nil, err
            // }


            // s.cacheL1PriceDataOfMsg(msgIdx, receipts, block, true)

            return msgResult;
        }

        private async Task<Block?> ProduceBlockWhileLockedAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
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
                    throw new ArbitrumBlockProductionException("Failed to build block or block has no hash.",
                        ErrorCodes.InternalError);
                }

                if (_logger.IsTrace) _logger.Trace($"Built block: hash={block?.Hash}");
                BlockRemovedEventArgs? resultArgs = await blockProcessedTaskCompletionSource.Task.WaitAsync(TimeSpan.FromSeconds(1));
                if (resultArgs.ProcessingResult == ProcessingResult.Exception)
                {
                    BlockchainException exception = new(
                        resultArgs.Exception?.Message ?? "Block processing threw an unspecified exception.",
                        resultArgs.Exception);
                    if (_logger.IsError) _logger.Error($"Block processing failed for {block?.Hash}", exception);
                    throw new ArbitrumBlockProductionException(exception.Message, ErrorCodes.InternalError);
                }

                return resultArgs.ProcessingResult switch
                {
                    ProcessingResult.Success => block,
                    ProcessingResult.ProcessingError => throw new ArbitrumBlockProductionException(resultArgs.Message ?? "Block processing failed.", ErrorCodes.InternalError),
                    _ => throw new ArbitrumBlockProductionException($"Block processing ended in an unhandled state: {resultArgs.ProcessingResult}", ErrorCodes.InternalError)
                };

            }
            catch (TimeoutException)
            {
                throw new ArbitrumBlockProductionException("Timeout waiting for block processing result.", ErrorCodes.Timeout);
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

        private bool GetMessageIndexFromBlockNumber(ulong blockNumber, [MaybeNullWhen(false)] out ulong? messageIndex)
        {
            ulong genesis = GetGenesisBlockNumber();
            if (blockNumber < genesis)
            {
                messageIndex = null;
                return false;
            }
            messageIndex = blockNumber - genesis;
            return true;
        }

        // blockMetadataFromBlock returns timeboosted byte array which says whether a transaction in the block was timeboosted
        // or not. The first byte of blockMetadata byte array is reserved to indicate the version,
        // starting from the second byte, (N)th bit would represent if (N)th tx is timeboosted or not, 1 means yes and 0 means no
        // blockMetadata[index / 8 + 1] & (1 << (index % 8)) != 0; where index = (N - 1), implies whether (N)th tx in a block is timeboosted
        // note that number of txs in a block will always lag behind (len(blockMetadata) - 1) * 8 but it wont lag more than a value of 7
        private static byte[] _blockMetadataFromBlock(Block block, HashSet<Hash256AsKey>? timeBoostedTxn)
        {
            var txCount = block.Transactions.Length;
            byte[] bits = new byte[1 + (txCount + 7) / 8];
            if (timeBoostedTxn is null || timeBoostedTxn.Count == 0) return bits;

            for (int i = 0; i < txCount; i++)
            {
                Transaction tx = block.Transactions[i];
                if (timeBoostedTxn.Contains(tx.Hash!)) bits[1 + i / 8] |= (byte)(1 << (i % 8));
            }

            return bits;
        }

    }
}

public class MessagesResequencedEventArgs(MessageWithMetadata[] messages) : EventArgs
{
    public MessageWithMetadata[] OldMessages { get; } = messages;
}

public readonly struct ResequenceOperationNotifier;
