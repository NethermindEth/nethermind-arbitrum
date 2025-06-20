// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
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
using Nethermind.State;
using Nethermind.State.Proofs;
using Nethermind.TxPool.Comparison;
using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Math;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using static Nethermind.Consensus.Processing.IBlockProcessor;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumBlockProcessor : BlockProcessor
    {
        protected ISpecProvider _specProvider;
        protected IBlockTransactionsExecutor _blockTransactionsExecutor;
        protected IBlockhashStore _blockhashStore;

        public ArbitrumBlockProcessor(
            ISpecProvider specProvider,
            IBlockValidator blockValidator,
            IRewardCalculator rewardCalculator,
            IBlockTransactionsExecutor blockTransactionsExecutor,
            IWorldState stateProvider,
            IReceiptStorage receiptStorage,
            IBlockhashStore blockhashStore,
            IBeaconBlockRootHandler beaconBlockRootHandler,
            ILogManager logManager,
            IWithdrawalProcessor withdrawalProcessor,
            IExecutionRequestsProcessor executionRequestsProcessor,
            IBlockCachePreWarmer? preWarmer = null)
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
                executionRequestsProcessor,
                preWarmer)
        {
            _specProvider = specProvider;
            _blockTransactionsExecutor = blockTransactionsExecutor;
            _blockhashStore = blockhashStore;
        }

        public class ArbitrumBlockProductionTransactionsExecutor(
            ITransactionProcessor txProcessor,
            IWorldState stateProvider,
            IBlockProductionTransactionPicker txPicker,
            ILogManager logManager)
            : IBlockProductionTransactionsExecutor
        {
            private readonly ITransactionProcessorAdapter _transactionProcessor = new BuildUpTransactionProcessorAdapter(txProcessor);
            private readonly ILogger _logger = logManager.GetClassLogger();

            public ArbitrumBlockProductionTransactionsExecutor(
                IReadOnlyTxProcessingScope readOnlyTxProcessingEnv,
                ISpecProvider specProvider,
                ILogManager logManager,
                long maxTxLengthKilobytes = BlocksConfig.DefaultMaxTxKilobytes)
                : this(
                    readOnlyTxProcessingEnv.TransactionProcessor,
                    readOnlyTxProcessingEnv.WorldState,
                    specProvider,
                    logManager,
                    maxTxLengthKilobytes)
            {
            }

            public ArbitrumBlockProductionTransactionsExecutor(
                ITransactionProcessor transactionProcessor,
                IWorldState stateProvider,
                ISpecProvider specProvider,
                ILogManager logManager,
                long maxTxLengthKilobytes = BlocksConfig.DefaultMaxTxKilobytes) : this(transactionProcessor, stateProvider,
                new BlockProductionTransactionPicker(specProvider, maxTxLengthKilobytes), logManager)
            {
            }

            protected EventHandler<TxProcessedEventArgs>? _transactionProcessed;

            event EventHandler<TxProcessedEventArgs>? IBlockTransactionsExecutor.TransactionProcessed
            {
                add => _transactionProcessed += value;
                remove => _transactionProcessed -= value;
            }

            event EventHandler<AddingTxEventArgs>? IBlockProductionTransactionsExecutor.AddingTransaction
            {
                add => txPicker.AddingTransaction += value;
                remove => txPicker.AddingTransaction -= value;
            }

            public void SetBlockExecutionContext(in BlockExecutionContext blockExecutionContext)
                => _transactionProcessor.SetBlockExecutionContext(in blockExecutionContext);

            public virtual TxReceipt[] ProcessTransactions(Block block, ProcessingOptions processingOptions,
                BlockReceiptsTracer receiptsTracer, IReleaseSpec spec, CancellationToken token = default)
            {
                // We start with high number as don't want to resize too much
                const int defaultTxCount = 512;

                BlockToProduce? blockToProduce = block as BlockToProduce;

                // Don't use blockToProduce.Transactions.Count() as that would fully enumerate which is expensive
                int txCount = blockToProduce is not null ? defaultTxCount : block.Transactions.Length;

                UInt256 expectedBalanceDelta = 0;
                ulong updatedArbosVersion = ArbosVersion.Zero;

                using ArrayPoolList<Transaction> includedTx = new(txCount);

                HashSet<Transaction> consideredTx = new(ByHashTxComparer.Instance);
                int i = 0;

                var redeems = new Queue<Transaction>();
                using var transactionsEnumerator = (blockToProduce?.Transactions ?? block.Transactions).GetEnumerator();
                
                while (true)
                {
                    // Check if we have gone over time or the payload has been requested
                    if (token.IsCancellationRequested) break;

                    if (redeems.Count > 0)
                    {
                        //process redeem
                        var currentRedeem = redeems.Dequeue();

                    } else if (transactionsEnumerator.MoveNext())
                    {
                        Transaction currentTx = transactionsEnumerator.Current;

                        var action = ProcessTransaction(block, currentTx, i++, receiptsTracer, processingOptions, consideredTx);
                        if (action == TxAction.Stop) break;

                        consideredTx.Add(currentTx);
                        if (action == TxAction.Add)
                        {
                            includedTx.Add(currentTx);
                            if (blockToProduce is not null)
                            {
                                //blockToProduce.TxByteLength += currentTx.GetLength();
                            }

                            ulong gasUsed = (ulong)currentTx.SpentGas;

                            var arbTxType = (ArbitrumTxType)currentTx.Type;
                            var currentInnerTx = ((IArbitrumTransaction)currentTx).GetInner();
                            if (arbTxType == ArbitrumTxType.ArbitrumInternal)
                            {
                                ArbosState arbosState =
                                    ArbosState.OpenArbosState(stateProvider, new SystemBurner(true), logManager.GetClassLogger<ArbosState>());

                                var currentInfo = ArbitrumBlockHeaderInfo.Deserialize(blockToProduce.Header, _logger);
                                currentInfo.ArbOSFormatVersion = updatedArbosVersion = arbosState.CurrentArbosVersion;
                                ArbitrumBlockHeaderInfo.UpdateHeader(blockToProduce.Header, currentInfo);
                            }

                            var scheduledTransactions = GetScheduledTransactions(receiptsTracer.LastReceipt);

                            if (updatedArbosVersion >= ArbosVersion.FixRedeemGas)
                            {
                                foreach (var tx in scheduledTransactions)
                                {
                                    var arbTx = (IArbitrumTransaction)tx;
                                    var innerTx = arbTx.GetInner();

                                    gasUsed = gasUsed.SaturateSub(tx.GasLimit < 0 ? 0UL : (ulong)tx.GasLimit);
                                }
                            }

                            switch (arbTxType)
                            {
                                case ArbitrumTxType.ArbitrumDeposit:
                                    expectedBalanceDelta += currentTx.Value;
                                    break;
                                case ArbitrumTxType.ArbitrumSubmitRetryable:
                                    expectedBalanceDelta += (currentInnerTx as ArbitrumSubmitRetryableTx).DepositValue;
                                    break;
                            }

                            //queue any scheduled transactions
                            foreach (var tx in scheduledTransactions)
                            {
                                redeems.Enqueue(tx);
                            }

                            //events - should be in precompiles ?
                            Hash256 L2ToL1TransactionEventID = Hash256.Zero;
                            Hash256 L2ToL1TxEventID = Hash256.Zero;

                            foreach (var log in receiptsTracer.LastReceipt.Logs)
                            {
                                if (log.Address == ArbosAddresses.ArbSysAddress)
                                {
                                    if (log.Topics.Length == 0)
                                        continue;

                                    if (log.Topics[0] == L2ToL1TransactionEventID)
                                    {
                                        //TODO parse topics and adjust expectedBalanceDelta
                                    }
                                    else if (log.Topics[0] == L2ToL1TxEventID)
                                    {
                                        //TODO parse topics and adjust expectedBalanceDelta
                                    }
                                }
                            }
                        }
                    }
                }

                block.Header.TxRoot = TxTrie.CalculateRoot(includedTx.AsSpan());
                if (blockToProduce is not null)
                {
                    blockToProduce.Transactions = includedTx.ToArray();
                }
                return receiptsTracer.TxReceipts.ToArray();
            }

            private TxAction ProcessTransaction(
                Block block,
                Transaction currentTx,
                int index,
                BlockReceiptsTracer receiptsTracer,
                ProcessingOptions processingOptions,
                HashSet<Transaction> transactionsInBlock)
            {
                AddingTxEventArgs args = txPicker.CanAddTransaction(block, currentTx, transactionsInBlock, stateProvider);

                if (args.Action != TxAction.Add)
                {
                    if (_logger.IsDebug) DebugSkipReason(currentTx, args);
                }
                else
                {
                    if (processingOptions.ContainsFlag(ProcessingOptions.DoNotVerifyNonce) && currentTx.SenderAddress != Address.SystemUser)
                    {
                        currentTx.Nonce = stateProvider.GetNonce(currentTx.SenderAddress!);
                    }
                    using ITxTracer tracer = receiptsTracer.StartNewTxTrace(currentTx);
                    TransactionResult result = _transactionProcessor.Execute(currentTx, receiptsTracer);
                    receiptsTracer.EndTxTrace();

                    if (result)
                    {
                        _transactionProcessed?.Invoke(this,
                            new TxProcessedEventArgs(index, currentTx, receiptsTracer.TxReceipts[index]));
                    }
                    else
                    {
                        args.Set(TxAction.Skip, result.Error!);
                    }
                }

                return args.Action;

                [MethodImpl(MethodImplOptions.NoInlining)]
                void DebugSkipReason(Transaction currentTx, AddingTxEventArgs args)
                    => _logger.Debug($"Skipping transaction {currentTx.ToShortString()} because: {args.Reason}.");
            }

            private IEnumerable<Transaction> GetScheduledTransactions(TxReceipt lasTxReceipt)
            {
                if (lasTxReceipt.Logs?.Length == 0)
                {
                    return Array.Empty<Transaction>();
                }

                Hash256 RedeemScheduledEventID = Keccak.OfAnEmptySequenceRlp;

                foreach (var log in lasTxReceipt.Logs)
                {
                    if (log.Address != ArbosAddresses.ArbRetryableTxAddress || log.Topics[0] != RedeemScheduledEventID)
                        continue;

                    //TODO: parse logs into events and create retryable tx
                }
                return Array.Empty<Transaction>();
            }
        }
    }
}
