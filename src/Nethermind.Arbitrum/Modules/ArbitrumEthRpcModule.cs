// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain.Find;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Facade;
using Nethermind.Facade.Eth;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Facade.Filters;
using Nethermind.Facade.Proxy.Models.Simulate;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Data;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;
using Nethermind.Logging;
using Nethermind.Network;
using Nethermind.State;
using Nethermind.State.Proofs;
using Nethermind.Specs.Forks;
using Nethermind.TxPool;
using Nethermind.Wallet;

namespace Nethermind.Arbitrum.Modules
{
    [RpcModule(ModuleType.Eth)]
    public class ArbitrumEthRpcModule : EthRpcModule
    {
        private readonly ArbitrumVirtualMachine _arbitrumVM;

        public ArbitrumEthRpcModule(
            IJsonRpcConfig rpcConfig,
            IBlockchainBridge blockchainBridge,
            IBlockFinder blockFinder,
            IReceiptFinder receiptFinder,
            IStateReader stateReader,
            ITxPool txPool,
            ITxSender txSender,
            IWallet wallet,
            ILogManager logManager,
            ISpecProvider specProvider,
            IGasPriceOracle gasPriceOracle,
            IEthSyncingInfo ethSyncingInfo,
            IFeeHistoryOracle feeHistoryOracle,
            IProtocolsManager protocolsManager,
            ArbitrumVirtualMachine arbitrumVM,
            ulong? secondsPerSlot)
            : base(rpcConfig, blockchainBridge, blockFinder, receiptFinder, stateReader,
                   txPool, txSender, wallet, logManager, specProvider, gasPriceOracle,
                   ethSyncingInfo, feeHistoryOracle, protocolsManager, secondsPerSlot)
        {
            _arbitrumVM = arbitrumVM;
        }

        public override ResultWrapper<string> eth_call(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError)
            {
                return ResultWrapper<string>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            UInt256? originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumCallTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, _arbitrumVM, originalBaseFee)
                .Execute(transactionCall, blockParameter, stateOverride, searchResult);
        }

