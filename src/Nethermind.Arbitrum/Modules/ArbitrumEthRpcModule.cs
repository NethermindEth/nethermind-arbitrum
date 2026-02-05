// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Core;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Rpc;
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
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Data;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;
using Nethermind.Logging;
using Nethermind.Network;
using Nethermind.Specs.Forks;
using Nethermind.State;
using Nethermind.TxPool;
using Nethermind.Wallet;

namespace Nethermind.Arbitrum.Modules
{
    [RpcModule(ModuleType.Eth)]
    public class ArbitrumEthRpcModule : EthRpcModule
    {
        private readonly ArbitrumChainSpecEngineParameters _chainSpecParams;

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
            IForkInfo forkInfo,
            ulong? secondsPerSlot,
            ArbitrumChainSpecEngineParameters chainSpecParams)
            : base(rpcConfig, blockchainBridge, blockFinder, receiptFinder, stateReader, txPool, txSender, wallet, logManager, specProvider, gasPriceOracle, ethSyncingInfo, feeHistoryOracle, protocolsManager, forkInfo, secondsPerSlot)
        {
            _chainSpecParams = chainSpecParams;
        }

        public override ResultWrapper<string> eth_call(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError && searchResult.Error != null)
            {
                return ResultWrapper<string>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            if (searchResult.Object == null)
            {
                return ResultWrapper<string>.Fail("Block not found", 0);
            }

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumCallTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, originalBaseFee, _chainSpecParams)
                .Execute(transactionCall, blockParameter, stateOverride, searchResult);
        }

        public override ResultWrapper<UInt256?> eth_estimateGas(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError && searchResult.Error != null)
            {
                return ResultWrapper<UInt256?>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            if (searchResult.Object == null)
            {
                return ResultWrapper<UInt256?>.Fail("Block not found", 0);
            }

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumEstimateGasTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, originalBaseFee, _chainSpecParams)
                .Execute(transactionCall, blockParameter, stateOverride, searchResult);
        }

        public override ResultWrapper<AccessListResultForRpc?> eth_createAccessList(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            bool optimize = true)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError && searchResult.Error != null)
            {
                return ResultWrapper<AccessListResultForRpc?>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            if (searchResult.Object == null)
            {
                return ResultWrapper<AccessListResultForRpc?>.Fail("Block not found", 0);
            }

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumCreateAccessListTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, originalBaseFee, _chainSpecParams, optimize)
                .Execute(transactionCall, blockParameter, null, searchResult);
        }

        public override ResultWrapper<ReceiptForRpc?> eth_getTransactionReceipt(Hash256 txHash)
        {
            (TxReceipt? receipt, ulong blockTimestamp, TxGasInfo? gasInfo, int logIndexStart) = _blockchainBridge.GetTxReceiptInfo(txHash);
            if (receipt is null || gasInfo is null)
                return ResultWrapper<ReceiptForRpc?>.Success(null);

            ArbitrumReceiptForRpc result = new(
                txHash,
                receipt,
                blockTimestamp,
                gasInfo.Value,
                logIndexStart);

            return ResultWrapper<ReceiptForRpc?>.Success(result);
        }

        public override ResultWrapper<ReceiptForRpc[]?> eth_getBlockReceipts(BlockParameter blockParameter)
        {
            SearchResult<Block> searchResult = _blockFinder.SearchForBlock(blockParameter);
            if (searchResult.IsError)
                return ResultWrapper<ReceiptForRpc[]?>.Success(null);

            Block block = searchResult.Object!;
            TxReceipt[] receipts = _receiptFinder.Get(block);
            IReleaseSpec spec = _specProvider.GetSpec(block.Header);

            ReceiptForRpc[] result = receipts
                .Zip(block.Transactions, (receipt, tx) =>
                    (ReceiptForRpc)new ArbitrumReceiptForRpc(
                        tx.Hash!,
                        receipt,
                        block.Timestamp,
                        tx.GetGasInfo(spec, block.Header),
                        receipts.GetBlockLogFirstIndex(receipt.Index)))
                .ToArray();

            return ResultWrapper<ReceiptForRpc[]?>.Success(result);
        }

        private abstract class ArbitrumTxExecutor<TResult>(
            IBlockchainBridge blockchainBridge,
            IBlockFinder blockFinder,
            IJsonRpcConfig rpcConfig,
            UInt256 originalBaseFee,
            ArbitrumChainSpecEngineParameters chainSpecParams)
            : ExecutorBase<TResult, TransactionForRpc, Transaction>(blockchainBridge, blockFinder, rpcConfig)
        {
            protected readonly UInt256 _originalBaseFee = originalBaseFee;
            protected readonly ArbitrumChainSpecEngineParameters _chainSpecParams = chainSpecParams;

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
                        transactionCall.Gas = searchResult.Value.Object?.GasLimit;
                    }
                }

                transactionCall.EnsureDefaults(_rpcConfig.GasCap);

                return base.Execute(transactionCall, blockParameter, stateOverride, searchResult);
            }

            protected override Result<Transaction> Prepare(TransactionForRpc call)
            {
                Result<Transaction> result = call.ToTransaction(validateUserInput: true);
                if (result.IsError)
                    return result;

                Transaction tx = result.Data;
                tx.ChainId = _blockchainBridge.GetChainId();
                return tx;
            }

            protected override ResultWrapper<TResult> Execute(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token)
            {
                // Create ArbitrumBlockHeader with original base fee
                ArbitrumBlockHeader arbitrumHeader = new(header, _originalBaseFee, (long)_chainSpecParams.GenesisBlockNum!);

                // Set base fee to 0 for EVM execution (like Ethereum's NoBaseFee)
                arbitrumHeader.BaseFeePerGas = 0;

                if (tx.IsContractCreation && tx.DataLength == 0)
                {
                    return ResultWrapper<TResult>.Fail("Contract creation without any data provided.", ErrorCodes.InvalidInput);
                }

                return ExecuteTx(arbitrumHeader, tx, stateOverride, token);
            }

            protected abstract ResultWrapper<TResult> ExecuteTx(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token);
        }

        private class ArbitrumCallTxExecutor(
            IBlockchainBridge blockchainBridge,
            IBlockFinder blockFinder,
            IJsonRpcConfig rpcConfig,
            UInt256 originalBaseFee,
            ArbitrumChainSpecEngineParameters chainSpecParams)
            : ArbitrumTxExecutor<string>(blockchainBridge, blockFinder, rpcConfig, originalBaseFee, chainSpecParams)
        {
            protected override ResultWrapper<string> ExecuteTx(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token)
            {
                CallOutput result = _blockchainBridge.Call(header, tx, stateOverride, token);

                return result switch
                {
                    { Error: null } => ResultWrapper<string>.Success(result.OutputData.ToHexString(true)),
                    { InputError: true } => ResultWrapper<string>.Fail(result.Error, ErrorCodes.InvalidInput),
                    _ => ResultWrapper<string>.Fail(result.Error, ErrorCodes.ExecutionError)
                };
            }
        }

        private class ArbitrumEstimateGasTxExecutor(
            IBlockchainBridge blockchainBridge,
            IBlockFinder blockFinder,
            IJsonRpcConfig rpcConfig,
            UInt256 originalBaseFee,
            ArbitrumChainSpecEngineParameters chainSpecParams)
            : ArbitrumTxExecutor<UInt256?>(blockchainBridge, blockFinder, rpcConfig, originalBaseFee, chainSpecParams)
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
            UInt256 originalBaseFee,
            ArbitrumChainSpecEngineParameters chainSpecParams,
            bool optimize = true)
            : ArbitrumTxExecutor<AccessListResultForRpc?>(blockchainBridge, blockFinder, rpcConfig, originalBaseFee, chainSpecParams)
        {
            protected override ResultWrapper<AccessListResultForRpc?> ExecuteTx(BlockHeader header, Transaction tx, Dictionary<Address, AccountOverride>? stateOverride, CancellationToken token)
            {
                CallOutput result = _blockchainBridge.CreateAccessList(header, tx, token, optimize);

                AccessListResultForRpc rpcAccessListResult = new(
                    accessList: AccessListForRpc.FromAccessList(result.AccessList ?? tx.AccessList),
                    gasUsed: GetResultGas(tx, result),
                    result.Error);

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
                if (result.AccessList is null)
                {
                    return (UInt256)gas;
                }

                var oldIntrinsicCost = IntrinsicGasCalculator.AccessListCost(transaction, Berlin.Instance);
                transaction.AccessList = result.AccessList;
                var newIntrinsicCost = IntrinsicGasCalculator.AccessListCost(transaction, Berlin.Instance);
                long updatedAccessListCost = newIntrinsicCost - oldIntrinsicCost;
                if (gas > operationGas)
                {
                    if (gas - operationGas < updatedAccessListCost)
                        gas = operationGas + updatedAccessListCost;
                }
                else
                {
                    gas += updatedAccessListCost;
                }

                return (UInt256)gas;
            }
        }
    }
}
