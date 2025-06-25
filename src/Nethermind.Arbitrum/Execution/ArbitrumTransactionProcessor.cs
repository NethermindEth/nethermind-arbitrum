// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
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
        public Hash256? CurrentRetryable { get; set; }
        public Address? CurrentRefundTo { get; set; }

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
            //TODO: track gas spent
            ArbitrumTransactionProcessorResult result =
                ProcessArbitrumTransaction(arbTxType, tx, in VirtualMachine.BlockExecutionContext, tracer, spec);

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
                        tracer.MarkAsSuccess(tx.To!, 0, [], result.Logs, stateRoot);
                    }
                    else
                    {
                        tracer.MarkAsFailed(tx.To!, 0, [], result.InnerResult.ToString(), stateRoot);
                    }
                }

                return result.InnerResult;
            }

            //TODO pass logs to base execution
            return base.Execute(tx, tracer, opts);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumTransaction(ArbitrumTxType txType, Transaction tx,
            in BlockExecutionContext blCtx, ITxTracer tracer, IReleaseSpec releaseSpec)
        {
            switch (txType)
            {
                case ArbitrumTxType.ArbitrumDeposit:
                    //TODO
                    break;
                case ArbitrumTxType.ArbitrumInternal:

                    if (tx.SenderAddress != ArbosAddresses.ArbosAddress)
                        return new(false, TransactionResult.SenderNotSpecified);

                    return ProcessArbitrumInternalTransaction(tx as ArbitrumTransaction<ArbitrumInternalTx>, in blCtx, tracer, releaseSpec);
                case ArbitrumTxType.ArbitrumSubmitRetryable:
                    //TODO
                    break;
                case ArbitrumTxType.ArbitrumRetry:
                    return ProcessArbitrumRetryTransaction(tx as ArbitrumTransaction<ArbitrumRetryTx>, in blCtx, releaseSpec);
            }

            //nothing to processing internally, continue with EVM execution
            return new(true, TransactionResult.Ok);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumInternalTransaction(
            ArbitrumTransaction<ArbitrumInternalTx>? tx,
            in BlockExecutionContext blCtx, ITxTracer tracer, IReleaseSpec releaseSpec)
        {
            if (tx is null || tx.Data.Length < 4)
                return new(false, TransactionResult.MalformedTransaction);

            var methodId = tx.Data[..4];

            SystemBurner burner = new(readOnly: false);

            if (methodId.Span.SequenceEqual(AbiMetadata.StartBlockMethodId))
            {
                ArbosState arbosState =
                    ArbosState.OpenArbosState(worldState, burner, logManager.GetClassLogger<ArbosState>());

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
                TryReapOneRetryable(arbosState, blCtx.Header.Timestamp, worldState, releaseSpec);
                TryReapOneRetryable(arbosState, blCtx.Header.Timestamp, worldState, releaseSpec);

                arbosState.L2PricingState.UpdatePricingModel(timePassed);

                arbosState.UpgradeArbosVersionIfNecessary(blCtx.Header.Timestamp, worldState, releaseSpec);
                return new(false, TransactionResult.Ok);
            }

            return new(false, TransactionResult.Ok);
        }

        private ArbitrumTransactionProcessorResult ProcessArbitrumRetryTransaction(ArbitrumTransaction<ArbitrumRetryTx>? tx,
            in BlockExecutionContext blCtx, IReleaseSpec releaseSpec)
        {
            if (tx is null) return new(false, TransactionResult.MalformedTransaction);

            SystemBurner burner = new(readOnly: false);
            ArbosState arbosState =
                    ArbosState.OpenArbosState(worldState, burner, logManager.GetClassLogger<ArbosState>());

            Retryable? retryable = arbosState.RetryableState.OpenRetryable(tx.Inner.TicketId, blCtx.Header.Timestamp);
            if (retryable is null)
            {
                return new(false, new TransactionResult($"Retryable with ticketId: {tx!.Inner.TicketId} not found"));
            }

            // Transfer callvalue from escrow
            Address escrowAddress = GetRetryableEscrowAddress(tx.Inner.TicketId);
            TransactionResult transfer = TransferBalance(escrowAddress, tx.SenderAddress, tx.Value, arbosState, worldState, releaseSpec);
            if (transfer != TransactionResult.Ok)
            {
                return new(false, transfer);
            }

    		// The redeemer has pre-paid for this tx's gas
            UInt256 gasLimit = new((ulong)tx.GasLimit);
            UInt256.Multiply(in blCtx.Header.BaseFeePerGas, in gasLimit, out UInt256 prepaid);
            TransactionResult mint = TransferBalance(null, tx.SenderAddress, prepaid, arbosState, worldState, releaseSpec);
            if (mint != TransactionResult.Ok)
            {
                return new(false, mint);
            }

            CurrentRetryable = tx.Inner.TicketId;
            CurrentRefundTo = tx.Inner.RefundTo;

            //TODO: return true here when base tx processor can handle it (for now return false for tests)
            return new(false, TransactionResult.Ok);
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

        public static Address GetRetryableEscrowAddress(ValueHash256 hash)
        {
            var staticBytes = "retryable escrow"u8.ToArray();
            Span<byte> workingSpan = stackalloc byte[staticBytes.Length + Keccak.Size];
            staticBytes.CopyTo(workingSpan);
            hash.Bytes.CopyTo(workingSpan.Slice(staticBytes.Length));
            return new Address(Keccak.Compute(workingSpan).Bytes.Slice(Keccak.Size - Address.Size));
        }

        private record ArbitrumTransactionProcessorResult(
            bool ContinueProcessing,
            TransactionResult InnerResult)
        {
            public LogEntry[] Logs { get; init; } = [];
        }
    }
}
