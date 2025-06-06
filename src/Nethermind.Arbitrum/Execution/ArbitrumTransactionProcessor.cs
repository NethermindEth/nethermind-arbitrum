// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.State.Tracing;
using System.Diagnostics;
using System.Text.Json;

namespace Nethermind.Arbitrum.Execution
{
    internal class ArbitrumTransactionProcessor(
        ISpecProvider specProvider,
        IWorldState worldState,
        IVirtualMachine virtualMachine,
        IBlockTree blockTree,
        ILogManager logManager,
        ICodeInfoRepository? codeInfoRepository
    ) : TransactionProcessorBase(specProvider, worldState, virtualMachine, codeInfoRepository, logManager)
    {
        private static readonly byte[] InternalTxStartBlockMethodId = Bytes.FromHexString("0x6bf6a42d");

        //TODO: move to a common precompile place
        public static readonly string ABIMetadata =
            "[{\"inputs\":[],\"name\":\"CallerNotArbOS\",\"type\":\"error\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"batchTimestamp\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"batchPosterAddress\",\"type\":\"address\"},{\"internalType\":\"uint64\",\"name\":\"batchNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"batchDataGas\",\"type\":\"uint64\"},{\"internalType\":\"uint256\",\"name\":\"l1BaseFeeWei\",\"type\":\"uint256\"}],\"name\":\"batchPostingReport\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"uint256\",\"name\":\"l1BaseFee\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"l1BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"l2BlockNumber\",\"type\":\"uint64\"},{\"internalType\":\"uint64\",\"name\":\"timePassed\",\"type\":\"uint64\"}],\"name\":\"startBlock\",\"outputs\":[],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}]";


        protected override TransactionResult Execute(Transaction tx, in BlockExecutionContext blCtx, ITxTracer tracer,
            ExecutionOptions opts)
        {
            Debug.Assert(tx is IArbitrumTransaction);

            var arbTxType = (ArbitrumTxType)tx.Type;


            BlockHeader header = blCtx.Header;
            IReleaseSpec spec = GetSpec(tx, header);

            //TODO - need to establish what should be the correct flags to handle here
            bool restore = opts.HasFlag(ExecutionOptions.Restore);
            bool commit = opts.HasFlag(ExecutionOptions.Commit) || (!opts.HasFlag(ExecutionOptions.SkipValidation) && !spec.IsEip658Enabled);

            //do internal Arb transaction processing - logic from of StartTxHook
            (bool continueProcessing, TransactionResult innerResult) = ProcessArbitrumTransaction(arbTxType, tx, in blCtx);

            //if not doing any actuall EVM, commit the changes and create receipt
            if (!continueProcessing)
            {
                if (commit)
                {
                    WorldState.Commit(spec, tracer.IsTracingState ? tracer : NullStateTracer.Instance, commitRoots: !spec.IsEip658Enabled);
                }

                if (tracer.IsTracingReceipt)
                {
                    Hash256 stateRoot = null;
                    if (!spec.IsEip658Enabled)
                    {
                        WorldState.RecalculateStateRoot();
                        stateRoot = WorldState.StateRoot;
                    }

                    if (innerResult == TransactionResult.Ok)
                    {
                        tracer.MarkAsSuccess(tx.To, 0, [], [], stateRoot);
                    }
                    else
                    {
                        tracer.MarkAsFailed(tx.To, 0, [], innerResult.ToString(), stateRoot);
                    }
                }
                return innerResult;
            }
            else
            {
                return base.Execute(tx, in blCtx, tracer, opts);
            }
        }

        private (bool, TransactionResult) ProcessArbitrumTransaction(ArbitrumTxType txType, Transaction tx, in BlockExecutionContext blCtx)
        {
            switch (txType)
            {
                case ArbitrumTxType.ArbitrumDeposit:
                    //TODO
                    break;
                case ArbitrumTxType.ArbitrumInternal:

                    if (tx.SenderAddress != ArbosAddresses.ArbosAddress)
                        return (false, TransactionResult.SenderNotSpecified);

                    return ProcessArbitrumInternalTransaction(tx as ArbitrumTransaction<ArbitrumInternalTx>, in blCtx);
                case ArbitrumTxType.ArbitrumSubmitRetryable:
                    //TODO
                    break;
                case ArbitrumTxType.ArbitrumRetry:
                    //TODO
                    break;
            }

            //nothing to processing internally, continue with EVM execution
            return (true, TransactionResult.Ok);
        }

        private (bool, TransactionResult) ProcessArbitrumInternalTransaction(ArbitrumTransaction<ArbitrumInternalTx>? tx,
            in BlockExecutionContext blCtx)
        {
            if (tx is null || tx.Data?.Length < 4)
                return (false, TransactionResult.MalformedTransaction);

            var methodId = tx.Data?[..4];

            SystemBurner burner = new(readOnly: false);

            if (methodId.Value.Span.SequenceEqual(InternalTxStartBlockMethodId))
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
                    //core.ProcessParentBlockHash(prevHash, evm)
                }

                var callArguments = UnpackInput(ABIMetadata, "startBlock", tx.Data?.ToArray());

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
                    arbosState.Blockhashes.RecordNewL1Block(l1BlockNumber + 1, prevHash, arbosState.CurrentArbosVersion);
                }

                //TODO retryables

                arbosState.L2PricingState.UpdatePricingModel(timePassed);

                //TODO how to call with params?
                //arbosState.UpgradeArbosVersionIfNecessary();
                return (false, TransactionResult.Ok);
            }

            return (false, TransactionResult.Ok);
        }

        private Dictionary<string, object> UnpackInput(string abiJson, string methodName, byte[] rawData)
        {
            AbiEncoder abiEncoder = new AbiEncoder();

            if (rawData.Length <= 4)
                throw new ArgumentException("Input data too short");

            JsonSerializerOptions jso = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var functions = JsonSerializer.Deserialize<List<ArbAbiFunction>>(abiJson, jso);
            var target = functions.FirstOrDefault(f => f.Name == methodName);
            if (target == null)
                throw new Exception($"Function '{methodName}' not found in ABI");

            AbiSignature signature = new AbiSignature(methodName, target.Inputs.Select(i => i.Type).ToArray());

            var arguments = abiEncoder.Decode(AbiEncodingStyle.None, signature, rawData[4..]);

            var result = new Dictionary<string, object>();
            for (int i = 0; i < target.Inputs.Length; i++)
            {
                result[target.Inputs[i].Name] = arguments[i];
            }

            return result;
        }

        public static byte[] PackInput(string abiJson, string methodName, params object[] arguments)
        {
            AbiEncoder abiEncoder = new AbiEncoder();

            JsonSerializerOptions jso = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var functions = JsonSerializer.Deserialize<List<ArbAbiFunction>>(abiJson, jso);
            var target = functions.FirstOrDefault(f => f.Name == methodName);
            if (target == null)
                throw new Exception($"Function '{methodName}' not found in ABI");

            AbiSignature signature = new AbiSignature(methodName, target.Inputs.Select(i => i.Type).ToArray());

            var bytes = abiEncoder.Encode(AbiEncodingStyle.IncludeSignature, signature, arguments);

            return bytes;
        }

        private class ArbAbiFunction
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public ArbAbiParameter[] Inputs { get; set; }
        }

        private class ArbAbiParameter
        {
            public string Name { get; set; }
            public AbiType Type { get; set; }
        }
    }
}