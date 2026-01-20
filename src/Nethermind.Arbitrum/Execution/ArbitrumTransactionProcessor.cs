// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Numerics;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Crypto;
using Nethermind.Evm.CodeAnalysis;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing.State;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Stylus;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumTransactionProcessor(
        ITransactionProcessor.IBlobBaseFeeCalculator blobBaseFeeCalculator,
        ISpecProvider specProvider,
        IWorldState worldState,
        IWasmStore wasmStore,
        ArbitrumVirtualMachine virtualMachine,
        IBlockTree blockTree,
        ILogManager logManager,
        ICodeInfoRepository? codeInfoRepository
    ) : TransactionProcessorBase<ArbitrumGasPolicy>(blobBaseFeeCalculator, specProvider, worldState, virtualMachine, codeInfoRepository, logManager)
    {
        public ArbitrumTxExecutionContext TxExecContext => (VirtualMachine as ArbitrumVirtualMachine)!.ArbitrumTxExecutionContext;

        // Token count for the additional fields in calldata:
        // 4*4 - 1 function selector (4 non-zero bytes)
        // 4*24 - 4 fields fit in a uint64 - differ only by padding of 24 zero-bytes each
        // 4*12 + 12 - 1 address field, so has about 12 additional nonzero bytes + 12 zero bytes for padding
        // Total: 172
        // This is not exact since most uint64s also have zeroes, and batch poster may use another function,
        // but it doesn't need to be exact
        private const ulong FloorGasAdditionalTokens = 172;
        private const ulong GasEstimationL1PricePadding = 11_000; // pad estimates by 10%

        private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumTransactionProcessor>();
        private ArbosState? _arbosState;
        private TracingInfo? _tracingInfo;
        private bool _lastExecutionSuccess;
        private IReleaseSpec? _currentSpec;
        private BlockHeader? _currentHeader;
        private ExecutionOptions _currentOpts;
        private readonly IWorldState _worldState = worldState;

        protected override TransactionResult BuyGas(Transaction tx, IReleaseSpec spec, ITxTracer tracer, ExecutionOptions opts,
            in UInt256 effectiveGasPrice, out UInt256 premiumPerGas, out UInt256 senderReservedGasPayment,
            out UInt256 blobBaseFee)
        {
            TransactionResult result = base.BuyGas(tx, spec, tracer, opts, in effectiveGasPrice, out premiumPerGas,
                out senderReservedGasPayment, out blobBaseFee);
            IArbitrumTxTracer arbTracer = tracer.GetTracer<IArbitrumTxTracer>() ?? ArbNullTxTracer.Instance;
            if (result && arbTracer.IsTracingActions)
            {
                arbTracer.CaptureArbitrumTransfer(tx.SenderAddress, null, senderReservedGasPayment, true,
                    BalanceChangeReason.BalanceDecreaseGasBuy);
            }

            return result;
        }

        public override TransactionResult Warmup(Transaction transaction, ITxTracer txTracer) =>
            Execute(transaction, txTracer, ExecutionOptions.SkipValidation);

        protected override TransactionResult Execute(Transaction tx, ITxTracer tracer, ExecutionOptions opts)
        {
            _currentOpts = opts;
            IArbitrumTxTracer arbTracer = tracer.GetTracer<IArbitrumTxTracer>() ?? ArbNullTxTracer.Instance;

            Snapshot snapshot = WorldState.TakeSnapshot();

            InitializeTransactionState(tx, arbTracer);
            ArbitrumTransactionProcessorResult preProcessResult = PreProcessArbitrumTransaction(tx, arbTracer);

            // If not doing any actual EVM
            if (!preProcessResult.ContinueProcessing)
            {
                return FinalizeTransaction(preProcessResult.InnerResult, tx, tracer, snapshot,
                    isPreProcessing: true, preProcessResult.Logs);
            }

            // Store top level tx type used in precompiles
            TxExecContext.TopLevelTxType = (ArbitrumTxType)tx.Type;

            // Don't pass execution options as we don't want to commit / restore at this stage
            TransactionResult evmResult = base.Execute(tx, tracer, ExecutionOptions.None);

            // Post-processing changes the state - run only if EVM execution actually proceeded
            if (evmResult)
            {
                PostProcessArbitrumTransaction(tx);
            }

            // Commit / restore according to options
            return FinalizeTransaction(evmResult, tx, NullTxTracer.Instance, snapshot,
                isPreProcessing: false);
        }

        private void InitializeTransactionState(Transaction tx, IArbitrumTxTracer tracer)
        {
            ExecutionEnvironment executionEnv = ExecutionEnvironment.Rent(CodeInfo.Empty, tx.SenderAddress!, tx.To!, tx.To, 0, tx.Value,
                tx.Value, tx.Data);
            _tracingInfo = new TracingInfo(tracer, TracingScenario.TracingBeforeEvm, executionEnv);
            _arbosState = ArbosState.OpenArbosState(WorldState, new SystemBurner(_tracingInfo, readOnly: false), _logger);
            TxExecContext.Reset();
            ((ArbitrumVirtualMachine)VirtualMachine).L1BlockCache.ClearL1BlockNumberCache();
            _currentHeader = VirtualMachine.BlockExecutionContext.Header;
            _currentSpec = GetSpec(_currentHeader);
        }

        private ArbitrumTransactionProcessorResult PreProcessArbitrumTransaction(Transaction tx,
            IArbitrumTxTracer tracer)
        {
            if (tx is not ArbitrumTransaction arbTx)
                return new(true, TransactionResult.Ok);
            //do internal Arb transaction processing - logic of StartTxHook
            return ProcessArbitrumTransaction(arbTx, in VirtualMachine.BlockExecutionContext, tracer);
        }

        protected override void PayFees(Transaction tx, BlockHeader header, IReleaseSpec spec, ITxTracer tracer,
            in TransactionSubstate substate, long spentGas, in UInt256 premiumPerGas, in UInt256 blobBaseFee,
            int statusCode)
        {
            _lastExecutionSuccess = statusCode == StatusCode.Success;

            UInt256 fees = (UInt256)spentGas * premiumPerGas;

            Address tipRecipient = _arbosState!.NetworkFeeAccount.Get();
            WorldState.AddToBalanceAndCreateIfNotExists(tipRecipient, fees, spec);

            UInt256 effectiveBaseFee = VirtualMachine.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();
            UInt256 eip1559Fees = !tx.IsFree() ? (UInt256)spentGas * effectiveBaseFee : UInt256.Zero;
            UInt256 collectedFees = spec.IsEip1559Enabled ? eip1559Fees : UInt256.Zero;

            if (tx.SupportsBlobs && spec.IsEip4844FeeCollectorEnabled)
            {
                collectedFees += blobBaseFee;
            }

            if (spec.FeeCollector is not null && !collectedFees.IsZero)
            {
                WorldState.AddToBalanceAndCreateIfNotExists(spec.FeeCollector, collectedFees, spec);
            }

            if (tracer.IsTracingFees)
            {
                tracer.ReportFees(fees, eip1559Fees + blobBaseFee);
            }
        }

        protected override TransactionResult CalculateAvailableGas(Transaction tx, in IntrinsicGas<ArbitrumGasPolicy> intrinsicGas, out ArbitrumGasPolicy gasAvailable)
        {
            // Capture intrinsic gas for gas dimension tracers
            if (_tracingInfo?.Tracer.IsTracingGasDimension == true)
            {
                ArbitrumGasPolicy standardGas = intrinsicGas.Standard;
                long calculatedGas = ArbitrumGasPolicy.GetRemainingGas(in standardGas);
                _tracingInfo.Tracer.SetIntrinsicGas(calculatedGas);
            }

            TransactionResult result = GasChargingHook(tx, intrinsicGas.Standard, out gasAvailable);

            // Capture L1 poster gas for txGasDimensionLogger tracer (set by GasChargingHook)
            if (_tracingInfo?.Tracer.IsTracingGasDimension == true)
                _tracingInfo.Tracer.SetPosterGas(TxExecContext.PosterGas);

            return result;
        }

        protected override GasConsumed Refund(Transaction tx, BlockHeader header, IReleaseSpec spec, ExecutionOptions opts,
            in TransactionSubstate substate, in ArbitrumGasPolicy unspentGas, in UInt256 gasPrice, int codeInsertRefunds, ArbitrumGasPolicy floorGas)
        {
            UInt256 effectiveGasPrice = CalculateEffectiveGasPrice(tx, spec.IsEip1559Enabled, header.BaseFeePerGas, out _);

            long spentGas = tx.GasLimit;

            // Override the whole Refund() function only for this line
            // ComputeHoldGas should always be refunded, independently of the tx result (success or failure)
            spentGas -= (long)TxExecContext.ComputeHoldGas;

            long codeInsertRefund = (GasCostOf.NewAccount - GasCostOf.PerAuthBaseCost) * codeInsertRefunds;

            long refund = 0;
            if (!substate.IsError)
            {
                spentGas -= ArbitrumGasPolicy.GetRemainingGas(unspentGas);

                long totalToRefund = codeInsertRefund;
                if (!substate.ShouldRevert)
                    totalToRefund += substate.Refund + substate.DestroyList.Count * RefundOf.Destroy(spec.IsEip3529Enabled);
                refund = CalculateClaimableRefund(spentGas, totalToRefund, spec);

                if (Logger.IsTrace)
                    Logger.Trace("Refunding unused gas of " + unspentGas + " and refund of " + refund);
                spentGas -= refund;
            }
            else if (codeInsertRefund > 0)
            {
                refund = CalculateClaimableRefund(spentGas, codeInsertRefund, spec);

                if (Logger.IsTrace)
                    Logger.Trace("Refunding delegations only: " + refund);
                spentGas -= refund;
            }

            // Capture accumulated MultiGas with refund applied.
            // Use GetTotalAccumulated() to get net gas (accumulated - retained)
            ArbitrumGasPolicy gasWithRefund = unspentGas;
            ArbitrumGasPolicy.ApplyRefund(ref gasWithRefund, (ulong)System.Math.Max(0, refund));
            TxExecContext.AccumulatedMultiGas = gasWithRefund.GetTotalAccumulated();

            long operationGas = spentGas;
            spentGas = System.Math.Max(spentGas, ArbitrumGasPolicy.GetRemainingGas(floorGas));

            // If noValidation we didn't charge for gas, so do not refund
            if (!opts.HasFlag(ExecutionOptions.SkipValidation))
                WorldState.AddToBalance(tx.SenderAddress!, (ulong)(tx.GasLimit - spentGas) * effectiveGasPrice, spec);

            return new GasConsumed(spentGas, operationGas);
        }

        protected override long CalculateClaimableRefund(long spentGas, long totalRefund, IReleaseSpec spec)
        {
            // EVM-incentivized activity like freeing storage should only refund amounts paid to the network address,
            // which represents the overall burden to node operators. A poster's costs, then, should not be eligible
            // for this refund.
            ulong nonRefundable = TxExecContext.PosterGas;

            if (nonRefundable < (ulong)spentGas)
            {
                long maxRefundQuotient = spec.IsEip3529Enabled ?
                    RefundHelper.MaxRefundQuotientEIP3529 : RefundHelper.MaxRefundQuotient;

                return System.Math.Min((spentGas - (long)nonRefundable) / maxRefundQuotient, totalRefund);
            }

            return 0;
        }

        protected override UInt256 CalculateEffectiveGasPrice(Transaction tx, bool eip1559Enabled, in UInt256 baseFee, out UInt256 opcodeGasPrice)
        {
            opcodeGasPrice = tx.CalculateEffectiveGasPrice(eip1559Enabled, in baseFee);

            UInt256 effectiveBaseFee = VirtualMachine.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();
            UInt256 effectiveGasPrice = tx.CalculateEffectiveGasPrice(eip1559Enabled, in effectiveBaseFee);

            // Drop tip if necessary (Arbitrum-specific logic)
            if (ShouldDropTip(VirtualMachine.BlockExecutionContext, _arbosState!.CurrentArbosVersion) && effectiveGasPrice > effectiveBaseFee)
            {
                return effectiveBaseFee;
            }

            return effectiveGasPrice;
        }

        protected override bool TryCalculatePremiumPerGas(Transaction tx, in UInt256 baseFee, out UInt256 premiumPerGas)
        {
            UInt256 effectiveBaseFee = VirtualMachine.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();

            UInt256 effectiveGasPrice = base.CalculateEffectiveGasPrice(tx, _currentSpec!.IsEip1559Enabled, in effectiveBaseFee, out _);

            // We repeat the drop tip logic as in nitro they previously set GasTipCap to 0 if we dropped tip
            // which is then used for effectiveTip (premiumPerGas)
            if (ShouldDropTip(VirtualMachine.BlockExecutionContext, _arbosState!.CurrentArbosVersion) &&
                effectiveGasPrice > effectiveBaseFee)
            {
                premiumPerGas = UInt256.Zero;
                return true;
            }

            return base.TryCalculatePremiumPerGas(tx, in effectiveBaseFee, out premiumPerGas);
        }

        protected override GasConsumed RefundOnFailContractCreation(Transaction tx, BlockHeader header, IReleaseSpec spec, ExecutionOptions opts)
        {
            UInt256 effectiveGasPrice = CalculateEffectiveGasPrice(tx, spec.IsEip1559Enabled, header.BaseFeePerGas, out _);

            long spentGas = tx.GasLimit;

            // ComputeHoldGas should always be refunded, independently of the tx result (success or failure)
            spentGas -= (long)TxExecContext.ComputeHoldGas;

            if (!opts.HasFlag(ExecutionOptions.SkipValidation))
                WorldState.AddToBalance(tx.SenderAddress!, (ulong)(tx.GasLimit - spentGas) * effectiveGasPrice, spec);

            return spentGas;
        }

        private TransactionResult FinalizeTransaction(TransactionResult result, Transaction tx,
            ITxTracer tracer, Snapshot snapshot, bool isPreProcessing, IReadOnlyList<LogEntry>? additionalLogs = null)
        {
            // We don't restore snapshot for failures during preprocessing
            if (!result && !isPreProcessing)
            {
                WorldState.Restore(snapshot);
                TxExecContext.Reset();

                if (_logger.IsTrace)
                    _logger.Trace($"Reverted state for failed Arbitrum transaction {tx.Hash}: {result.ErrorDescription}");
            }

            bool restore = _currentOpts.HasFlag(ExecutionOptions.Restore);
            bool commit = _currentOpts.HasFlag(ExecutionOptions.Commit) ||
                          (!_currentOpts.HasFlag(ExecutionOptions.SkipValidation) && !_currentSpec!.IsEip658Enabled);
            if (commit)
            {
                WorldState.Commit(_currentSpec!, tracer.IsTracingState ? tracer : NullStateTracer.Instance,
                    commitRoots: !_currentSpec!.IsEip658Enabled);
            }
            else if (restore)
            {
                WorldState.Reset(resetBlockChanges: false);
            }

            if (tracer.IsTracingReceipt)
            {
                Hash256? stateRoot = null;
                if (!_currentSpec!.IsEip658Enabled)
                {
                    WorldState.RecalculateStateRoot();
                    stateRoot = WorldState.StateRoot;
                }

                long gasUsed = tx.SpentGas;
                if (tx is ArbitrumTransaction { OverrideSpentGas: not null } arbTx)
                    gasUsed = arbTx.OverrideSpentGas.Value;

                if (result == TransactionResult.Ok)
                {
                    _currentHeader!.GasUsed += gasUsed;
                    tracer.MarkAsSuccess(tx.To!, gasUsed, [], additionalLogs?.ToArray() ?? [], stateRoot);
                }
                else
                    tracer.MarkAsFailed(tx.To!, gasUsed, [], result.ToString(), stateRoot);
            }
            return isPreProcessing ? TransactionResult.Ok : result;
        }

        protected override TransactionResult IncrementNonce(Transaction tx, BlockHeader header, IReleaseSpec spec,
            ITxTracer tracer, ExecutionOptions opts)
        {
            //could achieve the same using ProcessingOptions.DoNotVerifyNonce at BlockProcessing level, but as it doesn't apply to whole block
            //this solution seems cleaner
            if (tx is not ArbitrumTransaction || tx is ArbitrumUnsignedTransaction)
            {
                return base.IncrementNonce(tx, header, spec, tracer, opts);
            }
            else
            {
                //increment without nonce check
                WorldState.IncrementNonce(tx.SenderAddress!);
                return TransactionResult.Ok;
            }
        }

        protected override TransactionResult ValidateSender(Transaction tx, BlockHeader header, IReleaseSpec spec, ITxTracer tracer, ExecutionOptions opts)
        {
            bool validate = !opts.HasFlag(ExecutionOptions.SkipValidation);

            if (tx is ArbitrumTransaction)
            {
                //only ArbitrumUnsigned tx is validated
                validate &= tx is ArbitrumUnsignedTransaction;
            }

            if (validate && WorldState.IsInvalidContractSender(spec, tx.SenderAddress!))
            {
                TraceLogInvalidTx(tx, "SENDER_IS_CONTRACT");
                return TransactionResult.SenderHasDeployedCode;
            }

            return TransactionResult.Ok;
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumTransaction(ArbitrumTransaction tx,
            in BlockExecutionContext blCtx, IArbitrumTxTracer tracer)
        {
            void StartTracer()
            {
                if (tracer.IsTracingActions)
                    tracer.ReportAction(0, tx.Value, tx.SenderAddress!, tx.To!, tx.Data, ExecutionType.CALL);

                ExecutionEnvironment executionEnv = ExecutionEnvironment.Rent(CodeInfo.Empty, tx.SenderAddress!, tx.To!, tx.To, 0, tx.Value,
                    tx.Value, tx.Data);
                _tracingInfo = new TracingInfo(tracer, TracingScenario.TracingDuringEvm, executionEnv);
                _arbosState = ArbosState.OpenArbosState(WorldState, new SystemBurner(_tracingInfo, readOnly: false), _logger);
            }

            try
            {
                switch (tx)
                {
                    case ArbitrumDepositTransaction depositTx:
                        if (depositTx.To is null)
                            return new ArbitrumTransactionProcessorResult(false,
                                TransactionResult.MalformedTransaction);

                        MintBalance(depositTx.SenderAddress, depositTx.Value, _arbosState!, WorldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceIncreaseDeposit);

                        StartTracer();
                        // We intentionally use the variant here that doesn't do tracing (instead of TransferBalance),
                        // because this transfer is represented as the outer eth transaction.
                        Transfer(depositTx.SenderAddress!, depositTx.To, depositTx.Value, WorldState, _currentSpec!);

                        return new ArbitrumTransactionProcessorResult(false, TransactionResult.Ok);

                    case ArbitrumInternalTransaction internalTx:
                        StartTracer();
                        return tx.SenderAddress != ArbosAddresses.ArbosAddress
                            ? new(false, TransactionResult.SenderNotSpecified)
                            : ProcessArbitrumInternalTransaction(internalTx, in blCtx);

                    case ArbitrumSubmitRetryableTransaction retryableTx:
                        StartTracer();
                        return ProcessArbitrumSubmitRetryableTransaction(retryableTx, in blCtx);

                    case ArbitrumRetryTransaction retryTx:
                        return ProcessArbitrumRetryTransaction(retryTx);

                    default:
                        //nothing to processing internally, continue with EVM execution
                        return new ArbitrumTransactionProcessorResult(true, TransactionResult.Ok);
                }
            }
            finally
            {
                if (tx is not ArbitrumRetryTransaction && tracer.IsTracingActions)
                {
                    tracer.ReportActionEnd((long)_arbosState!.BackingStorage.Burner.Burned, Array.Empty<byte>());
                }

                ExecutionEnvironment executionEnv = ExecutionEnvironment.Rent(CodeInfo.Empty, tx.SenderAddress!, tx.To!, tx.To, 0, tx.Value,
                    tx.Value, tx.Data);
                _tracingInfo = new TracingInfo(tracer, TracingScenario.TracingAfterEvm, executionEnv);
                _arbosState = ArbosState.OpenArbosState(WorldState, new SystemBurner(_tracingInfo, readOnly: false), _logger);
            }
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumInternalTransaction(
            ArbitrumInternalTransaction tx,
            in BlockExecutionContext blCtx)
        {
            if (tx.Data.Length < 4)
                return new ArbitrumTransactionProcessorResult(false, TransactionResult.MalformedTransaction);

            ReadOnlyMemory<byte> methodId = tx.Data[..4];

            if (methodId.Span.SequenceEqual(AbiMetadata.StartBlockMethodId))
            {
                ValueHash256 prevHash = ValueKeccak.Zero;
                if (blCtx.Header.Number > 0)
                {
                    // Can't we just do: blCtx.Header.ParentHash ? or else pass my witnessGeneratingHeaderFinder
                    prevHash = blockTree.FindBlockHash(blCtx.Header.Number - 1);
                }

                if (_arbosState!.CurrentArbosVersion >= ArbosVersion.ParentBlockHashSupport)
                {
                }

                Dictionary<string, object> callArguments =
                    AbiMetadata.UnpackInput(AbiMetadata.StartBlockMethod, tx.Data.ToArray());

                ulong l1BlockNumber = (ulong)callArguments["l1BlockNumber"];
                ulong timePassed = (ulong)callArguments["timePassed"];

                if (_arbosState.CurrentArbosVersion < ArbosVersion.Three)
                {
                    // (incorrectly) use the L2 block number instead
                    timePassed = (ulong)callArguments["l2BlockNumber"];
                }

                if (_arbosState.CurrentArbosVersion < ArbosVersion.Eight)
                {
                    // in old versions we incorrectly used an L1 block number one too high
                    l1BlockNumber++;
                }

                ulong oldL1BlockNumber = _arbosState!.Blockhashes.GetL1BlockNumber();

                if (l1BlockNumber > oldL1BlockNumber)
                {
                    _arbosState!.Blockhashes.RecordNewL1Block(l1BlockNumber - 1, prevHash,
                        _arbosState!.CurrentArbosVersion);
                }

                // It's not a mistake, we need to try reaping 2 retryables here
                TryReapOneRetryable(_arbosState!, blCtx.Header.Timestamp, _worldState, _currentSpec!, _tracingInfo);
                TryReapOneRetryable(_arbosState!, blCtx.Header.Timestamp, _worldState, _currentSpec!, _tracingInfo);

                _arbosState!.L2PricingState.UpdatePricingModel(timePassed);

                _arbosState!.UpgradeArbosVersionIfNecessary(blCtx.Header.Timestamp, _worldState, _currentSpec!);
                return new(false, TransactionResult.Ok);
            }

            if (methodId.Span.SequenceEqual(AbiMetadata.BatchPostingReportMethodId))
            {
                Dictionary<string, object> callArguments = AbiMetadata.UnpackInput(AbiMetadata.BatchPostingReport, tx.Data.ToArray());

                UInt256 batchTimestamp = (UInt256)callArguments["batchTimestamp"];
                Address batchPosterAddress = (Address)callArguments["batchPosterAddress"];
                ulong batchDataGas = (ulong)callArguments["batchDataGas"];
                UInt256 l1BaseFeeWei = (UInt256)callArguments["l1BaseFeeWei"];

                if (_arbosState != null)
                {
                    ulong perBatchGas = _arbosState.L1PricingState.PerBatchGasCostStorage.Get();
                    ulong gasSpent = perBatchGas.SaturateAdd(batchDataGas);
                    UInt256 weiSpent = l1BaseFeeWei * gasSpent;

                    ArbosStorageUpdateResult updateResult = _arbosState.L1PricingState.UpdateForBatchPosterSpending((ulong)batchTimestamp,
                        blCtx.Header.Timestamp, batchPosterAddress, (BigInteger)weiSpent, l1BaseFeeWei, _arbosState,
                        _worldState, _currentSpec!, _tracingInfo);

                    if (updateResult != ArbosStorageUpdateResult.Ok)
                    {
                        if (_logger.IsWarn)
                            _logger.Warn($"L1Pricing UpdateForSequencerSpending failed {updateResult}");
                    }
                }
            }

            if (methodId.Span.SequenceEqual(AbiMetadata.BatchPostingReportV2MethodId))
            {
                Dictionary<string, object> callArguments =
                    AbiMetadata.UnpackInput(AbiMetadata.BatchPostingReportV2, tx.Data.ToArray());

                UInt256 batchTimestamp = (UInt256)callArguments["batchTimestamp"];
                Address batchPosterAddress = (Address)callArguments["batchPosterAddress"];
                ulong batchNumber = (ulong)callArguments["batchNumber"];
                ulong batchCallDataLength = (ulong)callArguments["batchCallDataLength"];
                ulong batchCallDataNonZeros = (ulong)callArguments["batchCallDataNonZeros"];
                ulong batchExtraGas = (ulong)callArguments["batchExtraGas"];
                UInt256 l1BaseFeeWei = (UInt256)callArguments["l1BaseFeeWei"];

                if (_arbosState != null)
                {
                    ulong gasSpent = BatchGasCalculator.LegacyCostForStats(batchCallDataLength, batchCallDataNonZeros);
                    gasSpent = gasSpent.SaturateAdd(batchExtraGas);
                    ulong perBatchGas = _arbosState.L1PricingState.PerBatchGasCostStorage.Get();
                    gasSpent = gasSpent.SaturateAdd(perBatchGas);

                    if (_arbosState.CurrentArbosVersion >= ArbosVersion.Fifty)
                    {
                        ulong gasFloorPerToken = _arbosState.L1PricingState.ParentGasFloorPerToken();
                        ulong floorGasSpent = gasFloorPerToken * (batchCallDataLength + batchCallDataNonZeros * 3 + FloorGasAdditionalTokens) + GasCostOf.Transaction;

                        if (floorGasSpent > gasSpent)
                        {
                            gasSpent = floorGasSpent;
                        }
                    }

                    UInt256 weiSpent = l1BaseFeeWei * gasSpent;

                    ArbosStorageUpdateResult updateResult = _arbosState.L1PricingState.UpdateForBatchPosterSpending(
                        (ulong)batchTimestamp,
                        blCtx.Header.Timestamp,
                        batchPosterAddress,
                        (BigInteger)weiSpent,
                        l1BaseFeeWei,
                        _arbosState,
                        _worldState,
                        _currentSpec!,
                        _tracingInfo);

                    if (updateResult != ArbosStorageUpdateResult.Ok && _logger.IsWarn)
                        _logger.Warn($"L1Pricing UpdateForSequencerSpending failed (v2): {updateResult}");
                }
            }

            return new(false, TransactionResult.Ok);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumSubmitRetryableTransaction(
            ArbitrumSubmitRetryableTransaction tx,
            in BlockExecutionContext blCtx)
        {
            List<LogEntry> eventLogs = new(2);

            Address escrowAddress = GetRetryableEscrowAddress(tx.Hash!.ValueHash256);
            Address networkFeeAccount = _arbosState!.NetworkFeeAccount.Get();

            UInt256 availableRefund = tx.DepositValue;
            ConsumeAvailable(ref availableRefund, tx.RetryValue);

            MintBalance(tx.SenderAddress, tx.DepositValue, _arbosState!, _worldState, _currentSpec!,
                _tracingInfo, BalanceChangeReason.BalanceIncreaseDeposit);

            UInt256 balanceAfterMint = _worldState.GetBalance(tx.SenderAddress ?? Address.Zero);
            if (balanceAfterMint < tx.MaxSubmissionFee)
            {
                tx.OverrideSpentGas = 0;
                return new(false, TransactionResult.InsufficientMaxFeePerGasForSenderBalance);
            }

            UInt256 submissionFee =
                CalcRetryableSubmissionFee(tx.RetryData.Length, tx.L1BaseFee);
            if (submissionFee > tx.MaxSubmissionFee)
            {
                tx.OverrideSpentGas = 0;
                return new(false, TransactionResult.InsufficientSenderBalance);
            }

            // collect the submission fee
            TransactionResult tr;
            if ((tr = TransferBalance(tx.SenderAddress, networkFeeAccount, submissionFee, _arbosState!, _worldState,
                    _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceIncreaseNetworkFee)) != TransactionResult.Ok)
            {
                if (Logger.IsError)
                    Logger.Error("Failed to transfer submission fee");
                tx.OverrideSpentGas = 0;
                return new(false, tr);
            }

            UInt256 withheldSubmissionFee = ConsumeAvailable(ref availableRefund, submissionFee);

            // refund excess submission fee
            UInt256 submissionFeeRefund =
                ConsumeAvailable(ref availableRefund, tx.MaxSubmissionFee - submissionFee);
            if (TransferBalance(tx.SenderAddress, tx.FeeRefundAddr!, submissionFeeRefund, _arbosState!,
                    _worldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceChangeTransferRetryableExcessRefund) != TransactionResult.Ok)
            {
                if (Logger.IsError)
                    Logger.Error("Failed to transfer submission fee refund");
            }

            // move the callvalue into escrow
            if ((tr = TransferBalance(tx.SenderAddress, escrowAddress, tx.RetryValue, _arbosState!,
                    _worldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceChangeEscrowTransfer)) != TransactionResult.Ok)
            {
                if (TransferBalance(networkFeeAccount, tx.SenderAddress!, submissionFee, _arbosState!,
                        _worldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceChangeTransferNetworkRefund) != TransactionResult.Ok)
                {
                    if (Logger.IsError)
                        Logger.Error("Failed to refund submissionFee");
                }

                if (TransferBalance(tx.SenderAddress, tx.FeeRefundAddr!, withheldSubmissionFee,
                        _arbosState!,
                        _worldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceChangeTransferRetryableExcessRefund) != TransactionResult.Ok)
                {
                    if (Logger.IsError)
                        Logger.Error("Failed to refund withheld submission fee");
                }

                tx.OverrideSpentGas = 0;
                return new(false, tr);
            }

            ulong time = blCtx.Header.Timestamp;
            ulong timeout = time + Retryable.RetryableLifetimeSeconds;

            Retryable retryable = _arbosState.RetryableState.CreateRetryable(tx.Hash, tx.SenderAddress ?? Address.Zero,
                tx.RetryTo, tx.RetryValue, tx.Beneficiary!, timeout, tx.RetryData.ToArray());

            ulong ticketCreatedGasCost = ArbRetryableTx.TicketCreatedEventGasCost(tx.Hash);
            ArbitrumPrecompileExecutionContext precompileExecutionContext = new(Address.Zero, tx.Value,
                ticketCreatedGasCost, _worldState, wasmStore, blCtx, tx.ChainId ?? 0, _tracingInfo, _currentSpec!);

            ArbRetryableTx.EmitTicketCreatedEvent(precompileExecutionContext, tx.Hash);
            eventLogs.AddRange(precompileExecutionContext.EventLogs);

            UInt256 effectiveBaseFee = blCtx.Header.BaseFeePerGas;
            ulong userGas = (ulong)tx.GasLimit;

            UInt256 maxGasCost = tx.MaxFeePerGas * userGas;
            bool maxFeePerGasTooLow = tx.MaxFeePerGas < effectiveBaseFee;

            UInt256 balance = _worldState.GetBalance(tx.SenderAddress ?? Address.Zero);
            if (balance < maxGasCost || userGas < GasCostOf.Transaction || maxFeePerGasTooLow)
            {
                // User either specified too low of a gas fee cap, didn't have enough balance to pay for gas,
                // or the specified gas limit is below the minimum transaction gas cost.
                // Either way, attempt to refund the gas costs, since we're not doing the auto-redeem.
                UInt256 gasCostRefund = ConsumeAvailable(ref availableRefund, maxGasCost);
                if ((tr = TransferBalance(tx.SenderAddress, tx.FeeRefundAddr, gasCostRefund,
                        _arbosState!, _worldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceChangeTransferRetryableExcessRefund)) !=
                    TransactionResult.Ok)
                {
                    if (Logger.IsError)
                        Logger.Error($"Failed to transfer gasCostRefund {tr}");
                }

                tx.OverrideSpentGas = 0;
                return new(false, TransactionResult.Ok, eventLogs);
            }

            UInt256 gasCost = effectiveBaseFee * userGas;
            UInt256 networkCost = gasCost;
            if (_arbosState!.CurrentArbosVersion >= ArbosVersion.Eleven)
            {
                Address infraFeeAddress = _arbosState!.InfraFeeAccount.Get();
                if (infraFeeAddress != Address.Zero)
                {
                    UInt256 minBaseFee = _arbosState!.L2PricingState.MinBaseFeeWeiStorage.Get();
                    UInt256 infraCost = minBaseFee * effectiveBaseFee;
                    infraCost = ConsumeAvailable(ref networkCost, infraCost);
                    if (TransferBalance(tx.SenderAddress, infraFeeAddress, infraCost, _arbosState!, _worldState,
                            _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceIncreaseInfraFee) != TransactionResult.Ok)
                    {
                        if (Logger.IsError)
                            Logger.Error($"failed to transfer gas cost to infrastructure fee account {tr}");
                        tx.OverrideSpentGas = 0;
                        return new(false, TransactionResult.Ok, eventLogs);
                    }
                }
            }

            if (networkCost > UInt256.Zero)
            {
                if (TransferBalance(tx.SenderAddress, networkFeeAccount, networkCost, _arbosState!, _worldState,
                        _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceIncreaseNetworkFee) != TransactionResult.Ok)
                {
                    if (Logger.IsError)
                        Logger.Error($"Failed to transfer gas cost to network fee account {tr}");
                    tx.OverrideSpentGas = 0;
                    return new(false, TransactionResult.Ok, eventLogs);
                }
            }

            UInt256 withheldGasFunds = ConsumeAvailable(ref availableRefund, gasCost);
            UInt256 gasPriceRefund = (tx.MaxFeePerGas - effectiveBaseFee) * (ulong)tx.GasLimit;

            gasPriceRefund = ConsumeAvailable(ref availableRefund, gasPriceRefund);
            if (TransferBalance(tx.SenderAddress, tx.FeeRefundAddr, gasPriceRefund, _arbosState!,
                    _worldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceChangeTransferRetryableExcessRefund) != TransactionResult.Ok)
            {
                if (Logger.IsError)
                    Logger.Error($"Failed to transfer gasPriceRefund {tr}");
            }

            availableRefund += withheldGasFunds;
            availableRefund += withheldSubmissionFee;

            ArbitrumRetryTransaction outerRetryTx = new ArbitrumRetryTransaction
            {
                ChainId = tx.ChainId ?? 0,
                Nonce = 0,
                SenderAddress = retryable.From.Get(),
                DecodedMaxFeePerGas = effectiveBaseFee,
                GasFeeCap = effectiveBaseFee,
                Gas = userGas,
                GasLimit = (long)userGas,
                To = retryable!.To!.Get(),
                Value = retryable.CallValue.Get(),
                Data = retryable.Calldata.Get(),
                TicketId = tx.Hash,
                RefundTo = tx.FeeRefundAddr,
                MaxRefund = availableRefund,
                SubmissionFeeRefund = submissionFee
            };
            retryable.IncrementNumTries();

            outerRetryTx.Hash = outerRetryTx.CalculateHash();

            ulong redeemScheduledGasCost = ArbRetryableTx.RedeemScheduledEventGasCost(tx.Hash, outerRetryTx.Hash,
                (ulong)outerRetryTx.Nonce, userGas, tx.FeeRefundAddr!, availableRefund, submissionFee);
            precompileExecutionContext = new(Address.Zero, tx.Value,
                redeemScheduledGasCost, _worldState, wasmStore, blCtx, tx.ChainId ?? 0, _tracingInfo, _currentSpec!);

            ArbRetryableTx.EmitRedeemScheduledEvent(precompileExecutionContext, tx.Hash, outerRetryTx.Hash,
                (ulong)outerRetryTx.Nonce, userGas, tx.FeeRefundAddr!, availableRefund, submissionFee);
            eventLogs.AddRange(precompileExecutionContext.EventLogs);

            //TODO Add tracer call
            return new(false, TransactionResult.Ok, eventLogs);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumRetryTransaction(
            ArbitrumRetryTransaction tx)
        {
            Retryable retryable = _arbosState!.RetryableState.OpenRetryable(tx.TicketId,
                VirtualMachine.BlockExecutionContext.Header.Timestamp)!;
            if (retryable is null)
            {
                return new(false, TransactionResult.MalformedTransaction);
            }

            // Transfer callvalue from escrow
            Address escrowAddress = GetRetryableEscrowAddress(tx.TicketId);
            TransactionResult transfer = TransferBalance(escrowAddress, tx.SenderAddress, tx.Value, _arbosState!,
                _worldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceChangeEscrowTransfer);
            if (transfer != TransactionResult.Ok)
            {
                return new(false, transfer);
            }

            // The redeemer has pre-paid for this tx's gas
            UInt256 prepaid = VirtualMachine.BlockExecutionContext.Header.BaseFeePerGas * (ulong)tx.GasLimit;
            TransactionResult mint = TransferBalance(null, tx.SenderAddress, prepaid, _arbosState!, _worldState,
                _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceIncreasePrepaid);
            if (mint != TransactionResult.Ok)
            {
                return new(false, mint);
            }

            TxExecContext.CurrentRetryable = tx.TicketId;
            TxExecContext.CurrentRefundTo = tx.RefundTo;

            return new(true, TransactionResult.Ok);
        }

        private static void TryReapOneRetryable(
            ArbosState arbosState,
            ulong currentTimestamp,
            IWorldState worldState,
            IReleaseSpec releaseSpec,
            TracingInfo? tracingInfo)
        {
            ValueHash256 id = arbosState.RetryableState.TimeoutQueue.Peek();
            if (id == ValueKeccak.Zero)
            {
                // Queue empty
                return;
            }

            Retryable retryable = arbosState.RetryableState.GetRetryable(id);

            ulong timeout = retryable.Timeout.Get();
            if (timeout == 0)
            {
                // Already deleted — pop and return
                _ = arbosState.RetryableState.TimeoutQueue.Pop();
                return;
            }

            ulong windowsLeft = retryable.TimeoutWindowsLeft.Get();
            if (timeout >= currentTimestamp)
            {
                // Not expired yet — return without popping
                return;
            }

            // Expired — pop from queue
            _ = arbosState.RetryableState.TimeoutQueue.Pop();

            if (windowsLeft == 0)
            {
                // Expired — delete it
                DeleteRetryable(id, arbosState, worldState, releaseSpec, tracingInfo);
                return;
            }

            retryable.Timeout.Set(timeout + Retryable.RetryableLifetimeSeconds);
            retryable.TimeoutWindowsLeft.Set(windowsLeft - 1);
        }

        public static bool DeleteRetryable(ValueHash256 id, ArbosState arbosState, IWorldState worldState,
            IReleaseSpec releaseSpec, TracingInfo? tracingInfo)
        {
            Retryable retryable = arbosState.RetryableState.GetRetryable(id);

            if (retryable.Timeout.Get() == 0)
                return false;

            Address escrowAddress = GetRetryableEscrowAddress(id);
            Address beneficiaryAddress = retryable.Beneficiary.Get();
            UInt256 amount = worldState.GetBalance(escrowAddress);

            TransactionResult tr = TransferBalance(escrowAddress, beneficiaryAddress, amount, arbosState, worldState,
                releaseSpec, tracingInfo, BalanceChangeReason.BalanceChangeTransferRetryableExcessRefund);
            if (tr != TransactionResult.Ok)
                return false;

            retryable.Clear();

            return true;
        }

        /// <summary>
        /// Transfer of balance occuring aside from a call
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="amount"></param>
        /// <param name="arbosState"></param>
        /// <param name="worldState"></param>
        /// <param name="releaseSpec"></param>
        /// <param name="tracingInfo"></param>
        /// <param name="reason"></param>
        public static TransactionResult TransferBalance(
            Address? from,
            Address? to,
            UInt256 amount,
            ArbosState arbosState,
            IWorldState worldState,
            IReleaseSpec releaseSpec,
            TracingInfo? tracingInfo,
            BalanceChangeReason reason)
        {
            if (tracingInfo is not null)
            {
                IArbitrumTxTracer tracer = tracingInfo.Tracer;
                TracingScenario scenario = tracingInfo.Scenario;
                if (tracer.IsTracing)
                {
                    if (scenario != TracingScenario.TracingDuringEvm)
                    {
                        tracer.CaptureArbitrumTransfer(from, to, amount,
                            scenario == TracingScenario.TracingBeforeEvm, reason);
                    }
                    else
                    {
                        tracingInfo.MockCall(from ?? Address.Zero, to ?? Address.Zero, amount, 0, []);
                    }
                }
            }

            if (from is not null)
            {
                UInt256 balance = worldState.GetBalance(from);
                if (balance < amount)
                {
                    return TransactionResult.InsufficientSenderBalance;
                }

                if (arbosState.CurrentArbosVersion < ArbosVersion.FixZombieAccounts && amount == UInt256.Zero)
                {
                    worldState.CreateEmptyAccountIfDeleted(from);
                }

                worldState.SubtractFromBalance(from, amount, releaseSpec);
            }

            if (to is not null)
                worldState.AddToBalanceAndCreateIfNotExists(to, amount, releaseSpec);

            return TransactionResult.Ok;
        }

        public static void MintBalance(
            Address? to,
            UInt256 amount,
            ArbosState arbosState,
            IWorldState worldState,
            IReleaseSpec releaseSpec,
            TracingInfo? tracingInfo,
            BalanceChangeReason reason) =>
            TransferBalance(null, to, amount, arbosState, worldState, releaseSpec, tracingInfo, reason);

        private static void Transfer(Address from, Address to, UInt256 amount, IWorldState worldState,
            IReleaseSpec releaseSpec)
        {
            worldState.SubtractFromBalance(from, amount, releaseSpec);
            worldState.AddToBalanceAndCreateIfNotExists(to, amount, releaseSpec);
        }

        public static Address GetRetryableEscrowAddress(ValueHash256 hash)
        {
            byte[] staticBytes = "retryable escrow"u8.ToArray();
            Span<byte> workingSpan = stackalloc byte[staticBytes.Length + Keccak.Size];
            staticBytes.CopyTo(workingSpan);
            hash.Bytes.CopyTo(workingSpan[staticBytes.Length..]);
            return new Address(Keccak.Compute(workingSpan).Bytes[^Address.Size..]);
        }

        private static UInt256 CalcRetryableSubmissionFee(int byteLength, UInt256 l1BaseFee) =>
            l1BaseFee * (1400 + 6 * (uint)byteLength);

        /// <summary>
        /// Reduces available pool by given amount until zero
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="amount"></param>
        /// <returns>Amount consumed from pool</returns>
        private static UInt256 ConsumeAvailable(ref UInt256 pool, UInt256 amount)
        {
            if (amount > pool)
            {
                UInt256 taken = pool;
                pool = UInt256.Zero;
                return taken;
            }

            pool -= amount;
            return amount;
        }

        private static bool ShouldDropTip(BlockExecutionContext blockContext, ulong arbosVersion)
        {
            return arbosVersion != ArbosVersion.Nine ||
                   blockContext.Coinbase != ArbosAddresses.BatchPosterAddress;
        }

        private TransactionResult GasChargingHook(Transaction tx, in ArbitrumGasPolicy intrinsicGas, out ArbitrumGasPolicy gasAvailable)
        {
            // Because a user pays a 1-dimensional gas price, we must re-express poster L1 calldata costs
            // as if the user was buying an equivalent amount of L2 compute gas. This hook determines what
            // that cost looks like, ensuring the user can pay and saving the result for later reference.

            // Use effective base fee for L1 gas calculations (original base fee when NoBaseFee is active)
            UInt256 baseFee = VirtualMachine.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();
            ulong gasLeft = (ulong)(tx.GasLimit - ArbitrumGasPolicy.GetRemainingGas(in intrinsicGas));
            ulong gasNeededToStartEVM = 0;
            Address poster = VirtualMachine.BlockExecutionContext.Coinbase;

            // Never skip L1 charging
            if (baseFee > 0)
            {
                // Since tips go to the network, and not to the poster, we use the basefee.
                // Note, this only determines the amount of gas bought, not the price per gas.

                ulong brotliCompressionLevel = _arbosState!.BrotliCompressionLevel.Get();
                (UInt256 posterCost, ulong calldataUnits) = _arbosState!.L1PricingState.PosterDataCost(
                    tx, poster, brotliCompressionLevel, isTransactionProcessing: true
                );
                if (calldataUnits > 0)
                {
                    _arbosState!.L1PricingState.AddToUnitsSinceUpdate(calldataUnits);
                }

                ulong posterGas = GetPosterGas(_arbosState!, baseFee, posterCost, isGasEstimation: false);
                gasNeededToStartEVM = TxExecContext.PosterGas = posterGas;

                TxExecContext.PosterFee = baseFee * posterGas;
            }

            // the user cannot pay for call data, so give up
            if (gasLeft < gasNeededToStartEVM)
            {
                gasAvailable = new();
                return TransactionResult.GasLimitBelowIntrinsicGas;
            }

            gasLeft -= gasNeededToStartEVM;

            // Skip gas limit enforcement for simulations (eth_call, warmup, tracing)
            if (!_currentOpts.HasFlag(ExecutionOptions.SkipValidation))
            {
                // Limit the amount of compute gas based on gas limits.
                // Before ArbOS 50: cap to block limit. After ArbOS 50: cap to per-tx limit (EIP-7825).
                ulong max = _arbosState!.CurrentArbosVersion < ArbosVersion.Fifty
                    ? _arbosState.L2PricingState.PerBlockGasLimitStorage.Get()
                    : _arbosState.L2PricingState.PerTxGasLimitStorage.Get().SaturateSub((ulong)ArbitrumGasPolicy.GetRemainingGas(intrinsicGas));

                if (gasLeft > max)
                {
                    // Charge extra gas now, refund later
                    TxExecContext.ComputeHoldGas = gasLeft - max;
                    gasLeft = max;
                }
            }

            // Preserve intrinsic gas MultiGas breakdown and add poster gas to L1Calldata.
            // This ensures intrinsic gas (computation, L2 calldata, etc.) plus L1 costs are tracked.
            MultiGas accumulated = intrinsicGas.GetAccumulated();
            if (gasNeededToStartEVM > 0)
                accumulated.Increment(ResourceKind.L1Calldata, gasNeededToStartEVM);
            gasAvailable = ArbitrumGasPolicy.FromLongWithAccumulated((long)gasLeft, in accumulated);
            return TransactionResult.Ok;
        }

        private static ulong GetPosterGas(ArbosState arbosState, UInt256 baseFee, UInt256 posterCost, bool isGasEstimation)
        {
            if (isGasEstimation)
            {
                // Suggest the amount of gas needed for a given amount of ETH is higher in case of congestion.
                // This will help the user pad the total they'll pay in case the price rises a bit.
                // Note, reducing the poster cost will increase share the network fee gets, not reduce the total.

                UInt256 minGasPrice = arbosState.L2PricingState.MinBaseFeeWeiStorage.Get();
                UInt256 adjustedPrice = baseFee * 7 / 8; // assume congestion
                baseFee = UInt256.Max(adjustedPrice, minGasPrice);

                // Pad the L1 cost in case the L1 gas price rises
                posterCost = Utils.UInt256MulByBips(posterCost, GasEstimationL1PricePadding);
            }

            return (posterCost / baseFee).ToULongSafe();
        }

        private record ArbitrumTransactionProcessorResult(
            bool ContinueProcessing,
            TransactionResult InnerResult,
            IReadOnlyList<LogEntry> Logs)
        {
            public ArbitrumTransactionProcessorResult(
                bool ContinueProcessing,
                TransactionResult InnerResult
            ) : this(ContinueProcessing, InnerResult, [])
            {
            }
        }

        private void PostProcessArbitrumTransaction(Transaction tx)
        {
            ulong gasUsed = (ulong)tx.SpentGas;
            ulong gasLeft = (ulong)tx.GasLimit - gasUsed;

            if (gasLeft > (ulong)tx.GasLimit)
            {
                throw new Exception("Tx somehow refunds gas after computation");
            }

            if (tx is ArbitrumRetryTransaction retryTx)
            {
                HandleRetryTransactionEndTxHook(retryTx, gasLeft, gasUsed);
                return;
            }

            HandleNormalTransactionEndTxHook(gasUsed);
        }

        private void HandleRetryTransactionEndTxHook(
            ArbitrumRetryTransaction retryTx,
            ulong gasLeft,
            ulong gasUsed)
        {
            UInt256 effectiveBaseFee = ValidateAndGetEffectiveBaseFee(retryTx);

            UInt256 gasRefund = effectiveBaseFee * gasLeft;
            BurnBalance(retryTx.SenderAddress!, gasRefund, _arbosState!, WorldState, _currentSpec!, _tracingInfo!, BalanceChangeReason.BalanceDecreaseUndoRefund);

            UInt256 maxRefund = retryTx.MaxRefund;
            Address networkFeeAccount = _arbosState!.NetworkFeeAccount.Get();

            HandleSubmissionFeeRefund(retryTx, ref maxRefund, networkFeeAccount, _currentSpec!);

            UInt256 gasCharge = effectiveBaseFee * gasUsed;
            ConsumeAvailable(ref maxRefund, gasCharge);

            HandleGasRefunds(retryTx, effectiveBaseFee, gasLeft, ref maxRefund, networkFeeAccount);

            HandleRetryableLifecycle(retryTx);

            _arbosState!.L2PricingState.AddToGasPool(-gasUsed.ToLongSafe());
        }

        private UInt256 ValidateAndGetEffectiveBaseFee(ArbitrumRetryTransaction retryTx)
        {
            UInt256 effectiveBaseFee = retryTx.GasFeeCap;

            if (_currentOpts.HasFlag(ExecutionOptions.SkipValidation) || effectiveBaseFee == _currentHeader!.BaseFeePerGas)
                return effectiveBaseFee;

            if (_logger.IsError)
                _logger.Error(
                    $"ArbitrumRetryTx GasFeeCap doesn't match basefee in commit mode: gasFeeCap={effectiveBaseFee}, baseFee={_currentHeader!.BaseFeePerGas}");
            // revert to the old behavior to avoid diverging from older nodes
            effectiveBaseFee = _currentHeader!.BaseFeePerGas;

            return effectiveBaseFee;
        }

        public static void BurnBalance(Address fromAddress, UInt256 amount, ArbosState arbosState,
            IWorldState worldState, IReleaseSpec releaseSpec, TracingInfo tracingInfo, BalanceChangeReason reason) =>
            TransferBalance(fromAddress, null, amount, arbosState, worldState, releaseSpec, tracingInfo,
                reason);

        private void HandleSubmissionFeeRefund(ArbitrumRetryTransaction retryTx, ref UInt256 maxRefund, Address networkFeeAccount,
            IReleaseSpec spec)
        {
            if (_lastExecutionSuccess)
            {
                RefundFromAccount(networkFeeAccount, retryTx.SubmissionFeeRefund, ref maxRefund, retryTx, spec, BalanceChangeReason.BalanceChangeTransferNetworkRefund);
            }
            else
            {
                ConsumeAvailable(ref maxRefund, retryTx.SubmissionFeeRefund);
            }
        }

        private void HandleGasRefunds(ArbitrumRetryTransaction retryTx, UInt256 effectiveBaseFee, ulong gasLeft,
            ref UInt256 maxRefund, Address networkFeeAccount)
        {
            UInt256 networkRefund = effectiveBaseFee * gasLeft;

            if (_arbosState!.CurrentArbosVersion >= ArbosVersion.Eleven)
            {
                Address infraFeeAccount = _arbosState!.InfraFeeAccount.Get();
                if (infraFeeAccount != Address.Zero)
                {
                    UInt256 minBaseFee = _arbosState!.L2PricingState.MinBaseFeeWeiStorage.Get();
                    UInt256 infraFee = UInt256.Min(minBaseFee, effectiveBaseFee);
                    UInt256 infraRefund = infraFee * gasLeft;
                    infraRefund = ConsumeAvailable(ref networkRefund, infraRefund);
                    RefundFromAccount(infraFeeAccount, infraRefund, ref maxRefund, retryTx, _currentSpec!, BalanceChangeReason.BalanceChangeTransferInfraRefund);
                }
            }

            RefundFromAccount(networkFeeAccount, networkRefund, ref maxRefund, retryTx, _currentSpec!, BalanceChangeReason.BalanceChangeTransferNetworkRefund);
        }

        private void HandleRetryableLifecycle(ArbitrumRetryTransaction retryTx)
        {
            if (_lastExecutionSuccess)
            {
                DeleteRetryable(retryTx.TicketId, _arbosState!, WorldState, _currentSpec!, _tracingInfo);
                return;
            }

            Address escrowAddress = GetRetryableEscrowAddress(retryTx.TicketId);
            TransactionResult escrowResult = TransferBalance(retryTx.SenderAddress, escrowAddress, retryTx.Value, _arbosState!,
                WorldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceChangeEscrowTransfer);
            if (escrowResult != TransactionResult.Ok)
            {
                throw new Exception($"Failed to return callvalue to escrow: {escrowResult}");
            }
        }

        private void RefundFromAccount(Address refundFrom, UInt256 amount, ref UInt256 maxRefund, ArbitrumRetryTransaction retryTx,
            IReleaseSpec spec, BalanceChangeReason reason)
        {
            // Consume available refund from the max refund pool
            UInt256 toRefundAmount = ConsumeAvailable(ref maxRefund, amount);
            UInt256 remaining = amount - toRefundAmount;

            // Transfer refund to the refund address (if any)
            TransactionResult toRefundResult = TransferBalance(refundFrom, retryTx.RefundTo, toRefundAmount, _arbosState!,
                WorldState, spec, _tracingInfo, reason);
            if (toRefundResult != TransactionResult.Ok)
            {
                if (_logger.IsError)
                    _logger.Error($"Failed to refund {retryTx.RefundTo} from {refundFrom}: {toRefundResult}");
            }

            // Transfer remaining amount to the original sender
            TransactionResult toFromResult = TransferBalance(refundFrom, retryTx.SenderAddress, remaining, _arbosState!,
                WorldState, spec, _tracingInfo, reason);
            if (toFromResult != TransactionResult.Ok)
            {
                if (_logger.IsWarn)
                    _logger.Warn(
                        $"fee address doesn't have enough funds to give user refund: available={WorldState.GetBalance(refundFrom)}, needed={remaining}, address={refundFrom}");
            }
        }

        private void HandleNormalTransactionEndTxHook(ulong gasUsed)
        {
            UInt256 baseFee = VirtualMachine.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();

            // Calculate total transaction cost: price of gas * gas burnt
            // This represents the total amount the user paid for this transaction
            UInt256 totalCost = baseFee * gasUsed;

            // Calculate compute cost: total cost = network's compute + poster's L1 costs
            // The poster fee covers L1 calldata costs, compute cost goes to network operators
            if (UInt256.SubtractUnderflow(totalCost, TxExecContext.PosterFee, out UInt256 computeCost))
            {
                // Give all funds to the network account and continue
                if (_logger.IsInfo)
                    _logger.Info(
                        $"Total cost < poster cost: gasUsed={gasUsed}, baseFee={baseFee}, posterFee={TxExecContext.PosterFee}");
                TxExecContext.PosterFee = UInt256.Zero;
                computeCost = totalCost;
            }

            // Handle infrastructure fees (ArbOS version 5+): extract infra fee from compute cost
            // Infrastructure fees are based on minimum base fee and go to infra fee account
            computeCost = HandleInfrastructureFee(computeCost, gasUsed, baseFee, TxExecContext);
            if (!computeCost.IsZero)
            {
                // Mint remaining compute cost to network fee account
                // This represents the network's share for processing the transaction
                Address networkFeeAccount = _arbosState!.NetworkFeeAccount.Get();
                MintBalance(networkFeeAccount, computeCost, _arbosState!, WorldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceIncreaseNetworkFee);
            }

            // Handle poster fee distribution and L1 fee tracking
            // Poster fees compensate batch posters for L1 calldata costs
            HandlePosterFeeAndL1Tracking(TxExecContext);

            // Update gas pool for computational speed limit enforcement
            // ArbOS's gas pool prevents compute from exceeding per-block limits
            // We don't want to remove poster's L1 costs from the pool as they don't represent processing time
            if (!baseFee.IsZero)
            {
                UpdateGasPool(gasUsed, TxExecContext);
            }
        }

        private UInt256 HandleInfrastructureFee(UInt256 computeCost, ulong gasUsed, UInt256 baseFee,
            ArbitrumTxExecutionContext txContext)
        {
            // Infrastructure fees introduced in ArbOS version 5
            if (_arbosState!.CurrentArbosVersion < ArbosVersion.IntroduceInfraFees)
                return computeCost;

            Address infraFeeAccount = _arbosState!.InfraFeeAccount.Get();
            if (infraFeeAccount == Address.Zero)
                return computeCost;

            // Infrastructure fee is based on minimum base fee (not current base fee)
            // This ensures infrastructure gets a consistent fee even during congestion
            UInt256 minBaseFee = _arbosState!.L2PricingState.MinBaseFeeWeiStorage.Get();
            UInt256 infraFee = UInt256.Min(minBaseFee, baseFee);

            // Only charge infra fee on compute gas (exclude poster gas as it's for L1 costs)
            ulong computeGas = gasUsed > txContext.PosterGas ? gasUsed - txContext.PosterGas : 0;
            UInt256 infraComputeCost = infraFee * computeGas;

            MintBalance(infraFeeAccount, infraComputeCost, _arbosState!, WorldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceIncreaseInfraFee);

            // Subtract infra fee from compute cost (network's share)
            if (UInt256.SubtractUnderflow(computeCost, infraComputeCost, out UInt256 remainingCost))
            {
                if (_logger.IsError)
                    _logger.Error(
                        $"Compute cost < infra compute cost: computeCost={computeCost}, infraComputeCost={infraComputeCost}");
                return UInt256.Zero;
            }

            return remainingCost;
        }

        private void HandlePosterFeeAndL1Tracking(ArbitrumTxExecutionContext txContext)
        {
            Address posterFeeDestination = _arbosState!.CurrentArbosVersion < ArbosVersion.ChangePosterDestination
                ? VirtualMachine.BlockExecutionContext.Coinbase
                : ArbosAddresses.L1PricerFundsPoolAddress;

            MintBalance(posterFeeDestination, txContext.PosterFee, _arbosState!, WorldState, _currentSpec!, _tracingInfo, BalanceChangeReason.BalanceIncreaseL1PosterFee);

            // Track L1 fees available for rewards (ArbOS version 10+)
            if (_arbosState!.CurrentArbosVersion >= ArbosVersion.L1FeesAvailable)
            {
                UpdateL1FeesAvailable(_arbosState!, txContext.PosterFee);
            }
        }

        private void UpdateL1FeesAvailable(ArbosState arbosState, UInt256 posterFee)
        {
            try
            {
                // Add poster fee to L1 fees available pool for future rewards distribution
                // This tracks the total L1 fees collected for staker rewards
                arbosState.L1PricingState.AddToL1FeesAvailable(posterFee);
            }
            catch (Exception ex)
            {
                if (_logger.IsError)
                    _logger.Error($"Failed to update L1FeesAvailable: {ex}");
            }
        }

        private void UpdateGasPool(ulong gasUsed, ArbitrumTxExecutionContext txContext)
        {
            // Calculate compute gas (exclude poster gas as it represents L1 costs, not processing time)
            // Don't include posterGas in computeGas as it doesn't represent processing time
            ulong computeGas = gasUsed > txContext.PosterGas ? gasUsed - txContext.PosterGas : gasUsed;
            if (gasUsed <= txContext.PosterGas)
            {
                // Somehow, the core message transition succeeded, but we didn't burn the posterGas
                // An invariant was violated. To be safe, subtract the entire gas used from the gas pool
                if (_logger.IsError)
                    _logger.Error(
                        $"Total gas used < poster gas component: gasUsed={gasUsed}, posterGas={txContext.PosterGas}");
            }

            // Update gas pool for computational speed limit enforcement
            // This prevents compute from exceeding per-block gas limits
            _arbosState!.L2PricingState.AddToGasPool(-computeGas.ToLongSafe());
        }
    }
}
