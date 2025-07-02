// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Tracing;
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
using Nethermind.Evm.CodeAnalysis;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumTransactionProcessor(
        ISpecProvider specProvider,
        IWorldState worldState,
        IVirtualMachine virtualMachine,
        IBlockTree blockTree,
        ILogManager logManager,
        ICodeInfoRepository? codeInfoRepository
    ) : TransactionProcessorBase(specProvider, worldState, virtualMachine,
        new ArbitrumCodeInfoRepository(codeInfoRepository), logManager)
    {
        protected override TransactionResult Execute(Transaction tx, ITxTracer tracer, ExecutionOptions opts)
        {
            Debug.Assert(tx is IArbitrumTransaction);

            var arbTxType = (ArbitrumTxType)tx.Type;

            BlockHeader header = VirtualMachine.BlockExecutionContext.Header;
            IReleaseSpec spec = GetSpec(tx, header);

            //TODO - need to establish what should be the correct flags to handle here
            bool restore = opts.HasFlag(ExecutionOptions.Restore);
            bool commit = opts.HasFlag(ExecutionOptions.Commit) ||
                          (!opts.HasFlag(ExecutionOptions.SkipValidation) && !spec.IsEip658Enabled);

            //do internal Arb transaction processing - logic of StartTxHook
            ArbitrumTransactionProcessorResult result =
                ProcessArbitrumTransaction(arbTxType, tx, in VirtualMachine.BlockExecutionContext,
                    tracer as IArbitrumTxTracer, spec);

            ArbitrumVirtualMachine virtualMachine = (ArbitrumVirtualMachine)VirtualMachine;
            virtualMachine.ArbitrumTxExecutionContext = new(
                result.CurrentRetryable,
                result.CurrentRefundTo
            );

            //if not doing any actual EVM, commit the changes and create receipt
            if (!result.ContinueProcessing)
            {
                if (commit)
                {
                    WorldState.Commit(spec, tracer.IsTracingState ? tracer : NullStateTracer.Instance,
                        commitRoots: !spec.IsEip658Enabled);
                }
                else if (restore)
                {
                    WorldState.Reset(resetBlockChanges: false);
                }

                if (tracer.IsTracingReceipt)
                {
                    Hash256? stateRoot = null;
                    if (!spec.IsEip658Enabled)
                    {
                        WorldState.RecalculateStateRoot();
                        stateRoot = WorldState.StateRoot;
                    }

                    if (result.InnerResult == TransactionResult.Ok)
                    {
                        tracer.MarkAsSuccess(tx.To!, tx.SpentGas, [], result.Logs, stateRoot);
                    }
                    else
                    {
                        tracer.MarkAsFailed(tx.To!, tx.SpentGas, [], result.InnerResult.ToString(), stateRoot);
                    }
                }

                return result.InnerResult;
            }

            if (ShouldDropTip() && tx.GasPrice > header.BaseFeePerGas)
            {
                tx.GasPrice = header.BaseFeePerGas;
                //how to set MaxPriorityFee to zero ? It's just set to GasPrice
                tx.DecodedMaxFeePerGas = header.BaseFeePerGas;
            }

            //Note that gas spent on arbitrum transactions pre-processing and the spent on EVM call are not cumulated
            //currently that is fine because only SubmitRetryable consumes gas and it is not further processed
            //TODO pass logs to base execution
            return base.Execute(tx, tracer, opts);
        }


        private ArbitrumTransactionProcessorResult ProcessArbitrumTransaction(ArbitrumTxType txType, Transaction tx,
            in BlockExecutionContext blCtx, IArbitrumTxTracer tracer, IReleaseSpec releaseSpec)
        {
            if (tracer.IsTracingActions)
            {
                tracer.ReportAction(0, tx.Value, tx.SenderAddress, tx.To, tx.Data, ExecutionType.CALL);
            }

            var executionEnv = new ExecutionEnvironment(CodeInfo.Empty, tx.SenderAddress, tx.To, tx.To, 0, tx.Value,
                tx.Value, tx.Data);
            var tracingInfo = new TracingInfo(tracer, TracingScenario.TracingDuringEvm, executionEnv);
            var burner = new SystemBurner(tracingInfo);
            try
            {
                switch (txType)
                {
                    case ArbitrumTxType.ArbitrumDeposit:
                        //TODO
                        break;
                    case ArbitrumTxType.ArbitrumInternal:
                        return ProcessArbitrumInternalTransaction(tx as ArbitrumTransaction<ArbitrumInternalTx>, in blCtx, tracer, releaseSpec, burner);
                    case ArbitrumTxType.ArbitrumSubmitRetryable:
                        return ProcessArbitrumSubmitRetryableTransaction(tx as ArbitrumTransaction<ArbitrumSubmitRetryableTx>, in blCtx, tracer, releaseSpec, burner);
                    case ArbitrumTxType.ArbitrumRetry:
                        return ProcessArbitrumRetryTransaction(tx as ArbitrumTransaction<ArbitrumRetryTx>, in blCtx, releaseSpec, tracer, burner);
                }
            }
            finally
            {
                if (tracer.IsTracingActions)
                {
                    tracer.ReportActionEnd((long)burner.Burned, Array.Empty<byte>());
                }
            }

            //nothing to processing internally, continue with EVM execution
            return new(true, TransactionResult.Ok);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumInternalTransaction(
            ArbitrumTransaction<ArbitrumInternalTx>? tx,
            in BlockExecutionContext blCtx, IArbitrumTxTracer tracer, IReleaseSpec releaseSpec, SystemBurner burner)
        {
            if (tx is null || tx.Data.Length < 4)
                return new(false, TransactionResult.MalformedTransaction);

            var methodId = tx.Data[..4];

            if (methodId.Span.SequenceEqual(AbiMetadata.StartBlockMethodId))
            {
                ArbosState arbosState =
                    ArbosState.OpenArbosState(WorldState, burner, logManager.GetClassLogger<ArbosState>());

                ValueHash256 prevHash = Keccak.Zero;
                if (blCtx.Header.Number > 0)
                {
                    prevHash = blockTree.FindHash(blCtx.Header.Number - 1);
                }

                if (arbosState.CurrentArbosVersion >= 40)
                {
                    ProcessParentBlockHash(prevHash, in blCtx, tracer);
                }

                var callArguments = AbiMetadata.UnpackInput(AbiMetadata.StartBlockMethod, tx.Data.ToArray()!);

                var l1BlockNumber = (ulong)callArguments["l1BlockNumber"];
                var timePassed = (ulong)callArguments["timePassed"];

                if (arbosState.CurrentArbosVersion < 3)
                {
                    // (incorrectly) use the L2 block number instead
                    timePassed = (ulong)callArguments["l2BlockNumber"];
                }

                if (arbosState.CurrentArbosVersion < 3)
                {
                    // in old versions we incorrectly used an L1 block number one too high
                    l1BlockNumber++;
                }

                var oldL1BlockNumber = arbosState.Blockhashes.GetL1BlockNumber();
                var l2BaseFee = arbosState.L2PricingState.BaseFeeWeiStorage.Get();

                if (l1BlockNumber > oldL1BlockNumber)
                {
                    arbosState.Blockhashes.RecordNewL1Block(l1BlockNumber + 1, prevHash,
                        arbosState.CurrentArbosVersion);
                }

                // Try to reap 2 retryables
                TryReapOneRetryable(arbosState, blCtx.Header.Timestamp, WorldState, releaseSpec, tracer,
                    TracingScenario.TracingDuringEvm);
                TryReapOneRetryable(arbosState, blCtx.Header.Timestamp, WorldState, releaseSpec, tracer,
                    TracingScenario.TracingDuringEvm);

                arbosState.L2PricingState.UpdatePricingModel(timePassed);

                arbosState.UpgradeArbosVersionIfNecessary(blCtx.Header.Timestamp, WorldState, releaseSpec);
                return new(false, TransactionResult.Ok);
            }

            return new(false, TransactionResult.Ok);
        }


        private ArbitrumTransactionProcessorResult ProcessArbitrumSubmitRetryableTransaction(
            ArbitrumTransaction<ArbitrumSubmitRetryableTx>? tx,
            in BlockExecutionContext blCtx,
            IArbitrumTxTracer tracer,
            IReleaseSpec releaseSpec,
            SystemBurner burner)
        {
            ArbitrumSubmitRetryableTx submitRetryableTx = (ArbitrumSubmitRetryableTx)tx.GetInner();

            var eventLogs = new List<LogEntry>(2);

            var escrowAddress = GetRetryableEscrowAddress(tx.Hash.ValueHash256);

            ArbosState arbosState =
                    ArbosState.OpenArbosState(worldState, burner, logManager.GetClassLogger<ArbosState>());

            var networkFeeAccount = arbosState.NetworkFeeAccount.Get();

            UInt256 availableRefund = submitRetryableTx.DepositValue;
            ConsumeAvailable(ref availableRefund, submitRetryableTx.RetryValue);

            MintBalance(tx.SenderAddress, submitRetryableTx.DepositValue, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm);

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
            if ((tr = TransferBalance(tx.SenderAddress, networkFeeAccount, submissionFee, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm)) != TransactionResult.Ok)
            {
                if (Logger.IsError) Logger.Error("Failed to transfer submission fee");
                return new(false, tr);
            }
            var withheldSubmissionFee = ConsumeAvailable(ref availableRefund, submissionFee);

            // refund excess submission fee
            var submissionFeeRefund =
                ConsumeAvailable(ref availableRefund, submitRetryableTx.MaxSubmissionFee - submissionFee);
            if (TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, submissionFeeRefund, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm) != TransactionResult.Ok)
            {
                if (Logger.IsError) Logger.Error("Failed to transfer submission fee refund");
            }

            // move the callvalue into escrow
            if ((tr = TransferBalance(tx.SenderAddress, escrowAddress, submitRetryableTx.RetryValue, arbosState,
                    worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm)) != TransactionResult.Ok)
            {
                var innerTr = TransactionResult.Ok;
                if ((innerTr = TransferBalance(networkFeeAccount, tx.SenderAddress, submissionFee, arbosState,
                        worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm)) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error("Failed to refund submissionFee");
                }
                if ((innerTr = TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, withheldSubmissionFee, arbosState,
                        worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm)) != TransactionResult.Ok)
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

            var tracingInfo = new TracingInfo(tracer, TracingScenario.TracingDuringEvm, null);
            var precompileExecutionContext = new ArbitrumPrecompileExecutionContext(Address.Zero, ArbRetryableTx.TicketCreatedEventGasCost(tx.Hash),
                false, worldState, blCtx, tx.ChainId ?? 0, tracingInfo, releaseSpec);

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
                if ((tr = TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, gasCostRefund, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm)) != TransactionResult.Ok)
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
                    if (TransferBalance(tx.SenderAddress, infraFeeAddress, infraCost, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm) != TransactionResult.Ok)
                    {
                        if (Logger.IsError) Logger.Error($"failed to transfer gas cost to infrastructure fee account {tr}");
                        return new(false, tr);
                    }
                }
            }

            if (networkCost > UInt256.Zero)
            {
                if (TransferBalance(tx.SenderAddress, networkFeeAccount, networkCost, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm) != TransactionResult.Ok)
                {
                    if (Logger.IsError) Logger.Error($"Failed to transfer gas cost to network fee account {tr}");
                    return new(false, tr);
                }
            }

            var withheldGasFunds = ConsumeAvailable(ref availableRefund, gasCost);
            var gasPriceRefund = (tx.MaxFeePerGas - effectiveBaseFee) * (ulong)tx.GasLimit;

            gasPriceRefund = ConsumeAvailable(ref availableRefund, gasPriceRefund);
            if (TransferBalance(tx.SenderAddress, submitRetryableTx.FeeRefundAddr, gasPriceRefund, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm) != TransactionResult.Ok)
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
            };
            retryable.IncrementNumTries();

            outerRetryTx.Hash = outerRetryTx.CalculateHash();
            precompileExecutionContext = new ArbitrumPrecompileExecutionContext(Address.Zero,
                ArbRetryableTx.RedeemScheduledEventGasCost(tx.Hash, outerRetryTx.Hash,
                    retryInnerTx.Nonce, (ulong)userGas, submitRetryableTx.FeeRefundAddr, availableRefund, submissionFee),
                false, worldState, blCtx, tx.ChainId ?? 0, tracingInfo, releaseSpec);

            ArbRetryableTx.EmitRedeemScheduledEvent(precompileExecutionContext, tx.Hash, outerRetryTx.Hash,
                retryInnerTx.Nonce, (ulong)userGas, submitRetryableTx.FeeRefundAddr, availableRefund, submissionFee);
            eventLogs.AddRange(precompileExecutionContext.EventLogs);

            //set spend gas to be reflected in receipt
            tx.SpentGas = userGas;

            if (tracer.IsTracingActions)
            {
                var redeem = ArbRetryableTx.PackArbRetryableTxRedeem(tx.Hash);
                tracingInfo.MockCall(tx.SenderAddress, ArbosAddresses.ArbRetryableTxAddress, 0, userGas, redeem);
            }

            //TODO Add tracer call
            return new(false, TransactionResult.Ok) { Logs = eventLogs.ToArray() };
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumRetryTransaction(ArbitrumTransaction<ArbitrumRetryTx>? tx,
            in BlockExecutionContext blCtx, IReleaseSpec releaseSpec, IArbitrumTxTracer tracer, SystemBurner burner)
        {
            if (tx is null)
                return new(false, TransactionResult.MalformedTransaction);

            ArbosState arbosState =
                    ArbosState.OpenArbosState(worldState, burner, logManager.GetClassLogger<ArbosState>());

            Retryable? retryable = arbosState.RetryableState.OpenRetryable(tx.Inner.TicketId, blCtx.Header.Timestamp);
            if (retryable is null)
            {
                return new(false, new TransactionResult($"Retryable with ticketId: {tx.Inner.TicketId} not found"));
            }

            // Transfer callvalue from escrow
            Address escrowAddress = GetRetryableEscrowAddress(tx.Inner.TicketId);
            TransactionResult transfer = TransferBalance(escrowAddress, tx.SenderAddress, tx.Value, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm);
            if (transfer != TransactionResult.Ok)
            {
                return new(false, transfer);
            }

            // The redeemer has pre-paid for this tx's gas
            UInt256 prepaid = blCtx.Header.BaseFeePerGas * (ulong)tx.GasLimit;
            TransactionResult mint = TransferBalance(null, tx.SenderAddress, prepaid, arbosState, worldState, releaseSpec, tracer, TracingScenario.TracingDuringEvm);
            if (mint != TransactionResult.Ok)
            {
                return new(false, mint);
            }

            //TODO: return true here when base tx processor can handle it (for now return false for tests)
            return new(false, TransactionResult.Ok)
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

        private void TryReapOneRetryable(ArbosState arbosState, ulong currentTimeStamp, IWorldState worldState,
            IReleaseSpec releaseSpec, IArbitrumTxTracer tracer, TracingScenario scenario)
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
                DeleteRetryable(id, arbosState, worldState, releaseSpec, tracer, scenario);
            }

            retryable.Timeout.Set(timeout + Retryable.RetryableLifetimeSeconds);
            retryable.TimeoutWindowsLeft.Set(windowsLeft - 1);
        }

        public static bool DeleteRetryable(ValueHash256 id, ArbosState arbosState, IWorldState worldState,
            IReleaseSpec releaseSpec, IArbitrumTxTracer tracer, TracingScenario scenario)
        {
            var retryable = arbosState.RetryableState.GetRetryable(id);

            if (retryable.Timeout.Get() == 0)
                return false;

            var escrowAddress = GetRetryableEscrowAddress(id);
            var beneficiaryAddress = retryable.Beneficiary.Get();
            var amount = worldState.GetBalance(escrowAddress);

            var tr = TransferBalance(escrowAddress, beneficiaryAddress, amount, arbosState, worldState, releaseSpec,
                tracer, scenario);
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
        private static TransactionResult TransferBalance(Address? from, Address? to, UInt256 amount,
            ArbosState arbosState,
            IWorldState worldState, IReleaseSpec releaseSpec, IArbitrumTxTracer tracer, TracingScenario scenario)
        {
            if (tracer.IsTracing)
            {
                if (scenario != TracingScenario.TracingDuringEvm)
                {
                    tracer.CaptureArbitrumTransfer(from, to, amount,
                        scenario == TracingScenario.TracingBeforeEvm, "not supported yet");
                }
                else
                {
                    var tracingInfo = new TracingInfo(tracer, scenario, null);
                    tracingInfo.MockCall(from ?? Address.Zero, to ?? Address.Zero, amount, 0, []);
                }
            }

            if (from is not null)
            {
                var balance = worldState.GetBalance(from);
                if (balance < amount)
                    return TransactionResult.InsufficientSenderBalance;
                if (arbosState.CurrentArbosVersion < 30 && amount == UInt256.Zero)
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
            IReleaseSpec releaseSpec, IArbitrumTxTracer tracer, TracingScenario scenario)
        {
            TransferBalance(null, to, amount, arbosState, worldState, releaseSpec, tracer, scenario);
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
    }
}