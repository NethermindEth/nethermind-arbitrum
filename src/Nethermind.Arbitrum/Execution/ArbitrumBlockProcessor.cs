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
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain.Tracing;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumBlockProcessor : BlockProcessor
    {
        protected ISpecProvider _specProvider;
        protected IBlockTransactionsExecutor _blockTransactionsExecutor;
        protected IBlockhashStore _blockhashStore;
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
            _specProvider = specProvider;
            _blockTransactionsExecutor = blockTransactionsExecutor;
            _blockhashStore = blockhashStore;
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
            IArbitrumSpecHelper arbitrumSpecHelper,
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

                ArbosState arbosState =
                    ArbosState.OpenArbosState(stateProvider, new SystemBurner(), logManager.GetClassLogger<ArbosState>());

                BigInteger expectedBalanceDelta = 0;
                ulong updatedArbosVersion = arbosState.CurrentArbosVersion;

                using ArrayPoolList<Transaction> includedTx = new(txCount);

                HashSet<Transaction> consideredTx = new(ByHashTxComparer.Instance);
                int i = 0;

                var redeems = new Queue<Transaction>();
                using IEnumerator<Transaction> transactionsEnumerator = (blockToProduce?.Transactions ?? block.Transactions).GetEnumerator();

                while (true)
                {
                    // Check if we have gone over time or the payload has been requested
                    if (token.IsCancellationRequested)
                        break;

                    //pick up transaction for processing, either retry txn created by submit retryable or transaction from suggested block
                    Transaction? currentTx = null;

                    if (redeems.TryDequeue(out currentTx))
                    {
                        //process redeem
                        if (currentTx is not ArbitrumRetryTransaction retryTxRedeem)
                            continue;

                        var retryable = arbosState.RetryableState.OpenRetryable(retryTxRedeem.TicketId, block.Timestamp);

                        if (retryable == null)
                        {
                            // retryable was already deleted
                            continue;
                        }
                    }
                    else if (transactionsEnumerator.MoveNext())
                    {
                        currentTx = transactionsEnumerator.Current;
                    }

                    if (currentTx is null)
                        break;

                    var action = ProcessTransaction(block, currentTx, i++, receiptsTracer, processingOptions, consideredTx);

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

                        var arbTxType = (ArbitrumTxType)currentTx.Type;
                        if (arbTxType == ArbitrumTxType.ArbitrumInternal)
                        {
                            arbosState = ArbosState.OpenArbosState(stateProvider, new SystemBurner(),
                                logManager.GetClassLogger<ArbosState>());

                            var currentInfo = ArbitrumBlockHeaderInfo.Deserialize(blockToProduce.Header, _logger);
                            currentInfo.ArbOSFormatVersion = updatedArbosVersion = arbosState.CurrentArbosVersion;
                            ArbitrumBlockHeaderInfo.UpdateHeader(blockToProduce.Header, currentInfo);
                        }

                        var txGasUsed = currentTx.SpentGas;
                        if (currentTx is ArbitrumTransaction { OverrideSpentGas: not null } arbTx)
                            txGasUsed = arbTx.OverrideSpentGas.Value;

                        //only pickup scheduled transactions when producing block - otherwise already included in block
                        IEnumerable<Transaction> scheduledTransactions = [];
                        if (processingOptions.ContainsFlag(ProcessingOptions.ProducingBlock))
                        {
                            scheduledTransactions = receiptsTracer.TxReceipts.Count > 0
                                ? GetScheduledTransactions(arbosState, receiptsTracer.LastReceipt, block.Header, specProvider.ChainId)
                                : [];
                        }

                        if (updatedArbosVersion >= ArbosVersion.FixRedeemGas)
                        {
                            foreach (var tx in scheduledTransactions)
                            {
                                if ((ArbitrumTxType)tx.Type != ArbitrumTxType.ArbitrumRetry)
                                {
                                    if (_logger.IsWarn)
                                        _logger.Warn($"Unexpected type of scheduled tx {(ArbitrumTxType)tx.Type}");
                                    continue;
                                }

                                txGasUsed = txGasUsed.SaturateSub(tx.GasLimit < 0 ? 0 : tx.GasLimit);
                            }
                        }

                        switch (arbTxType)
                        {
                            case ArbitrumTxType.ArbitrumDeposit:
                                expectedBalanceDelta += (BigInteger)currentTx.Value;
                                break;
                            case ArbitrumTxType.ArbitrumSubmitRetryable:
                                if (currentTx is ArbitrumSubmitRetryableTransaction submitRetryableTx)
                                {
                                    expectedBalanceDelta += (BigInteger)submitRetryableTx.DepositValue;
                                }
                                break;
                        }

                        //queue any scheduled transactions
                        foreach (Transaction tx in scheduledTransactions)
                        {
                            redeems.Enqueue(tx);
                        }

                        var l2ToL1TransactionEventId = ArbSys.L2ToL1TransactionEvent.GetHash();
                        var l2ToL1TxEventId = ArbSys.L2ToL1TxEvent.GetHash();

                        foreach (LogEntry log in receiptsTracer.LastReceipt.Logs)
                        {
                            if (log.Address == ArbosAddresses.ArbSysAddress)
                            {
                                if (log.Topics.Length == 0)
                                    continue;

                                if (log.Topics[0] == l2ToL1TransactionEventId)
                                {
                                    ArbSys.ArbSysL2ToL1Transaction eventData = ArbSys.DecodeL2ToL1TransactionEvent(log);
                                    expectedBalanceDelta -= (BigInteger)eventData.CallValue;
                                }
                                else if (log.Topics[0] == l2ToL1TxEventId)
                                {
                                    ArbSys.ArbSysL2ToL1Tx eventData = ArbSys.DecodeL2ToL1TxEvent(log);
                                    expectedBalanceDelta -= (BigInteger)eventData.CallValue;
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

                UpdateArbitrumBlockHeader(block.Header, stateProvider);

                // TODO: nitro's balanceDelta & expectedBalanceDelta comparison
                // might be a different PR because it seems to be a bit big?
                // does not seem to affect block 552 issue

                WasmStore.Instance.Commit();
                WasmStore.Instance.GetRecentWasms().Clear();

                return receiptsTracer.TxReceipts.ToArray();
            }

            private void UpdateArbitrumBlockHeader(BlockHeader header, IWorldState stateProvider)
            {
                ArbosState arbosState =
                    ArbosState.OpenArbosState(stateProvider, new SystemBurner(), logManager.GetClassLogger<ArbosState>());

                ChainConfig chainConfig = GetChainConfig(arbosState);

                ArbitrumBlockHeaderInfo arbBlockHeaderInfo = new()
                {
                    SendRoot = Hash256.Zero,
                    SendCount = 0,
                    L1BlockNumber = 0,
                    ArbOSFormatVersion = 0
                };

                if ((ulong)header.Number < chainConfig.ArbitrumChainParams.GenesisBlockNum)
                {
                    throw new InvalidOperationException("Cannot finalize blocks before genesis");
                }

                if ((ulong)header.Number == chainConfig.ArbitrumChainParams.GenesisBlockNum)
                {
                    arbBlockHeaderInfo.ArbOSFormatVersion = chainConfig.ArbitrumChainParams.InitialArbOSVersion;
                }
                else
                {
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
                HashSet<Transaction> transactionsInBlock)
            {
                AddingTxEventArgs args = txPicker.CanAddTransaction(block, currentTx, transactionsInBlock, stateProvider);

                if (args.Action != TxAction.Add)
                {
                    if (_logger.IsDebug)
                        DebugSkipReason(currentTx, args);
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
                        _transactionProcessedHandler?.OnTransactionProcessed(new TxProcessedEventArgs(index, currentTx, block.Header, receiptsTracer.TxReceipts[index]));
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

            private IEnumerable<Transaction> GetScheduledTransactions(ArbosState arbosState, TxReceipt lastTxReceipt, BlockHeader header, ulong chainId)
            {
                if ((lastTxReceipt.Logs?.Length ?? 0) == 0)
                {
                    return Array.Empty<Transaction>();
                }

                var redeemScheduledEventId = Precompiles.ArbRetryableTx.RedeemScheduledEvent.GetHash();

                var addedTransactions = new List<Transaction>();

                foreach (var log in lastTxReceipt.Logs)
                {
                    if (log.Address != ArbosAddresses.ArbRetryableTxAddress || log.Topics.Length == 0 || log.Topics[0] != redeemScheduledEventId)
                        continue;

                    var eventData = Precompiles.ArbRetryableTx.DecodeRedeemScheduledEvent(log);
                    var retryableState = arbosState.RetryableState.OpenRetryable(eventData.TicketId, header.Timestamp);
                    if (retryableState is null)
                        continue;

                    ArbitrumRetryTransaction transaction = new ArbitrumRetryTransaction
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

            private ChainConfig GetChainConfig(ArbosState arbosState)
            {
                // Try to get from ArbOS state first (v11+ when set via SetChainConfig)
                if (arbosState.CurrentArbosVersion >= ArbosVersion.Eleven)
                {
                    byte[] serializedConfig = arbosState.ChainConfigStorage.Get();
                    if (serializedConfig?.Length > 0)
                    {
                        try
                        {
                            ChainConfig? stateConfig = JsonSerializer.Deserialize<ChainConfig>(serializedConfig);
                            if (stateConfig?.ArbitrumChainParams != null)
                            {
                                return stateConfig;
                            }
                        }
                        catch (JsonException ex)
                        {
                            if (_logger.IsWarn)
                                _logger.Warn($"Failed to deserialize chain config from ArbOS state: {ex.Message}. Falling back to chainspec.");
                        }
                    }
                }

                // Fallback to chainspec (source of truth for syncing or pre-v11)
                return GetChainConfigFromChainSpec();
            }

            private ChainConfig GetChainConfigFromChainSpec()
            {
                return new ChainConfig
                {
                    ChainId = specProvider.ChainId,
                    ArbitrumChainParams = new ArbitrumChainParams
                    {
                        GenesisBlockNum = arbitrumSpecHelper.GenesisBlockNum,
                        InitialArbOSVersion = arbitrumSpecHelper.InitialArbOSVersion,
                        InitialChainOwner = arbitrumSpecHelper.InitialChainOwner,
                        Enabled = arbitrumSpecHelper.Enabled,
                        AllowDebugPrecompiles = arbitrumSpecHelper.AllowDebugPrecompiles,
                        DataAvailabilityCommittee = arbitrumSpecHelper.DataAvailabilityCommittee,
                        MaxCodeSize = arbitrumSpecHelper.MaxCodeSize,
                        MaxInitCodeSize = arbitrumSpecHelper.MaxInitCodeSize
                    }
                };
            }
        }
    }
}
