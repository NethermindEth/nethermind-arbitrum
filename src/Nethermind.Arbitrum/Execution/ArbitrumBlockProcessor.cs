// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State.Proofs;
using Nethermind.TxPool.Comparison;
using System.Runtime.CompilerServices;
using Nethermind.Crypto;
using static Nethermind.Consensus.Processing.IBlockProcessor;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.Execution.Receipts;
using System.Numerics;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain.Tracing;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumBlockProcessor : BlockProcessor
    {
        private readonly CachedL1PriceData _cachedL1PriceData;

        public ArbitrumBlockProcessor(
            ISpecProvider specProvider,
            IBlockValidator blockValidator,
            IRewardCalculator rewardCalculator,
            IBlockTransactionsExecutor blockTransactionsExecutor,
            ITransactionProcessor txProcessor,
            CachedL1PriceData cachedL1PriceData,
            IWorldState stateProvider,
            IReceiptStorage receiptStorage,
            IBlockhashStore blockhashStore,
            IBeaconBlockRootHandler beaconBlockRootHandler,
            ILogManager logManager,
            IWithdrawalProcessor withdrawalProcessor,
            IExecutionRequestsProcessor executionRequestsProcessor)
            : base(
                specProvider,
                blockValidator,
                rewardCalculator,
                blockTransactionsExecutor,
                stateProvider,
                receiptStorage,
                beaconBlockRootHandler,
                blockhashStore,
                logManager,
                withdrawalProcessor,
                executionRequestsProcessor)
        {
            _cachedL1PriceData = cachedL1PriceData;
            ReceiptsTracer = new ArbitrumBlockReceiptTracer((txProcessor as ArbitrumTransactionProcessor)!.TxExecContext);
        }

        protected override TxReceipt[] ProcessBlock(
            Block block,
            IBlockTracer blockTracer,
            ProcessingOptions options,
            IReleaseSpec releaseSpec,
            CancellationToken token)
        {
            TxReceipt[] receipts = base.ProcessBlock(block, blockTracer, options, releaseSpec, token);
            _cachedL1PriceData.CacheL1PriceDataOfMsg(
                (ulong)block.Number, receipts, block, blockBuiltUsingDelayedMessage: false
            );
            return receipts;
        }

        public class ArbitrumBlockProductionTransactionsExecutor(
            ITransactionProcessor txProcessor,
            IWorldState stateProvider,
            IBlockProductionTransactionPicker txPicker,
            ILogManager logManager,
            ISpecProvider specProvider,
            ArbitrumChainSpecEngineParameters chainSpecParams,
            BlockValidationTransactionsExecutor.ITransactionProcessedEventHandler? transactionProcessedHandler = null)
            : IBlockProductionTransactionsExecutor
        {
            private readonly ITransactionProcessorAdapter _transactionProcessor = new BuildUpTransactionProcessorAdapter(txProcessor);
            private readonly ILogger _logger = logManager.GetClassLogger();
            private BlockValidationTransactionsExecutor.ITransactionProcessedEventHandler? _transactionProcessedHandler = transactionProcessedHandler;

            event EventHandler<AddingTxEventArgs>? IBlockProductionTransactionsExecutor.AddingTransaction
            {
                add => txPicker.AddingTransaction += value;
                remove => txPicker.AddingTransaction -= value;
            }

            public void SetBlockExecutionContext(in BlockExecutionContext blockExecutionContext)
                => _transactionProcessor.SetBlockExecutionContext(in blockExecutionContext);

            public virtual TxReceipt[] ProcessTransactions(Block block, ProcessingOptions processingOptions,
                BlockReceiptsTracer receiptsTracer, CancellationToken token = default)
            {
                // We start with high number as don't want to resize too much
                const int defaultTxCount = 512;

                BlockToProduce? blockToProduce = block as BlockToProduce;

                // Don't use blockToProduce.Transactions.Count() as that would fully enumerate which is expensive
                int txCount = blockToProduce is not null ? defaultTxCount : block.Transactions.Length;

                ArbosState arbosState = ArbosState.OpenArbosState(stateProvider, new SystemBurner(), logManager.GetClassLogger<ArbosState>());

                ulong blockGasLeft = arbosState.L2PricingState.PerBlockGasLimitStorage.Get();
                ulong updatedArbosVersion = arbosState.CurrentArbosVersion;
                BigInteger expectedBalanceDelta = 0;

                using ArrayPoolList<Transaction> includedTx = new(txCount);

                HashSet<Transaction> consideredTx = new(ByHashTxComparer.Instance);
                Queue<Transaction> scheduledRedeems = new();
                int processedCount = 0;

                using IEnumerator<Transaction> transactionsEnumerator = (blockToProduce?.Transactions ?? block.Transactions).GetEnumerator();

                while (!token.IsCancellationRequested)
                {
                    // Get next transaction to process (either scheduled redeem by submit retryable or from suggested block)
                    Transaction? currentTx = TryGetNextTransaction(scheduledRedeems, transactionsEnumerator, arbosState, block.Timestamp);
                    if (currentTx is null)
                        break;

                    TxAction action = ProcessTransaction(block, currentTx, processedCount++, receiptsTracer, processingOptions, consideredTx, blockGasLeft);
                    if (action == TxAction.Stop)
                        break;

                    consideredTx.Add(currentTx);
                    if (action == TxAction.Add)
                    {
                        includedTx.Add(currentTx);
                        if (blockToProduce is not null)
                        {
                            //TODO requires change to visibility in NM
                            //blockToProduce.TxByteLength += currentTx.GetLength();
                        }

                        ArbitrumTxType arbTxType = (ArbitrumTxType)currentTx.Type;

                        if (arbTxType == ArbitrumTxType.ArbitrumInternal && blockToProduce is not null)
                        {
                            arbosState = ArbosState.OpenArbosState(stateProvider, new SystemBurner(), logManager.GetClassLogger<ArbosState>());
                            updatedArbosVersion = arbosState.CurrentArbosVersion;

                            ArbitrumBlockHeaderInfo currentInfo = ArbitrumBlockHeaderInfo.Deserialize(blockToProduce.Header, _logger);
                            currentInfo.ArbOSFormatVersion = updatedArbosVersion;
                            ArbitrumBlockHeaderInfo.UpdateHeader(blockToProduce.Header, currentInfo);
                        }

                        long txGasUsed = currentTx is ArbitrumTransaction { OverrideSpentGas: not null } arbTx
                            ? arbTx.OverrideSpentGas.Value
                            : currentTx.SpentGas;

                        long dataGas = receiptsTracer.LastReceipt is ArbitrumTxReceipt arbReceipt
                            ? (long)arbReceipt.GasUsedForL1
                            : 0;

                        //only pickup scheduled transactions when producing block - otherwise already included in block
                        IEnumerable<Transaction> scheduledTransactions;
                        if (blockToProduce is not null && receiptsTracer.TxReceipts.Count > 0)
                        {
                            scheduledTransactions = GetScheduledTransactions(arbosState, receiptsTracer.LastReceipt, block.Header, specProvider.ChainId);

                            // Adjust gas used for scheduled redeems (if ArbOS version supports it)
                            IEnumerable<Transaction> transactions = scheduledTransactions.ToList();
                            if (updatedArbosVersion >= ArbosVersion.FixRedeemGas)
                            {
                                txGasUsed = AdjustGasForScheduledRedeems(txGasUsed, transactions);
                            }

                            // Queue scheduled transactions for processing
                            foreach (Transaction tx in transactions)
                            {
                                scheduledRedeems.Enqueue(tx);
                            }
                        }

                        // Update block gas limit
                        blockGasLeft = CalculateAndUpdateBlockGasLimit(txGasUsed, dataGas, blockGasLeft);

                        // Track balance changes from deposits
                        expectedBalanceDelta += GetBalanceChange(currentTx);

                        // Track balance changes from L2->L1 messages
                        expectedBalanceDelta -= GetL2ToL1MessageValue(receiptsTracer.LastReceipt);
                    }
                }

                block.Header.TxRoot = TxTrie.CalculateRoot(includedTx.AsSpan());
                if (blockToProduce is not null)
                {
                    blockToProduce.Transactions = includedTx.ToArray();
                }

                UpdateArbitrumBlockHeader(block.Header, stateProvider);

                // TODO: nitro's balanceDelta & expectedBalanceDelta comparison
                // might be a different PR because it seems to be a bit big?
                // does not seem to affect block 552 issue

                WasmStore.Instance.Commit();
                WasmStore.Instance.GetRecentWasms().Clear();

                return receiptsTracer.TxReceipts.ToArray();
            }

            private static Transaction? TryGetNextTransaction(
                Queue<Transaction> scheduledRedeems,
                IEnumerator<Transaction> transactionsEnumerator,
                ArbosState arbosState,
                ulong blockTimestamp)
            {
                while (scheduledRedeems.TryDequeue(out Transaction? redeem))
                {
                    if (redeem is ArbitrumRetryTransaction retryTx)
                    {
                        Retryable? retryable = arbosState.RetryableState.OpenRetryable(retryTx.TicketId, blockTimestamp);
                        if (retryable != null)
                            return redeem;
                    }
                }

                return transactionsEnumerator.MoveNext() ? transactionsEnumerator.Current : null;
            }

            private static bool IsUserTransaction(Transaction tx)
            {
                return (ArbitrumTxType)tx.Type switch
                {
                    ArbitrumTxType.ArbitrumInternal => false,
                    ArbitrumTxType.ArbitrumRetry => false,
                    _ => true
                };
            }

            private static BigInteger GetBalanceChange(Transaction tx)
            {
                return (ArbitrumTxType)tx.Type switch
                {
                    ArbitrumTxType.ArbitrumDeposit => (BigInteger)tx.Value,
                    ArbitrumTxType.ArbitrumSubmitRetryable when tx is ArbitrumSubmitRetryableTransaction submitRetryable
                        => (BigInteger)submitRetryable.DepositValue,
                    _ => 0
                };
            }

            private static BigInteger GetL2ToL1MessageValue(TxReceipt receipt)
            {
                Hash256 l2ToL1TransactionEventId = ArbSys.L2ToL1TransactionEvent.GetHash();
                Hash256 l2ToL1TxEventId = ArbSys.L2ToL1TxEvent.GetHash();
                BigInteger totalValue = 0;

                foreach (LogEntry log in receipt.Logs)
                {
                    if (log.Address != ArbosAddresses.ArbSysAddress || log.Topics.Length == 0)
                        continue;

                    if (log.Topics[0] == l2ToL1TransactionEventId)
                    {
                        ArbSys.ArbSysL2ToL1Transaction eventData = ArbSys.DecodeL2ToL1TransactionEvent(log);
                        totalValue += (BigInteger)eventData.CallValue;
                    }
                    else if (log.Topics[0] == l2ToL1TxEventId)
                    {
                        ArbSys.ArbSysL2ToL1Tx eventData = ArbSys.DecodeL2ToL1TxEvent(log);
                        totalValue += (BigInteger)eventData.CallValue;
                    }
                }

                return totalValue;
            }

            private long AdjustGasForScheduledRedeems(long txGasUsed, IEnumerable<Transaction> scheduledTransactions)
            {
                foreach (Transaction tx in scheduledTransactions)
                {
                    if ((ArbitrumTxType)tx.Type != ArbitrumTxType.ArbitrumRetry)
                    {
                        if (_logger.IsWarn)
                            _logger.Warn($"Unexpected type of scheduled tx {(ArbitrumTxType)tx.Type}");
                        continue;
                    }

                    txGasUsed = txGasUsed.SaturateSub(tx.GasLimit < 0 ? 0 : tx.GasLimit);
                }
                return txGasUsed;
            }

            private ulong CalculateAndUpdateBlockGasLimit(long txGasUsed, long dataGas, ulong blockGasLeft)
            {
                long computeUsed = System.Math.Max(0, txGasUsed - dataGas);

                if (computeUsed < GasCostOf.Transaction)
                {
                    computeUsed = GasCostOf.Transaction;
                }

                return System.Math.Max(0, blockGasLeft - (ulong)computeUsed);
            }

            private void UpdateArbitrumBlockHeader(BlockHeader header, IWorldState stateProvider)
            {
                ArbosState arbosState =
                    ArbosState.OpenArbosState(stateProvider, new SystemBurner(), logManager.GetClassLogger<ArbosState>());

                if ((ulong)header.Number < chainSpecParams.GenesisBlockNum)
                {
                    throw new InvalidOperationException("Cannot finalize blocks before genesis");
                }

                ArbitrumBlockHeaderInfo arbBlockHeaderInfo = new()
                {
                    SendRoot = Hash256.Zero,
                    SendCount = 0,
                    L1BlockNumber = 0,
                    ArbOSFormatVersion = 0
                };

                if ((ulong)header.Number == chainSpecParams.GenesisBlockNum)
                {
                    arbBlockHeaderInfo.ArbOSFormatVersion = (ulong)chainSpecParams.InitialArbOSVersion!;
                }
                else
                {
                    // Add outbox info to the header for client-side proving
                    arbBlockHeaderInfo.SendRoot = arbosState.SendMerkleAccumulator.CalculateRoot().ToCommitment();
                    arbBlockHeaderInfo.SendCount = arbosState.SendMerkleAccumulator.GetSize();
                    arbBlockHeaderInfo.L1BlockNumber = arbosState.Blockhashes.GetL1BlockNumber();
                    arbBlockHeaderInfo.ArbOSFormatVersion = arbosState.CurrentArbosVersion;
                }
                ArbitrumBlockHeaderInfo.UpdateHeader(header, arbBlockHeaderInfo);
            }

            private TxAction ProcessTransaction(
                Block block,
                Transaction currentTx,
                int index,
                BlockReceiptsTracer receiptsTracer,
                ProcessingOptions processingOptions,
                HashSet<Transaction> transactionsInBlock,
                ulong? blockGasLeft = null)
            {
                AddingTxEventArgs args = CanAddTransaction(
                    block, currentTx, transactionsInBlock, blockGasLeft);

                if (args.Action != TxAction.Add)
                {
                    if (_logger.IsDebug)
                        DebugSkipReason(currentTx, args);
                }
                else
                {
                    if (processingOptions.ContainsFlag(ProcessingOptions.LoadNonceFromState) && currentTx.SenderAddress != Address.SystemUser)
                    {
                        currentTx.Nonce = stateProvider.GetNonce(currentTx.SenderAddress!);
                    }
                    using ITxTracer tracer = receiptsTracer.StartNewTxTrace(currentTx);
                    TransactionResult result = _transactionProcessor.Execute(currentTx, receiptsTracer);
                    receiptsTracer.EndTxTrace();

                    if (result)
                    {
                        _transactionProcessedHandler?.OnTransactionProcessed(new TxProcessedEventArgs(index, currentTx, block.Header, receiptsTracer.TxReceipts[index]));
                    }
                    else
                    {
                        args.Set(TxAction.Skip, result.ErrorDescription);
                    }
                }

                return args.Action;

                [MethodImpl(MethodImplOptions.NoInlining)]
                void DebugSkipReason(Transaction currentTx, AddingTxEventArgs args)
                    => _logger.Debug($"Skipping transaction {currentTx.ToShortString()} because: {args.Reason}.");
            }

            private AddingTxEventArgs CanAddTransaction(
                Block block,
                Transaction currentTx,
                IReadOnlySet<Transaction> transactionsInBlock,
                ulong? blockGasLeft)
            {
                if (blockGasLeft.HasValue && IsUserTransaction(currentTx) && (ulong)currentTx.GasLimit > blockGasLeft.Value)
                {
                    AddingTxEventArgs args = new(transactionsInBlock.Count, currentTx, block, transactionsInBlock);
                    return args.Set(TxAction.Skip, TransactionResult.BlockGasLimitExceeded.ErrorDescription);
                }

                return txPicker.CanAddTransaction(block, currentTx, transactionsInBlock, stateProvider);
            }

            private IEnumerable<Transaction> GetScheduledTransactions(ArbosState arbosState, TxReceipt lastTxReceipt, BlockHeader header, ulong chainId)
            {
                if ((lastTxReceipt.Logs?.Length ?? 0) == 0)
                {
                    return Array.Empty<Transaction>();
                }

                var redeemScheduledEventId = ArbRetryableTx.RedeemScheduledEvent.GetHash();

                var addedTransactions = new List<Transaction>();

                foreach (var log in lastTxReceipt.Logs)
                {
                    if (log.Address != ArbosAddresses.ArbRetryableTxAddress || log.Topics.Length == 0 || log.Topics[0] != redeemScheduledEventId)
                        continue;

                    var eventData = ArbRetryableTx.DecodeRedeemScheduledEvent(log);
                    var retryableState = arbosState.RetryableState.OpenRetryable(eventData.TicketId, header.Timestamp);
                    if (retryableState is null)
                        continue;

                    ArbitrumRetryTransaction transaction = new()
                    {
                        ChainId = chainId,
                        Nonce = eventData.SequenceNum,
                        SenderAddress = retryableState.From.Get(),
                        DecodedMaxFeePerGas = header.BaseFeePerGas,
                        GasFeeCap = header.BaseFeePerGas,
                        Gas = eventData.DonatedGas,
                        GasLimit = eventData.DonatedGas.ToLongSafe(),
                        To = retryableState.To?.Get(),
                        Value = retryableState.CallValue.Get(),
                        Data = retryableState.Calldata.Get(),
                        TicketId = eventData.TicketId.ToCommitment(),
                        RefundTo = eventData.GasDonor,
                        MaxRefund = eventData.MaxRefund,
                        SubmissionFeeRefund = eventData.SubmissionFeeRefund,
                        Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                    };

                    transaction.Hash = transaction.CalculateHash();
                    addedTransactions.Add(transaction);
                }

                return addedTransactions;
            }
        }
    }
}
