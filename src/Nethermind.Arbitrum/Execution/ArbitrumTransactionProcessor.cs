// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.State.Tracing;
using Nethermind.Crypto;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumTransactionProcessor(
        ISpecProvider specProvider,
        IWorldState worldState,
        IVirtualMachine virtualMachine,
        IBlockTree blockTree,
        ILogManager logManager,
        ICodeInfoRepository? codeInfoRepository
    ) : TransactionProcessorBase(specProvider, worldState, virtualMachine, new ArbitrumCodeInfoRepository(codeInfoRepository), logManager)
    {
        private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumTransactionProcessor>();
        private ArbosState? _arbosState;
        private bool _lastExecutionSuccess;
        private IReleaseSpec? _currentSpec;
        private BlockHeader? _currentHeader;
        private ExecutionOptions _currentOpts;

        protected override TransactionResult Execute(Transaction tx, ITxTracer tracer, ExecutionOptions opts)
        {
            _currentOpts = opts;
            InitializeTransactionState();
            ArbitrumTransactionProcessorResult preProcessResult = PreProcessArbitrumTransaction(tx, tracer);
            //if not doing any actual EVM, commit the changes and create receipt
            if (!preProcessResult.ContinueProcessing)
            {
                return FinalizeTransaction(preProcessResult.InnerResult, tx, tracer, preProcessResult.Logs);
            }
            TransactionResult evmResult = base.Execute(tx, tracer, opts);
            PostProcessArbitrumTransaction(tx);
            return evmResult;
        }

        private void InitializeTransactionState()
        {
            _arbosState = ArbosState.OpenArbosState(WorldState, new SystemBurner(readOnly: false), _logger);
            ((ArbitrumVirtualMachine)VirtualMachine).ArbitrumTxExecutionContext = new(null, null, UInt256.Zero, 0);
            _currentHeader = VirtualMachine.BlockExecutionContext.Header;
            _currentSpec = GetSpec(null, _currentHeader);
        }

        private ArbitrumTransactionProcessorResult PreProcessArbitrumTransaction(Transaction tx, ITxTracer tracer)
        {
            if (tx is not IArbitrumTransaction)
                return new(true, TransactionResult.Ok);
            ArbitrumTxType arbTxType = (ArbitrumTxType)tx.Type;
            //do internal Arb transaction processing - logic of StartTxHook
            ArbitrumTransactionProcessorResult result = ProcessArbitrumTransaction(arbTxType, tx, in VirtualMachine.BlockExecutionContext, tracer);
            ArbitrumVirtualMachine virtualMachine = (ArbitrumVirtualMachine)VirtualMachine;
            virtualMachine.ArbitrumTxExecutionContext = new(
                result.CurrentRetryable,
                result.CurrentRefundTo,
                virtualMachine.ArbitrumTxExecutionContext.PosterFee,
                virtualMachine.ArbitrumTxExecutionContext.PosterGas
            );
            return result;
        }

        protected override void PayFees(Transaction tx, BlockHeader header, IReleaseSpec spec, ITxTracer tracer, in TransactionSubstate substate, long spentGas, in UInt256 premiumPerGas, in UInt256 blobBaseFee, int statusCode)
        {
            _lastExecutionSuccess = statusCode == StatusCode.Success;
            base.PayFees(tx, header, spec, tracer, substate, spentGas, premiumPerGas, blobBaseFee, statusCode);
        }

        private void PostProcessArbitrumTransaction(Transaction tx)
        {
            if (tx.SpentGas == 0)
            {
                return;
            }
            _currentSpec = GetSpec(tx, _currentHeader!);
            EndTxHook(tx);
        }

        private TransactionResult FinalizeTransaction(TransactionResult result, Transaction tx, ITxTracer tracer, LogEntry[]? additionalLogs = null)
        {
            //TODO - need to establish what should be the correct flags to handle here
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
                if (result == TransactionResult.Ok)
                {
                    _currentHeader!.GasUsed += tx.SpentGas;
                    tracer.MarkAsSuccess(tx.To!, tx.SpentGas, [], additionalLogs ?? [], stateRoot);
                }
                else
                {
                    tracer.MarkAsFailed(tx.To!, tx.SpentGas, [], result.ToString(), stateRoot);
                }
            }
            return result;
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumTransaction(ArbitrumTxType txType, Transaction tx,
            in BlockExecutionContext blCtx, ITxTracer tracer)
        {
            return txType switch
            {
                ArbitrumTxType.ArbitrumDeposit => ProcessArbitrumDepositTxTransaction((ArbitrumTransaction<ArbitrumDepositTx>)tx),
                ArbitrumTxType.ArbitrumInternal => tx.SenderAddress != ArbosAddresses.ArbosAddress
                    ? new(false, TransactionResult.SenderNotSpecified)
                    : ProcessArbitrumInternalTransaction((ArbitrumTransaction<ArbitrumInternalTx>)tx, in blCtx, tracer),
                ArbitrumTxType.ArbitrumSubmitRetryable => ProcessArbitrumSubmitRetryableTransaction((ArbitrumTransaction<ArbitrumSubmitRetryableTx>)tx, in blCtx, tracer),
                ArbitrumTxType.ArbitrumRetry => ProcessArbitrumRetryTransaction((ArbitrumTransaction<ArbitrumRetryTx>)tx),
                //nothing to processing internally, continue with EVM execution
                _ => new(true, TransactionResult.Ok)
            };
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumDepositTxTransaction(
            ArbitrumTransaction<ArbitrumDepositTx> tx)
        {
            if (tx.To is null)
                return new(false, TransactionResult.MalformedTransaction);

            ArbitrumDepositTx depositTx = (ArbitrumDepositTx)tx.GetInner();

            MintBalance(depositTx.From, depositTx.Value, _arbosState!, WorldState, _currentSpec!);

            // We intentionally use the variant here that doesn't do tracing (instead of TransferBalance),
            // because this transfer is represented as the outer eth transaction.
            Transfer(depositTx.From, depositTx.To, depositTx.Value, WorldState, _currentSpec!);

            return new(false, TransactionResult.Ok);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumInternalTransaction(
            ArbitrumTransaction<ArbitrumInternalTx> tx,
            in BlockExecutionContext blCtx, ITxTracer tracer)
        {
            if (tx.Data.Length < 4)
                return new(false, TransactionResult.MalformedTransaction);

            ReadOnlyMemory<byte> methodId = tx.Data[..4];

            if (methodId.Span.SequenceEqual(AbiMetadata.StartBlockMethodId))
            {
                ValueHash256 prevHash = ValueKeccak.Zero;
                if (blCtx.Header.Number > 0)
                {
                    prevHash = blockTree.FindHash(blCtx.Header.Number - 1);
                }

                if (_arbosState!.CurrentArbosVersion >= ArbosVersion.ParentBlockHashSupport)
                {
                    ProcessParentBlockHash(prevHash, tracer);
                }

                Dictionary<string, object> callArguments = AbiMetadata.UnpackInput(AbiMetadata.StartBlockMethod, tx.Data.ToArray());

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

                TryReapOneRetryable(_arbosState!, blCtx.Header.Timestamp, worldState, _currentSpec!);
                TryReapOneRetryable(_arbosState!, blCtx.Header.Timestamp, worldState, _currentSpec!);

                _arbosState!.L2PricingState.UpdatePricingModel(timePassed);

                _arbosState!.UpgradeArbosVersionIfNecessary(blCtx.Header.Timestamp, worldState, _currentSpec!);
                return new(false, TransactionResult.Ok);
            }

            return new(false, TransactionResult.Ok);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumSubmitRetryableTransaction(
            ArbitrumTransaction<ArbitrumSubmitRetryableTx> tx,
            in BlockExecutionContext blCtx,
            ITxTracer tracer)
        {
            ArbitrumSubmitRetryableTx submitRetryableTx = (ArbitrumSubmitRetryableTx)tx.GetInner();

            List<LogEntry> eventLogs = new(2);

            Address escrowAddress = GetRetryableEscrowAddress(tx.Hash!.ValueHash256);
            Address networkFeeAccount = _arbosState!.NetworkFeeAccount.Get();

            UInt256 availableRefund = submitRetryableTx.DepositValue;
            ConsumeAvailable(ref availableRefund, submitRetryableTx.RetryValue);

            MintBalance(tx.SenderAddress, submitRetryableTx.DepositValue, _arbosState!, worldState, _currentSpec!);

            UInt256 balanceAfterMint = worldState.GetBalance(tx.SenderAddress ?? Address.Zero);
            if (balanceAfterMint < submitRetryableTx.MaxSubmissionFee)
            {
                return new(false, TransactionResult.InsufficientMaxFeePerGasForSenderBalance);
            }

            UInt256 submissionFee = CalcRetryableSubmissionFee(submitRetryableTx.RetryData.Length, submitRetryableTx.L1BaseFee);
            if (submissionFee > submitRetryableTx.MaxSubmissionFee)
            {
                return new(false, TransactionResult.InsufficientSenderBalance);
            }

            // collect the submission fee
            TransactionResult tr;
            if ((tr = TransferBalance(tx.SenderAddress, networkFeeAccount, submissionFee, _arbosState!, worldState, _currentSpec!)) != TransactionResult.Ok)
            {
                if (Logger.IsError) Logger.Error("Failed to transfer submission fee");
                return new(false, tr);
            }
            UInt256 withheldSubmissionFee = ConsumeAvailable(ref availableRefund, submissionFee);

            // refund excess submission fee
            UInt256 submissionFeeRefund = ConsumeAvailable(ref availableRefund, submitRetryableTx.MaxSubmissionFee - submissionFee);
            if (TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr!, submissionFeeRefund, _arbosState!, worldState, _currentSpec!) != TransactionResult.Ok)
            {
                if (Logger.IsError) Logger.Error("Failed to transfer submission fee refund");
            }

            // move the callvalue into escrow
            if ((tr = TransferBalance(tx.SenderAddress, escrowAddress, submitRetryableTx.RetryValue, _arbosState!,
                    worldState, _currentSpec!)) != TransactionResult.Ok)
            {
                if (TransferBalance(networkFeeAccount, tx.SenderAddress!, submissionFee, _arbosState!,
                        worldState, _currentSpec!) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error("Failed to refund submissionFee");
                }
                if (TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr!, withheldSubmissionFee, _arbosState!,
                        worldState, _currentSpec!) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error("Failed to refund withheld submission fee");
                }

                return new(false, tr);
            }

            ulong time = blCtx.Header.Timestamp;
            ulong timeout = time + Retryable.RetryableLifetimeSeconds;

            Retryable retryable = _arbosState.RetryableState.CreateRetryable(tx.Hash, tx.SenderAddress ?? Address.Zero, submitRetryableTx.RetryTo ?? Address.Zero,
                submitRetryableTx.RetryValue, submitRetryableTx.Beneficiary!, timeout,
                submitRetryableTx.RetryData.ToArray());

            ArbitrumPrecompileExecutionContext precompileExecutionContext = new(Address.Zero, ArbRetryableTx.TicketCreatedEventGasCost(tx.Hash), tracer,
                false, worldState, blCtx, tx.ChainId ?? 0, _currentSpec!);

            ArbRetryableTx.EmitTicketCreatedEvent(precompileExecutionContext, tx.Hash);
            eventLogs.AddRange(precompileExecutionContext.EventLogs);

            UInt256 effectiveBaseFee = blCtx.Header.BaseFeePerGas;
            ulong userGas = (ulong)tx.GasLimit;

            UInt256 maxGasCost = tx.MaxFeePerGas * userGas;
            bool maxFeePerGasTooLow = tx.MaxFeePerGas < effectiveBaseFee;

            UInt256 balance = worldState.GetBalance(tx.SenderAddress ?? Address.Zero);
            if (balance < maxGasCost || userGas < GasCostOf.Transaction || maxFeePerGasTooLow)
            {
                // User either specified too low of a gas fee cap, didn't have enough balance to pay for gas,
                // or the specified gas limit is below the minimum transaction gas cost.
                // Either way, attempt to refund the gas costs, since we're not doing the auto-redeem.
                UInt256 gasCostRefund = ConsumeAvailable(ref availableRefund, maxGasCost);
                if ((tr = TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, gasCostRefund, _arbosState!, worldState, _currentSpec!)) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error($"Failed to transfer gasCostRefund {tr}");
                }
                return new(false, TransactionResult.Ok);
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
                    if (TransferBalance(tx.SenderAddress, infraFeeAddress, infraCost, _arbosState!, worldState, _currentSpec!) != TransactionResult.Ok)
                    {
                        if (Logger.IsError) Logger.Error($"failed to transfer gas cost to infrastructure fee account {tr}");
                        return new(false, tr);
                    }
                }
            }

            if (networkCost > UInt256.Zero)
            {
                if (TransferBalance(tx.SenderAddress, networkFeeAccount, networkCost, _arbosState!, worldState, _currentSpec!) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error($"Failed to transfer gas cost to network fee account {tr}");
                    return new(false, tr);
                }
            }

            UInt256 withheldGasFunds = ConsumeAvailable(ref availableRefund, gasCost);
            UInt256 gasPriceRefund = (tx.MaxFeePerGas - effectiveBaseFee) * (ulong)tx.GasLimit;

            gasPriceRefund = ConsumeAvailable(ref availableRefund, gasPriceRefund);
            if (TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, gasPriceRefund, _arbosState!, worldState, _currentSpec!) != TransactionResult.Ok)
            {
                if (Logger.IsError) Logger.Error($"Failed to transfer gasPriceRefund {tr}");
            }

            availableRefund += withheldGasFunds;
            availableRefund += withheldSubmissionFee;

            ArbitrumRetryTx retryInnerTx = new(
                tx.ChainId ?? 0,
                0,
                retryable.From.Get(),
                effectiveBaseFee,
                userGas,
                retryable!.To!.Get(),
                retryable.CallValue.Get(),
                retryable.Calldata.Get(),
                tx.Hash,
                submitRetryableTx.FeeRefundAddr,
                availableRefund,
                submissionFee);

            ArbitrumTransaction<ArbitrumRetryTx> outerRetryTx = new(retryInnerTx)
            {
                ChainId = tx.ChainId,
                Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                SenderAddress = retryInnerTx.From,
                To = retryInnerTx.To,
                Value = retryable.CallValue.Get(),
                DecodedMaxFeePerGas = effectiveBaseFee,
                GasLimit = (long)userGas
            };
            retryable.IncrementNumTries();

            outerRetryTx.Hash = outerRetryTx.CalculateHash();

            precompileExecutionContext = new(Address.Zero,
                ArbRetryableTx.RedeemScheduledEventGasCost(tx.Hash, outerRetryTx.Hash,
                    retryInnerTx.Nonce, userGas, submitRetryableTx.FeeRefundAddr!, availableRefund, submissionFee),
                tracer, false, worldState, blCtx, tx.ChainId ?? 0, _currentSpec!);

            ArbRetryableTx.EmitRedeemScheduledEvent(precompileExecutionContext, tx.Hash, outerRetryTx.Hash,
                retryInnerTx.Nonce, userGas, submitRetryableTx.FeeRefundAddr!, availableRefund, submissionFee);
            eventLogs.AddRange(precompileExecutionContext.EventLogs);

            //set spend gas to be reflected in receipt
            tx.SpentGas = (long)userGas;

            //TODO Add tracer call
            return new(false, TransactionResult.Ok) { Logs = [.. eventLogs] };
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumRetryTransaction(ArbitrumTransaction<ArbitrumRetryTx> tx)
        {
            Retryable retryable = _arbosState!.RetryableState.OpenRetryable(tx.Inner.TicketId, VirtualMachine.BlockExecutionContext.Header.Timestamp)!;
            if (retryable is null)
            {
                return new(false, new TransactionResult($"Retryable with ticketId: {tx.Inner.TicketId} not found"));
            }

            // Transfer callvalue from escrow
            Address escrowAddress = GetRetryableEscrowAddress(tx.Inner.TicketId);
            TransactionResult transfer = TransferBalance(escrowAddress, tx.SenderAddress, tx.Value, _arbosState!, worldState, _currentSpec!);
            if (transfer != TransactionResult.Ok)
            {
                return new(false, transfer);
            }

            // The redeemer has pre-paid for this tx's gas
            UInt256 prepaid = VirtualMachine.BlockExecutionContext.Header.BaseFeePerGas * (ulong)tx.GasLimit;
            TransactionResult mint = TransferBalance(null, tx.SenderAddress, prepaid, _arbosState!, worldState, _currentSpec!);
            if (mint != TransactionResult.Ok)
            {
                return new(false, mint);
            }

            return new(true, TransactionResult.Ok)
            {
                CurrentRetryable = tx.Inner.TicketId,
                CurrentRefundTo = tx.Inner.RefundTo
            };
        }

        private void ProcessParentBlockHash(ValueHash256 prevHash, ITxTracer tracer)
        {
            var builder = new AccessList.Builder()
                .AddAddress(Eip2935Constants.BlockHashHistoryAddress);

            var newTransaction = new Transaction()
            {
                SenderAddress = Address.SystemUser,
                GasLimit = 30_000_000,
                GasPrice = UInt256.Zero,
                DecodedMaxFeePerGas = UInt256.Zero,
                To = Eip2935Constants.BlockHashHistoryAddress,
                AccessList = builder.Build(),
                Data = prevHash.Bytes.ToArray()
            };

            base.Execute(newTransaction, tracer, ExecutionOptions.Commit);
        }

        private static void TryReapOneRetryable(ArbosState arbosState, ulong currentTimeStamp, IWorldState worldState, IReleaseSpec releaseSpec)
        {
            ValueHash256 id = arbosState.RetryableState.TimeoutQueue.Peek();

            Retryable retryable = arbosState.RetryableState.GetRetryable(id);

            ulong timeout = retryable.Timeout.Get();
            if (timeout == 0)
                _ = arbosState.RetryableState.TimeoutQueue.Pop();

            if (timeout >= currentTimeStamp)
            {
                //error?
                return;
            }

            _ = arbosState.RetryableState.TimeoutQueue.Pop();
            ulong windowsLeft = retryable.TimeoutWindowsLeft.Get();

            if (windowsLeft == 0)
            {
                //error if false?
                DeleteRetryable(id, arbosState, worldState, releaseSpec);
                return;
            }

            retryable.Timeout.Set(timeout + Retryable.RetryableLifetimeSeconds);
            retryable.TimeoutWindowsLeft.Set(windowsLeft - 1);
        }

        public static bool DeleteRetryable(ValueHash256 id, ArbosState arbosState, IWorldState worldState,
            IReleaseSpec releaseSpec)
        {
            Retryable retryable = arbosState.RetryableState.GetRetryable(id);

            if (retryable.Timeout.Get() == 0)
                return false;

            Address escrowAddress = GetRetryableEscrowAddress(id);
            Address beneficiaryAddress = retryable.Beneficiary.Get();
            UInt256 amount = worldState.GetBalance(escrowAddress);

            TransactionResult tr = TransferBalance(escrowAddress, beneficiaryAddress, amount, arbosState, worldState, releaseSpec);
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
        private static TransactionResult TransferBalance(Address? from, Address? to, UInt256 amount,
            ArbosState arbosState,
            IWorldState worldState, IReleaseSpec releaseSpec)
        {
            //TODO add trace
            if (amount.IsZero) return TransactionResult.Ok;

            if (from is not null)
            {
                UInt256 balance = worldState.GetBalance(from);
                if (balance < amount)
                {
                    return TransactionResult.InsufficientSenderBalance;
                }
                if (arbosState.CurrentArbosVersion < ArbosVersion.FixZombieAccounts && amount == UInt256.Zero)
                {
                    //create zombie?
                    if (!worldState.AccountExists(from))
                        worldState.CreateAccount(from, 0);
                }

                worldState.SubtractFromBalance(from, amount, releaseSpec);
            }

            if (to is not null)
                worldState.AddToBalanceAndCreateIfNotExists(to, amount, releaseSpec);

            return TransactionResult.Ok;
        }

        private static void MintBalance(Address? to, UInt256 amount, ArbosState arbosState, IWorldState worldState,
            IReleaseSpec releaseSpec) => TransferBalance(null, to, amount, arbosState, worldState, releaseSpec);

        private static void Transfer(Address from, Address to, UInt256 amount, IWorldState worldState, IReleaseSpec releaseSpec)
        {
            worldState.SubtractFromBalance(from, amount, releaseSpec);
            worldState.AddToBalanceAndCreateIfNotExists(to, amount, releaseSpec);
        }

        public static Address GetRetryableEscrowAddress(ValueHash256 hash)
        {
            var staticBytes = "retryable escrow"u8.ToArray();
            Span<byte> workingSpan = stackalloc byte[staticBytes.Length + Keccak.Size];
            staticBytes.CopyTo(workingSpan);
            hash.Bytes.CopyTo(workingSpan[staticBytes.Length..]);
            return new Address(Keccak.Compute(workingSpan).Bytes[^Address.Size..]);
        }

        private static UInt256 CalcRetryableSubmissionFee(int byteLength, UInt256 l1BaseFee) => l1BaseFee * (1400 + 6 * (uint)byteLength);

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

        private static bool ShouldDropTip()
        {
            return false;
        }


        private record ArbitrumTransactionProcessorResult(
            bool ContinueProcessing,
            TransactionResult InnerResult)
        {
            public LogEntry[] Logs { get; init; } = [];
            public Hash256? CurrentRetryable { get; init; }
            public Address? CurrentRefundTo { get; init; }
        }

        private void EndTxHook(Transaction tx)
        {
            ulong gasUsed = (ulong)tx.SpentGas;
            ulong gasLeft = (ulong)tx.GasLimit - gasUsed;

            if (gasLeft > (ulong)tx.GasLimit)
            {
                return;
            }

            if (tx is ArbitrumTransaction<ArbitrumRetryTx> retryTx)
            {
                HandleRetryTransactionEndTxHook(retryTx, gasLeft, gasUsed);
                return;
            }

            ArbitrumVirtualMachine virtualMachine = (ArbitrumVirtualMachine)VirtualMachine;
            HandleNormalTransactionEndTxHook(gasUsed, virtualMachine.ArbitrumTxExecutionContext);
        }

        private void HandleRetryTransactionEndTxHook(
            ArbitrumTransaction<ArbitrumRetryTx> retryTx,
            ulong gasLeft,
            ulong gasUsed)
        {
            ArbitrumRetryTx inner = retryTx.Inner;
            UInt256 effectiveBaseFee = ValidateAndGetEffectiveBaseFee(inner);

            UInt256 gasRefund = effectiveBaseFee * gasLeft;
            BurnBalance(inner.From, gasRefund, _arbosState!, WorldState, _currentSpec!);

            UInt256 maxRefund = inner.MaxRefund;
            Address networkFeeAccount = _arbosState!.NetworkFeeAccount.Get();

            HandleSubmissionFeeRefund(inner, ref maxRefund, networkFeeAccount, _currentSpec!);

            UInt256 gasCharge = effectiveBaseFee * gasUsed;
            ConsumeAvailable(ref maxRefund, gasCharge);

            HandleGasRefunds(inner, effectiveBaseFee, gasLeft, ref maxRefund, networkFeeAccount);

            HandleRetryableLifecycle(inner);

            _arbosState!.L2PricingState.AddToGasPool(-gasUsed.ToLongSafe());
        }

        private UInt256 ValidateAndGetEffectiveBaseFee(ArbitrumRetryTx inner)
        {
            UInt256 effectiveBaseFee = inner.GasFeeCap;

            if (!_currentOpts.HasFlag(ExecutionOptions.SkipValidation) && effectiveBaseFee != _currentHeader!.BaseFeePerGas)
            {
                if (_logger.IsError) _logger.Error($"ArbitrumRetryTx GasFeeCap doesn't match basefee in commit mode: gasFeeCap={effectiveBaseFee}, baseFee={_currentHeader!.BaseFeePerGas}");
                effectiveBaseFee = _currentHeader!.BaseFeePerGas;
            }

            return effectiveBaseFee;
        }

        private static void BurnBalance(Address fromAddress, UInt256 amount, ArbosState arbosState, IWorldState worldState, IReleaseSpec releaseSpec) =>
            TransferBalance(fromAddress, null, amount, arbosState, worldState, releaseSpec);

        private void HandleSubmissionFeeRefund(ArbitrumRetryTx inner, ref UInt256 maxRefund, Address networkFeeAccount, IReleaseSpec spec)
        {
            if (_lastExecutionSuccess)
            {
                RefundFromAccount(networkFeeAccount, inner.SubmissionFeeRefund, ref maxRefund, inner, spec);
            }
            else
            {
                ConsumeAvailable(ref maxRefund, inner.SubmissionFeeRefund);
            }
        }

        private void HandleGasRefunds(ArbitrumRetryTx inner, UInt256 effectiveBaseFee, ulong gasLeft, ref UInt256 maxRefund, Address networkFeeAccount)
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
                    RefundFromAccount(infraFeeAccount, infraRefund, ref maxRefund, inner, _currentSpec!);
                }
            }

            RefundFromAccount(networkFeeAccount, networkRefund, ref maxRefund, inner, _currentSpec!);
        }

        private void HandleRetryableLifecycle(ArbitrumRetryTx inner)
        {
            if (_lastExecutionSuccess)
            {
                DeleteRetryable(inner.TicketId, _arbosState!, WorldState, _currentSpec!);
                return;
            }

            Address escrowAddress = GetRetryableEscrowAddress(inner.TicketId);
            TransactionResult escrowResult = TransferBalance(inner.From, escrowAddress, inner.Value, _arbosState!, WorldState, _currentSpec!);
            if (escrowResult != TransactionResult.Ok)
            {
                if (_logger.IsError) _logger.Error($"Failed to return callvalue to escrow: {escrowResult}");
            }
        }

        private void RefundFromAccount(Address refundFrom, UInt256 amount, ref UInt256 maxRefund, ArbitrumRetryTx inner, IReleaseSpec spec)
        {
            UInt256 availableBalance = WorldState.GetBalance(refundFrom);
            if (UInt256.SubtractUnderflow(availableBalance, amount, out _))
            {
                if (_logger.IsError) _logger.Error($"fee address doesn't have enough funds to give user refund: available={availableBalance}, needed={amount}, address={refundFrom}");
                return;
            }

            UInt256 toRefundAddr = ConsumeAvailable(ref maxRefund, amount);
            UInt256 remaining = amount - toRefundAddr;

            TransactionResult toRefundResult = TransferBalance(refundFrom, inner.RefundTo, toRefundAddr, _arbosState!, WorldState, spec);
            if (toRefundResult != TransactionResult.Ok)
            {
                if (_logger.IsError) _logger.Error($"Failed to refund {toRefundAddr} from {refundFrom} to {inner.RefundTo}: {toRefundResult}");
            }

            TransactionResult toFromResult = TransferBalance(refundFrom, inner.From, remaining, _arbosState!, WorldState, spec);
            if (toFromResult != TransactionResult.Ok)
            {
                if (_logger.IsError) _logger.Error($"Failed to refund remaining {remaining} from {refundFrom} to {inner.From}: {toFromResult}");
            }
        }

        private void HandleNormalTransactionEndTxHook(
            ulong gasUsed,
            ArbitrumTxExecutionContext txContext)
        {
            UInt256 baseFee = _currentHeader!.BaseFeePerGas;
            UInt256 totalCost = baseFee * gasUsed;

            if (UInt256.SubtractUnderflow(totalCost, txContext.PosterFee, out UInt256 computeCost))
            {
                if (_logger.IsInfo) _logger.Info($"Total cost < poster cost: gasUsed={gasUsed}, baseFee={baseFee}, posterFee={txContext.PosterFee}");
                txContext.PosterFee = UInt256.Zero;
                computeCost = totalCost;
            }

            computeCost = HandleInfrastructureFee(computeCost, gasUsed, baseFee, txContext);
            if (!computeCost.IsZero)
            {
                Address networkFeeAccount = _arbosState!.NetworkFeeAccount.Get();
                MintBalance(networkFeeAccount, computeCost, _arbosState!, WorldState, _currentSpec!);
            }

            HandlePosterFeeAndL1Tracking(txContext);

            if (!_currentHeader!.BaseFeePerGas.IsZero)
            {
                UpdateGasPool(gasUsed, txContext);
            }
        }

        private UInt256 HandleInfrastructureFee(UInt256 computeCost, ulong gasUsed, UInt256 baseFee, ArbitrumTxExecutionContext txContext)
        {
            if (_arbosState!.CurrentArbosVersion < ArbosVersion.IntroduceInfraFees)
                return computeCost;

            Address infraFeeAccount = _arbosState!.InfraFeeAccount.Get();
            if (infraFeeAccount == Address.Zero)
                return computeCost;

            UInt256 minBaseFee = _arbosState!.L2PricingState.MinBaseFeeWeiStorage.Get();
            UInt256 infraFee = UInt256.Min(minBaseFee, baseFee);
            ulong computeGas = gasUsed > txContext.PosterGas ? gasUsed - txContext.PosterGas : gasUsed;
            UInt256 infraComputeCost = infraFee * computeGas;

            MintBalance(infraFeeAccount, infraComputeCost, _arbosState!, WorldState, _currentSpec!);

            if (UInt256.SubtractUnderflow(computeCost, infraComputeCost, out UInt256 remainingCost))
            {
                if (_logger.IsError) _logger.Error($"Compute cost < infra compute cost: computeCost={computeCost}, infraComputeCost={infraComputeCost}");
                return UInt256.Zero;
            }

            return remainingCost;
        }

        private void HandlePosterFeeAndL1Tracking(ArbitrumTxExecutionContext txContext)
        {
            Address posterFeeDestination = _arbosState!.CurrentArbosVersion < ArbosVersion.ChangePosterDestination
                ? VirtualMachine.BlockExecutionContext.Header.Beneficiary ?? Address.Zero
                : ArbosAddresses.L1PricerFundsPoolAddress;

            MintBalance(posterFeeDestination, txContext.PosterFee, _arbosState!, WorldState, _currentSpec!);

            if (_arbosState!.CurrentArbosVersion >= ArbosVersion.L1FeesAvailable)
            {
                UpdateL1FeesAvailable(_arbosState!, txContext);
            }
        }

        private void UpdateL1FeesAvailable(ArbosState arbosState, ArbitrumTxExecutionContext txContext)
        {
            try
            {
                arbosState.L1PricingState.AddToL1FeesAvailable(txContext.PosterFee);
            }
            catch (Exception ex)
            {
                if (_logger.IsError) _logger.Error($"Failed to update L1FeesAvailable: {ex}");
            }
        }

        private void UpdateGasPool(ulong gasUsed, ArbitrumTxExecutionContext txContext)
        {
            ulong computeGas = gasUsed > txContext.PosterGas ? gasUsed - txContext.PosterGas : gasUsed;
            if (gasUsed <= txContext.PosterGas && gasUsed > 0)
            {
                if (_logger.IsError) _logger.Error($"Total gas used < poster gas component: gasUsed={gasUsed}, posterGas={txContext.PosterGas}");
            }
            _arbosState!.L2PricingState.AddToGasPool(-computeGas.ToLongSafe());
        }
    }
}