        public override ResultWrapper<UInt256?> eth_estimateGas(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError)
            {
                return ResultWrapper<UInt256?>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            UInt256? originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumEstimateGasTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, _arbitrumVM, originalBaseFee)
                .Execute(transactionCall, blockParameter, stateOverride, searchResult);
        }

        public override ResultWrapper<AccessListResultForRpc?> eth_createAccessList(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            bool optimize = true)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError)
            {
                return ResultWrapper<AccessListResultForRpc?>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            UInt256? originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumCreateAccessListTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, _arbitrumVM, originalBaseFee, optimize)
                .Execute(transactionCall, blockParameter, stateOverride, searchResult);
        }

        private abstract class ArbitrumTxExecutor<TResult>(
            IBlockchainBridge blockchainBridge,
            IBlockFinder blockFinder,
            IJsonRpcConfig rpcConfig,
            ArbitrumVirtualMachine arbitrumVM,
            UInt256? originalBaseFee)
            : ExecutorBase<TResult, TransactionForRpc, Transaction>(blockchainBridge, blockFinder, rpcConfig)
        {
            protected readonly UInt256? _originalBaseFee = originalBaseFee;
            protected readonly ArbitrumVirtualMachine _arbitrumVM = arbitrumVM;

            public override ResultWrapper<TResult> Execute(
                TransactionForRpc transactionCall,
                BlockParameter? blockParameter,
                Dictionary<Address, AccountOverride>? stateOverride = null,
                SearchResult<BlockHeader>? searchResult = null)
            {
                if (transactionCall.Gas is null)
                {
                    searchResult ??= _blockFinder.SearchForHeader(blockParameter);
                    if (!searchResult.Value.IsError)
                    {
                        transactionCall.Gas = searchResult.Value.Object.GasLimit;
                    }
                }

                transactionCall.EnsureDefaults(_rpcConfig.GasCap);

                return base.Execute(transactionCall, blockParameter, stateOverride, searchResult);
            }

            protected override Transaction Prepare(TransactionForRpc call)
            {
                var tx = call.ToTransaction();
                tx.ChainId = _blockchainBridge.GetChainId();

                return tx;
            }

            protected override ResultWrapper<TResult> Execute(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token)
            {
                BlockHeader clonedHeader = header.Clone();

                if (tx.IsContractCreation && tx.DataLength == 0)
                {
                    return ResultWrapper<TResult>.Fail("Contract creation without any data provided.", ErrorCodes.InvalidInput);
                }

                if (_originalBaseFee.HasValue)
                {
                    using var noBaseFeeScope = _arbitrumVM.UseNoBaseFee(_originalBaseFee.Value);
                    return ExecuteTx(clonedHeader, tx, stateOverride, token);
                }

                return ExecuteTx(clonedHeader, tx, stateOverride, token);
            }

            protected abstract ResultWrapper<TResult> ExecuteTx(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token);
        }

        private class ArbitrumCallTxExecutor(
            IBlockchainBridge blockchainBridge,
            IBlockFinder blockFinder,
            IJsonRpcConfig rpcConfig,
            ArbitrumVirtualMachine arbitrumVM,
            UInt256? originalBaseFee = null)
            : ArbitrumTxExecutor<string>(blockchainBridge, blockFinder, rpcConfig, arbitrumVM, originalBaseFee)
        {
            protected override ResultWrapper<string> ExecuteTx(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token)
            {
                CallOutput result = _blockchainBridge.Call(header, tx, stateOverride, token);

                return result.Error is null
                    ? ResultWrapper<string>.Success(result.OutputData.ToHexString(true))
                    : TryGetInputError(result) ?? ResultWrapper<string>.Fail("VM execution error.", ErrorCodes.ExecutionError, result.Error);
            }
        }

        private class ArbitrumEstimateGasTxExecutor(
            IBlockchainBridge blockchainBridge,
            IBlockFinder blockFinder,
            IJsonRpcConfig rpcConfig,
            ArbitrumVirtualMachine arbitrumVM,
            UInt256? originalBaseFee = null)
            : ArbitrumTxExecutor<UInt256?>(blockchainBridge, blockFinder, rpcConfig, arbitrumVM, originalBaseFee)
        {
            private readonly int _errorMargin = rpcConfig.EstimateErrorMargin;

            protected override ResultWrapper<UInt256?> ExecuteTx(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token)
            {
                CallOutput result = _blockchainBridge.EstimateGas(header, tx, _errorMargin, stateOverride, token);

                return result switch
                {
                    { Error: null } => ResultWrapper<UInt256?>.Success((UInt256)result.GasSpent),
                    { InputError: true } => ResultWrapper<UInt256?>.Fail(result.Error, ErrorCodes.InvalidInput),
                    _ => ResultWrapper<UInt256?>.Fail(result.Error, ErrorCodes.ExecutionError)
                };
            }
        }

        private class ArbitrumCreateAccessListTxExecutor(
            IBlockchainBridge blockchainBridge,
            IBlockFinder blockFinder,
            IJsonRpcConfig rpcConfig,
            ArbitrumVirtualMachine arbitrumVM,
            UInt256? originalBaseFee = null,
            bool optimize = true)
            : ArbitrumTxExecutor<AccessListResultForRpc?>(blockchainBridge, blockFinder, rpcConfig, arbitrumVM, originalBaseFee)
        {
            protected override ResultWrapper<AccessListResultForRpc?> ExecuteTx(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token)
            {
                CallOutput result = _blockchainBridge.CreateAccessList(header, tx, token, optimize);

                var rpcAccessListResult = new AccessListResultForRpc(
                    accessList: AccessListForRpc.FromAccessList(result.AccessList ?? tx.AccessList),
                    gasUsed: GetResultGas(tx, result));

                return result switch
                {
                    { Error: null } => ResultWrapper<AccessListResultForRpc?>.Success(rpcAccessListResult),
                    { InputError: true } => ResultWrapper<AccessListResultForRpc?>.Fail(result.Error, ErrorCodes.InvalidInput),
                    _ => ResultWrapper<AccessListResultForRpc?>.Fail(result.Error, ErrorCodes.ExecutionError),
                };
            }

            private static UInt256 GetResultGas(Transaction transaction, CallOutput result)
            {
                long gas = result.GasSpent;
                long operationGas = result.OperationGas;
                if (result.AccessList is not null)
                {
                    var oldIntrinsicCost = IntrinsicGasCalculator.AccessListCost(transaction, Berlin.Instance);
                    transaction.AccessList = result.AccessList;
                    var newIntrinsicCost = IntrinsicGasCalculator.AccessListCost(transaction, Berlin.Instance);
                    long updatedAccessListCost = newIntrinsicCost - oldIntrinsicCost;
                    if (gas > operationGas)
                    {
                        if (gas - operationGas < updatedAccessListCost) gas = operationGas + updatedAccessListCost;
                    }
                    else
                    {
                        gas += updatedAccessListCost;
                    }
                }

                return (UInt256)gas;
            }
        }
    }
}
