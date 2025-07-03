// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Transactions;
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
using System.Diagnostics;
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
        private readonly IBlockTree blockTree = blockTree;
        private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumTransactionProcessor>();
        private ArbosState? _arbosState;

        protected override TransactionResult Execute(Transaction tx, ITxTracer tracer, ExecutionOptions opts)
        {
            // Phase 1: Initialize transaction state
            InitializeTransactionState();

            // Phase 2: Pre-processing
            var preProcessResult = PreProcessArbitrumTransaction(tx, tracer, opts);
            if (!preProcessResult.ContinueProcessing)
            {
                // Transaction was fully processed in pre-processing phase
                // We need to handle finalization (commit/restore and receipt creation)
                return FinalizeTransaction(preProcessResult.InnerResult, tx, tracer, opts, preProcessResult.Logs);
            }

            // Phase 3: EVM execution - base.Execute handles its own finalization including receipts
            var evmResult = ExecuteEvm(tx, tracer, opts);

            // Phase 4: Post-processing (EndTxHook for fee distribution)
            PostProcessArbitrumTransaction(tx, tracer, opts, evmResult);

            // No need for additional finalization - base.Execute already handled it
            return evmResult;
        }

        private void InitializeTransactionState()
        {
            // Initialize ArbosState once per transaction (similar to nitro's NewTxProcessor)
            var burner = new SystemBurner(readOnly: false);
            _arbosState = ArbosState.OpenArbosState(WorldState, burner, _logger);

            // Initialize the ArbitrumTxExecutionContext for all transaction types
            ArbitrumVirtualMachine virtualMachine = (ArbitrumVirtualMachine)VirtualMachine;
            virtualMachine.ArbitrumTxExecutionContext = new(null, null, UInt256.Zero, 0);
        }

        private ArbitrumTransactionProcessorResult PreProcessArbitrumTransaction(Transaction tx, ITxTracer tracer, ExecutionOptions opts)
        {
            // Handle non-Arbitrum transactions
            if (tx is not IArbitrumTransaction)
            {
                // Non-Arbitrum transactions continue to EVM execution and post-processing
                return new(true, TransactionResult.Ok);
            }

            var arbTxType = (ArbitrumTxType)tx.Type;
            BlockHeader header = VirtualMachine.BlockExecutionContext.Header;
            IReleaseSpec spec = GetSpec(tx, header);

                    // Process Arbitrum-specific transaction types
        var result = ProcessArbitrumTransaction(arbTxType, tx, in VirtualMachine.BlockExecutionContext, tracer, spec);

            // Update the context with Arbitrum-specific values
            ArbitrumVirtualMachine virtualMachine = (ArbitrumVirtualMachine)VirtualMachine;
            virtualMachine.ArbitrumTxExecutionContext = new(
                result.CurrentRetryable,
                result.CurrentRefundTo,
                virtualMachine.ArbitrumTxExecutionContext.PosterFee,
                virtualMachine.ArbitrumTxExecutionContext.PosterGas
            );

            return result;
        }

        private TransactionResult ExecuteEvm(Transaction tx, ITxTracer tracer, ExecutionOptions opts)
        {
            BlockHeader header = VirtualMachine.BlockExecutionContext.Header;

            // Adjust gas pricing for Arbitrum if needed
            if (ShouldDropTip() && tx.GasPrice > header.BaseFeePerGas)
            {
                tx.GasPrice = header.BaseFeePerGas;
                tx.DecodedMaxFeePerGas = header.BaseFeePerGas;
            }

            // For non-Arbitrum transactions, execute normally in EVM
            if (tx is not IArbitrumTransaction)
            {
                return base.Execute(tx, tracer, opts);
            }

            // For Arbitrum transactions that reach this point, they should execute in EVM
            // But we need to be careful not to double-process
            // Most Arbitrum transaction types shouldn't reach here since they return ContinueProcessing = false
            return base.Execute(tx, tracer, opts);
        }

        private void PostProcessArbitrumTransaction(Transaction tx, ITxTracer tracer, ExecutionOptions opts, TransactionResult evmResult)
        {
            if (tx.SpentGas == 0)
                return;

            BlockHeader header = VirtualMachine.BlockExecutionContext.Header;
            IReleaseSpec spec = GetSpec(tx, header);
            
            ulong gasUsed = (ulong)tx.SpentGas;
            ulong gasLeft = (ulong)tx.GasLimit - gasUsed;
            bool success = evmResult == TransactionResult.Ok;

            EndTxHook(gasLeft, success, tx, header, spec, tracer, opts);
        }

        private TransactionResult FinalizeTransaction(TransactionResult result, Transaction tx, ITxTracer tracer, ExecutionOptions opts, LogEntry[]? additionalLogs = null)
        {
            BlockHeader header = VirtualMachine.BlockExecutionContext.Header;
            IReleaseSpec spec = GetSpec(tx, header);

            // Determine commit/restore flags
            bool restore = opts.HasFlag(ExecutionOptions.Restore);
            bool commit = opts.HasFlag(ExecutionOptions.Commit) ||
                          (!opts.HasFlag(ExecutionOptions.SkipValidation) && !spec.IsEip658Enabled);

            // Apply state changes
            if (commit)
            {
                WorldState.Commit(spec, tracer.IsTracingState ? tracer : NullStateTracer.Instance,
                    commitRoots: !spec.IsEip658Enabled);
            }
            else if (restore)
            {
                WorldState.Reset(resetBlockChanges: false);
            }

            // Handle receipt tracing
            if (tracer.IsTracingReceipt)
            {
                Hash256? stateRoot = null;
                if (!spec.IsEip658Enabled)
                {
                    WorldState.RecalculateStateRoot();
                    stateRoot = WorldState.StateRoot;
                }

                if (result == TransactionResult.Ok)
                {
                    header.GasUsed += tx.SpentGas;
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
            in BlockExecutionContext blCtx, ITxTracer tracer, IReleaseSpec releaseSpec)
        {
            switch (txType)
            {
                case ArbitrumTxType.ArbitrumDeposit:
                    return ProcessArbitrumDepositTxTransaction(tx as ArbitrumTransaction<ArbitrumDepositTx>, releaseSpec);
                case ArbitrumTxType.ArbitrumInternal:

                    if (tx.SenderAddress != ArbosAddresses.ArbosAddress)
                        return new(false, TransactionResult.SenderNotSpecified);

                    return ProcessArbitrumInternalTransaction(tx as ArbitrumTransaction<ArbitrumInternalTx>, in blCtx, tracer, releaseSpec);
                case ArbitrumTxType.ArbitrumSubmitRetryable:
                    return ProcessArbitrumSubmitRetryableTransaction(tx as ArbitrumTransaction<ArbitrumSubmitRetryableTx>, in blCtx, tracer, releaseSpec);
                case ArbitrumTxType.ArbitrumRetry:
                    return ProcessArbitrumRetryTransaction(tx as ArbitrumTransaction<ArbitrumRetryTx>, in blCtx, releaseSpec);
            }

            //nothing to processing internally, continue with EVM execution
            return new(true, TransactionResult.Ok);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumDepositTxTransaction(
            ArbitrumTransaction<ArbitrumDepositTx>? tx, IReleaseSpec releaseSpec)
        {
            if (tx is null || tx.To is null)
                return new(false, TransactionResult.MalformedTransaction);

            ArbitrumDepositTx depositTx = (ArbitrumDepositTx)tx.GetInner();

            MintBalance(depositTx.From, depositTx.Value, _arbosState!, WorldState, releaseSpec);

            // We intentionally use the variant here that doesn't do tracing (instead of TransferBalance),
            // because this transfer is represented as the outer eth transaction.
            Transfer(depositTx.From, depositTx.To, depositTx.Value, WorldState, releaseSpec);

            return new(false, TransactionResult.Ok);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumInternalTransaction(
            ArbitrumTransaction<ArbitrumInternalTx>? tx,
            in BlockExecutionContext blCtx, ITxTracer tracer, IReleaseSpec releaseSpec)
        {
            if (tx is null || tx.Data.Length < 4)
                return new(false, TransactionResult.MalformedTransaction);

            var methodId = tx.Data[..4];

            if (methodId.Span.SequenceEqual(AbiMetadata.StartBlockMethodId))
            {

                ValueHash256 prevHash = Keccak.Zero;
                if (blCtx.Header.Number > 0)
                {
                    prevHash = blockTree.FindHash(blCtx.Header.Number - 1);
                }

                if (_arbosState!.CurrentArbosVersion >= ArbosVersion.ParentBlockHashSupport)
                {
                    ProcessParentBlockHash(prevHash, in blCtx, tracer);
                }

                var callArguments = AbiMetadata.UnpackInput(AbiMetadata.StartBlockMethod, tx.Data.ToArray()!);

                var l1BlockNumber = (ulong)callArguments["l1BlockNumber"];
                var timePassed = (ulong)callArguments["timePassed"];

                if (_arbosState!.CurrentArbosVersion < ArbosVersion.Three)
                {
                    // (incorrectly) use the L2 block number instead
                    timePassed = (ulong)callArguments["l2BlockNumber"];
                }

                if (_arbosState.CurrentArbosVersion < ArbosVersion.Eight)
                {
                    // in old versions we incorrectly used an L1 block number one too high
                    l1BlockNumber++;
                }

                var oldL1BlockNumber = _arbosState!.Blockhashes.GetL1BlockNumber();
                var l2BaseFee = _arbosState!.L2PricingState.BaseFeeWeiStorage.Get();

                if (l1BlockNumber > oldL1BlockNumber)
                {
                    _arbosState!.Blockhashes.RecordNewL1Block(l1BlockNumber - 1, prevHash,
                        _arbosState!.CurrentArbosVersion);
                }

                // Try to reap 2 retryables
                TryReapOneRetryable(_arbosState!, blCtx.Header.Timestamp, worldState, releaseSpec);
                TryReapOneRetryable(_arbosState!, blCtx.Header.Timestamp, worldState, releaseSpec);

                _arbosState!.L2PricingState.UpdatePricingModel(timePassed);

                _arbosState!.UpgradeArbosVersionIfNecessary(blCtx.Header.Timestamp, worldState, releaseSpec);
                return new(false, TransactionResult.Ok);
            }

            return new(false, TransactionResult.Ok);
        }


        private ArbitrumTransactionProcessorResult ProcessArbitrumSubmitRetryableTransaction(
            ArbitrumTransaction<ArbitrumSubmitRetryableTx>? tx,
            in BlockExecutionContext blCtx,
            ITxTracer tracer,
            IReleaseSpec releaseSpec)
        {
            ArbitrumSubmitRetryableTx submitRetryableTx = (ArbitrumSubmitRetryableTx)tx.GetInner();

            var eventLogs = new List<LogEntry>(2);

            var escrowAddress = GetRetryableEscrowAddress(tx.Hash.ValueHash256);

            SystemBurner burner = new(readOnly: false);
            ArbosState arbosState =
                    ArbosState.OpenArbosState(worldState, burner, logManager.GetClassLogger<ArbosState>());

            var networkFeeAccount = arbosState.NetworkFeeAccount.Get();

            UInt256 availableRefund = submitRetryableTx.DepositValue;
            ConsumeAvailable(ref availableRefund, submitRetryableTx.RetryValue);

            MintBalance(tx.SenderAddress, submitRetryableTx.DepositValue, arbosState, worldState, releaseSpec);

            var balanceAfterMint = worldState.GetBalance(tx.SenderAddress);
            if (balanceAfterMint < submitRetryableTx.MaxSubmissionFee)
            {
                return new(false, TransactionResult.InsufficientMaxFeePerGasForSenderBalance);
            }

            var submissionFee = CalcRetryableSubmissionFee(submitRetryableTx.RetryData.Length, submitRetryableTx.L1BaseFee);
            if (submissionFee > submitRetryableTx.MaxSubmissionFee)
            {
                return new(false, TransactionResult.InsufficientSenderBalance);
            }

            // collect the submission fee
            TransactionResult tr;
            if ((tr = TransferBalance(tx.SenderAddress, networkFeeAccount, submissionFee, arbosState, worldState, releaseSpec)) != TransactionResult.Ok)
            {
                if (Logger.IsError) Logger.Error("Failed to transfer submission fee");
                return new(false, tr);
            }
            var withheldSubmissionFee = ConsumeAvailable(ref availableRefund, submissionFee);

            // refund excess submission fee
            var submissionFeeRefund =
                ConsumeAvailable(ref availableRefund, submitRetryableTx.MaxSubmissionFee - submissionFee);
            if (TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, submissionFeeRefund, arbosState, worldState, releaseSpec) != TransactionResult.Ok)
            {
                if (Logger.IsError) Logger.Error("Failed to transfer submission fee refund");
            }

            // move the callvalue into escrow
            if ((tr = TransferBalance(tx.SenderAddress, escrowAddress, submitRetryableTx.RetryValue, arbosState,
                    worldState, releaseSpec)) != TransactionResult.Ok)
            {
                var innerTr = TransactionResult.Ok;
                if ((innerTr = TransferBalance(networkFeeAccount, tx.SenderAddress, submissionFee, arbosState,
                        worldState, releaseSpec)) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error("Failed to refund submissionFee");
                }
                if ((innerTr = TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, withheldSubmissionFee, arbosState,
                        worldState, releaseSpec)) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error("Failed to refund withheld submission fee");
                }

                return new(false, tr);
            }

            var time = blCtx.Header.Timestamp;
            var timeout = time + Retryable.RetryableLifetimeSeconds;

            var retryable = arbosState.RetryableState.CreateRetryable(tx.Hash, tx.SenderAddress, submitRetryableTx.RetryTo,
                submitRetryableTx.RetryValue, submitRetryableTx.Beneficiary, timeout,
                submitRetryableTx.RetryData.ToArray());

            ulong gasSupplied = GasCostOf.CallValue;

            var precompileExecutionContext = new ArbitrumPrecompileExecutionContext(Address.Zero, ArbRetryableTx.TicketCreatedEventGasCost(tx.Hash), tracer,
                false, worldState, blCtx, tx.ChainId ?? 0, releaseSpec);

            ArbRetryableTx.EmitTicketCreatedEvent(precompileExecutionContext, tx.Hash);
            eventLogs.AddRange(precompileExecutionContext.EventLogs);


            var effectiveBaseFee = blCtx.Header.BaseFeePerGas;
            var userGas = tx.GasLimit;

            var maxGasCost = tx.MaxFeePerGas * (ulong)userGas;
            var maxFeePerGasTooLow = tx.MaxFeePerGas < effectiveBaseFee;

            var balance = worldState.GetBalance(tx.SenderAddress);
            if (balance < maxGasCost || userGas < GasCostOf.Transaction || maxFeePerGasTooLow)
            {
                // User either specified too low of a gas fee cap, didn't have enough balance to pay for gas,
                // or the specified gas limit is below the minimum transaction gas cost.
                // Either way, attempt to refund the gas costs, since we're not doing the auto-redeem.
                var gasCostRefund = ConsumeAvailable(ref availableRefund, maxGasCost);
                if ((tr = TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, gasCostRefund, arbosState, worldState, releaseSpec)) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error($"Failed to transfer gasCostRefund {tr}");
                }
                return new(false, TransactionResult.Ok);
            }

            var gasCost = effectiveBaseFee * (ulong)userGas;
            var networkCost = gasCost;
            if (arbosState.CurrentArbosVersion >= ArbosVersion.Eleven)
            {
                var infraFeeAddress = arbosState.InfraFeeAccount.Get();
                if (infraFeeAddress != Address.Zero)
                {
                    var minBaseFee = arbosState.L2PricingState.MinBaseFeeWeiStorage.Get();
                    var infraCost = minBaseFee * effectiveBaseFee;
                    infraCost = ConsumeAvailable(ref networkCost, infraCost);
                    if (TransferBalance(tx.SenderAddress, infraFeeAddress, infraCost, arbosState, worldState, releaseSpec) != TransactionResult.Ok)
                    {
                        if (Logger.IsError) Logger.Error($"failed to transfer gas cost to infrastructure fee account {tr}");
                        return new(false, tr);
                    }
                }
            }

            if (networkCost > UInt256.Zero)
            {
                if (TransferBalance(tx.SenderAddress, networkFeeAccount, networkCost, arbosState, worldState, releaseSpec) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error($"Failed to transfer gas cost to network fee account {tr}");
                    return new(false, tr);
                }
            }

            var withheldGasFunds = ConsumeAvailable(ref availableRefund, gasCost);
            var gasPriceRefund = (tx.MaxFeePerGas - effectiveBaseFee) * (ulong)tx.GasLimit;

            gasPriceRefund = ConsumeAvailable(ref availableRefund, gasPriceRefund);
            if (TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, gasPriceRefund, arbosState, worldState, releaseSpec) != TransactionResult.Ok)
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
                (ulong)userGas,
                retryable.To.Get(),
                retryable.CallValue.Get(),
                retryable.Calldata.Get(),
                tx.Hash,
                submitRetryableTx.FeeRefundAddr,
                availableRefund,
                submissionFee);

            var outerRetryTx = new ArbitrumTransaction<ArbitrumRetryTx>(retryInnerTx)
            {
                ChainId = tx.ChainId,
                Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                SenderAddress = retryInnerTx.From,
                To = retryInnerTx.To,
                Value = retryable.CallValue.Get(),
                DecodedMaxFeePerGas = effectiveBaseFee,
                GasLimit = userGas
            };
            retryable.IncrementNumTries();

            outerRetryTx.Hash = outerRetryTx.CalculateHash();

            precompileExecutionContext = new ArbitrumPrecompileExecutionContext(Address.Zero,
                ArbRetryableTx.RedeemScheduledEventGasCost(tx.Hash, outerRetryTx.Hash,
                    retryInnerTx.Nonce, (ulong)userGas, submitRetryableTx.FeeRefundAddr, availableRefund, submissionFee),
                tracer, false, worldState, blCtx, tx.ChainId ?? 0, releaseSpec);

            ArbRetryableTx.EmitRedeemScheduledEvent(precompileExecutionContext, tx.Hash, outerRetryTx.Hash,
                retryInnerTx.Nonce, (ulong)userGas, submitRetryableTx.FeeRefundAddr, availableRefund, submissionFee);
            eventLogs.AddRange(precompileExecutionContext.EventLogs);

            //set spend gas to be reflected in receipt
            tx.SpentGas = userGas;

            //TODO Add tracer call
            return new(false, TransactionResult.Ok) { Logs = eventLogs.ToArray() };
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumRetryTransaction(ArbitrumTransaction<ArbitrumRetryTx>? tx,
            in BlockExecutionContext blCtx, IReleaseSpec releaseSpec)
        {
            if (tx is null)
                return new(false, TransactionResult.MalformedTransaction);

            SystemBurner burner = new(readOnly: false);
            ArbosState arbosState =
                    ArbosState.OpenArbosState(worldState, burner, logManager.GetClassLogger<ArbosState>());

            Retryable? retryable = arbosState.RetryableState.OpenRetryable(tx.Inner.TicketId, blCtx.Header.Timestamp);
            if (retryable is null)
            {
                return new(false, new TransactionResult($"Retryable with ticketId: {tx.Inner.TicketId} not found"));
            }

            // Transfer callvalue from escrow
            Address escrowAddress = GetRetryableEscrowAddress(tx.Inner.TicketId);
            TransactionResult transfer = TransferBalance(escrowAddress, tx.SenderAddress, tx.Value, arbosState, worldState, releaseSpec);
            if (transfer != TransactionResult.Ok)
            {
                return new(false, transfer);
            }

            // The redeemer has pre-paid for this tx's gas
            UInt256 prepaid = blCtx.Header.BaseFeePerGas * (ulong)tx.GasLimit;
            TransactionResult mint = TransferBalance(null, tx.SenderAddress, prepaid, arbosState, worldState, releaseSpec);
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

        private void ProcessParentBlockHash(ValueHash256 prevHash, in BlockExecutionContext blCtx, ITxTracer tracer)
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

        private void TryReapOneRetryable(ArbosState arbosState, ulong currentTimeStamp, IWorldState worldState, IReleaseSpec releaseSpec)
        {
            var id = arbosState.RetryableState.TimeoutQueue.Peek();

            var retryable = arbosState.RetryableState.GetRetryable(id);

            var timeout = retryable.Timeout.Get();
            if (timeout == 0)
                _ = arbosState.RetryableState.TimeoutQueue.Pop();

            if (timeout >= currentTimeStamp)
            {
                //error?
                return;
            }

            _ = arbosState.RetryableState.TimeoutQueue.Pop();
            var windowsLeft = retryable.TimeoutWindowsLeft.Get();

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
            var retryable = arbosState.RetryableState.GetRetryable(id);

            if (retryable.Timeout.Get() == 0)
                return false;

            var escrowAddress = GetRetryableEscrowAddress(id);
            var beneficiaryAddress = retryable.Beneficiary.Get();
            var amount = worldState.GetBalance(escrowAddress);

            var tr = TransferBalance(escrowAddress, beneficiaryAddress, amount, arbosState, worldState, releaseSpec);
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
            if (from is not null)
            {
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

        private void MintBalance(Address? to, UInt256 amount, ArbosState arbosState, IWorldState worldState,
            IReleaseSpec releaseSpec)
        {
            TransferBalance(null, to, amount, arbosState, worldState, releaseSpec);
        }

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
            hash.Bytes.CopyTo(workingSpan.Slice(staticBytes.Length));
            return new Address(Keccak.Compute(workingSpan).Bytes.Slice(Keccak.Size - Address.Size));
        }

        private UInt256 CalcRetryableSubmissionFee(int byteLength, UInt256 l1BaseFee)
        {
            return l1BaseFee * (1400 + 6 * (uint)byteLength);
        }

        /// <summary>
        /// Reduces available pool by given amount until zero
        /// </summary>
        /// <param name="pool"></param>
        /// <param name="amount"></param>
        /// <returns>Amount consumed from pool</returns>
        private UInt256 ConsumeAvailable(ref UInt256 pool, UInt256 amount)
        {
            if (amount > pool)
            {
                var prevPool = pool;
                pool = 0;
                return prevPool;
            }
            pool -= amount;
            return amount;
        }

        private bool ShouldDropTip()
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

        /// <summary>
        /// EndTxHook handles post-transaction fee distribution and gas refunds
        /// </summary>
        private void EndTxHook(ulong gasLeft, bool success, Transaction tx, BlockHeader header, IReleaseSpec spec, ITxTracer tracer, ExecutionOptions opts)
        {
            if (gasLeft > (ulong)tx.GasLimit)
            {
                _logger.Error($"Transaction somehow refunds gas after computation: gasLeft={gasLeft}, gasLimit={tx.GasLimit}");
                return;
            }

            var gasUsed = (ulong)tx.GasLimit - gasLeft;

            if (tx is ArbitrumTransaction<ArbitrumRetryTx> retryTx)
            {
                HandleRetryTransactionEndTxHook(retryTx, gasLeft, gasUsed, success, header, spec, _arbosState!, tracer, opts);
                return;
            }

            ArbitrumVirtualMachine virtualMachine = (ArbitrumVirtualMachine)VirtualMachine;
            HandleNormalTransactionEndTxHook(gasUsed, success, header, spec, _arbosState!, virtualMachine.ArbitrumTxExecutionContext);
        }

        private void HandleRetryTransactionEndTxHook(
            ArbitrumTransaction<ArbitrumRetryTx> retryTx,
            ulong gasLeft,
            ulong gasUsed,
            bool success,
            BlockHeader header,
            IReleaseSpec spec,
            ArbosState arbosState,
            ITxTracer tracer,
            ExecutionOptions opts)
        {
            var inner = retryTx.Inner;
            var effectiveBaseFee = ValidateAndGetEffectiveBaseFee(inner, header, opts);

            // Undo Nethermind's refund to the From address
            var gasRefund = effectiveBaseFee * gasLeft;
            UndoGasRefund(inner.From, gasRefund, tracer, spec);

            var maxRefund = inner.MaxRefund;
            var networkFeeAccount = arbosState.NetworkFeeAccount.Get();

            HandleSubmissionFeeRefund(inner, success, ref maxRefund, networkFeeAccount, spec);

            var gasCharge = effectiveBaseFee * gasUsed;
            TakeFunds(ref maxRefund, gasCharge);

            HandleGasRefunds(inner, effectiveBaseFee, gasLeft, ref maxRefund, arbosState, networkFeeAccount, spec);

            HandleRetryableLifecycle(inner, success, arbosState, spec);

            // Update gas pool with actual gas used
            arbosState.L2PricingState.AddToGasPool(-(long)gasUsed);
        }

        private UInt256 ValidateAndGetEffectiveBaseFee(ArbitrumRetryTx inner, BlockHeader header, ExecutionOptions opts)
        {
            var effectiveBaseFee = inner.GasFeeCap;

            // Only validate base fee match in real execution mode
            if (!opts.HasFlag(ExecutionOptions.SkipValidation) && effectiveBaseFee != header.BaseFeePerGas)
            {
                _logger.Error($"ArbitrumRetryTx GasFeeCap doesn't match basefee in commit mode: gasFeeCap={effectiveBaseFee}, baseFee={header.BaseFeePerGas}");
                effectiveBaseFee = header.BaseFeePerGas;
            }

            return effectiveBaseFee;
        }

        private void UndoGasRefund(Address fromAddress, UInt256 gasRefund, ITxTracer tracer, IReleaseSpec spec)
        {
            var currentBalance = WorldState.GetBalance(fromAddress);
            if (currentBalance < gasRefund)
            {
                _logger.Error($"Retry transaction from address doesn't have enough balance for gas refund: {fromAddress}, needed: {gasRefund}, available: {currentBalance}");
                return;
            }

            var beforeBalance = currentBalance;
            WorldState.SubtractFromBalance(fromAddress, gasRefund, spec);
            var afterBalance = WorldState.GetBalance(fromAddress);

            if (tracer?.IsTracingState == true)
            {
                tracer.ReportBalanceChange(fromAddress, beforeBalance, afterBalance);
            }
        }

        private void HandleSubmissionFeeRefund(ArbitrumRetryTx inner, bool success, ref UInt256 maxRefund, Address networkFeeAccount, IReleaseSpec spec)
        {
            if (success)
            {
                // If successful, refund the submission fee
                RefundFromAccount(networkFeeAccount, inner.SubmissionFeeRefund, ref maxRefund, inner, spec);
            }
            else
            {
                // Take submission fee from maxRefund even if we don't refund it
                TakeFunds(ref maxRefund, inner.SubmissionFeeRefund);
            }
        }

        private void HandleGasRefunds(ArbitrumRetryTx inner, UInt256 effectiveBaseFee, ulong gasLeft, ref UInt256 maxRefund, ArbosState arbosState, Address networkFeeAccount, IReleaseSpec spec)
        {
            var networkRefund = effectiveBaseFee * gasLeft;

            if (arbosState.CurrentArbosVersion >= ArbosVersion.Eleven)
            {
                var infraFeeAccount = arbosState.InfraFeeAccount.Get();
                if (infraFeeAccount != Address.Zero)
                {
                    var minBaseFee = arbosState.L2PricingState.MinBaseFeeWeiStorage.Get();
                    var infraFee = UInt256.Min(minBaseFee, effectiveBaseFee);
                    var infraRefund = infraFee * gasLeft;
                    infraRefund = TakeFunds(ref networkRefund, infraRefund);
                    RefundFromAccount(infraFeeAccount, infraRefund, ref maxRefund, inner, spec);
                }
            }

            RefundFromAccount(networkFeeAccount, networkRefund, ref maxRefund, inner, spec);
        }

        private void HandleRetryableLifecycle(ArbitrumRetryTx inner, bool success, ArbosState arbosState, IReleaseSpec spec)
        {
            if (success)
            {
                DeleteRetryable(inner.TicketId, arbosState, WorldState, spec);
                return;
            }

            // Return callvalue to escrow on failure
            var escrowAddress = GetRetryableEscrowAddress(inner.TicketId);
            var escrowResult = TransferBalance(inner.From, escrowAddress, inner.Value, arbosState, WorldState, spec);
            if (escrowResult != TransactionResult.Ok)
            {
                _logger.Error($"Failed to return callvalue to escrow: {escrowResult}");
            }
        }

        private void RefundFromAccount(Address refundFrom, UInt256 amount, ref UInt256 maxRefund, ArbitrumRetryTx inner, IReleaseSpec spec)
        {
            if (amount.IsZero) return;

            // Check if refundFrom account has sufficient balance
            var availableBalance = WorldState.GetBalance(refundFrom);
            if (availableBalance < amount)
            {
                if (_logger.IsError) _logger.Error($"fee address doesn't have enough funds to give user refund: available={availableBalance}, needed={amount}, address={refundFrom}");
                return;
            }

            var toRefundAddr = TakeFunds(ref maxRefund, amount);
            var remaining = amount - toRefundAddr;

            // Refund to RefundTo address (limited by L1 deposit)
            if (!toRefundAddr.IsZero)
            {
                var toRefundResult = TransferBalance(refundFrom, inner.RefundTo, toRefundAddr, _arbosState!, WorldState, spec);
                if (toRefundResult != TransactionResult.Ok)
                {
                    if (_logger.IsError) _logger.Error($"Failed to refund {toRefundAddr} from {refundFrom} to {inner.RefundTo}: {toRefundResult}");
                }
            }

            // Remaining goes to transaction sender
            if (!remaining.IsZero)
            {
                var toFromResult = TransferBalance(refundFrom, inner.From, remaining, _arbosState!, WorldState, spec);
                if (toFromResult != TransactionResult.Ok)
                {
                    if (_logger.IsError) _logger.Error($"Failed to refund remaining {remaining} from {refundFrom} to {inner.From}: {toFromResult}");
                }
            }
        }

        private void HandleNormalTransactionEndTxHook(
            ulong gasUsed,
            bool success,
            BlockHeader header,
            IReleaseSpec spec,
            ArbosState arbosState,
            ArbitrumTxExecutionContext txContext)
        {
            var baseFee = header.BaseFeePerGas;
            var totalCost = baseFee * gasUsed;

            UInt256 computeCost;
            if (totalCost < txContext.PosterFee)
            {
                if (_logger.IsError) _logger.Error($"Total cost < poster cost: gasUsed={gasUsed}, baseFee={baseFee}, posterFee={txContext.PosterFee}");
                txContext.PosterFee = UInt256.Zero;
                computeCost = totalCost;
            }
            else
            {
                computeCost = totalCost - txContext.PosterFee;
            }

            var networkFeeAccount = arbosState.NetworkFeeAccount.Get();

            // Fee processing happens regardless of success/failure for normal transactions
            // This matches Nitro's behavior where network gets paid even for failed transactions
            computeCost = HandleInfrastructureFee(computeCost, gasUsed, baseFee, arbosState, spec, txContext);
            if (!computeCost.IsZero)
            {
                WorldState.AddToBalanceAndCreateIfNotExists(networkFeeAccount, computeCost, spec);
            }

            // Poster fee and L1 tracking also happen regardless of success
            // The user paid for data posting costs whether transaction succeeded or failed
            HandlePosterFeeAndL1Tracking(arbosState, spec, txContext);

            if (!header.BaseFeePerGas.IsZero)
            {
                UpdateGasPool(gasUsed, arbosState, txContext);
            }

            // Note: For normal transactions, success/failure doesn't affect fee distribution
            // Unlike retry transactions which have complex refund logic based on success
        }

        private UInt256 HandleInfrastructureFee(UInt256 computeCost, ulong gasUsed, UInt256 baseFee, ArbosState arbosState, IReleaseSpec spec, ArbitrumTxExecutionContext txContext)
        {
            if (arbosState.CurrentArbosVersion < ArbosVersion.IntroduceInfraFees)
                return computeCost;

            var infraFeeAccount = arbosState.InfraFeeAccount.Get();
            if (infraFeeAccount == Address.Zero)
                return computeCost;

            var minBaseFee = arbosState.L2PricingState.MinBaseFeeWeiStorage.Get();
            var infraFee = UInt256.Min(minBaseFee, baseFee);
            var computeGas = gasUsed > txContext.PosterGas ? gasUsed - txContext.PosterGas : 0UL;
            var infraComputeCost = infraFee * computeGas;

            WorldState.AddToBalanceAndCreateIfNotExists(infraFeeAccount, infraComputeCost, spec);

            if (computeCost >= infraComputeCost)
            {
                return computeCost - infraComputeCost;
            }

            if (_logger.IsError) _logger.Error($"Compute cost < infra compute cost: computeCost={computeCost}, infraComputeCost={infraComputeCost}");
            return UInt256.Zero;
        }

        private void HandlePosterFeeAndL1Tracking(ArbosState arbosState, IReleaseSpec spec, ArbitrumTxExecutionContext txContext)
        {
            var posterFeeDestination = arbosState.CurrentArbosVersion < ArbosVersion.ChangePosterDestination
                ? VirtualMachine.BlockExecutionContext.Header.Beneficiary ?? Address.Zero
                : ArbosAddresses.L1PricerFundsPoolAddress;

            WorldState.AddToBalanceAndCreateIfNotExists(posterFeeDestination, txContext.PosterFee, spec);

            if (arbosState.CurrentArbosVersion >= ArbosVersion.L1FeesAvailable)
            {
                UpdateL1FeesAvailable(arbosState, txContext);
            }
        }

        private void UpdateL1FeesAvailable(ArbosState arbosState, ArbitrumTxExecutionContext txContext)
        {
            try
            {
                var currentL1Fees = arbosState.L1PricingState.L1FeesAvailableStorage.Get();
                arbosState.L1PricingState.L1FeesAvailableStorage.Set(currentL1Fees + txContext.PosterFee);
            }
            catch (Exception ex)
            {
                if (_logger.IsError) _logger.Error($"Failed to update L1FeesAvailable: {ex}");
            }
        }

        private void UpdateGasPool(ulong gasUsed, ArbosState arbosState, ArbitrumTxExecutionContext txContext)
        {
            var computeGas = gasUsed > txContext.PosterGas ? gasUsed - txContext.PosterGas : gasUsed;
            if (gasUsed <= txContext.PosterGas && gasUsed > 0 && _logger.IsError)
            {
                _logger.Error($"Total gas used < poster gas component: gasUsed={gasUsed}, posterGas={txContext.PosterGas}");
            }
            arbosState.L2PricingState.AddToGasPool(-(long)computeGas);
        }

        private static UInt256 TakeFunds(ref UInt256 pool, UInt256 take) =>
            take.IsZero ? UInt256.Zero :
            pool < take ? Exchange(ref pool, UInt256.Zero) :
            (pool -= take, take).take;

        private static UInt256 Exchange(ref UInt256 location, UInt256 value)
        {
            var oldValue = location;
            location = value;
            return oldValue;
        }
    }
}
