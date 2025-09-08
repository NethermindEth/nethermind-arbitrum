// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json.Nodes;
using Nethermind.Arbitrum.Core;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Facade.Eth;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Facade.Filters;
using Nethermind.Facade.Proxy.Models.Simulate;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Data;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.State.Proofs;
using Nethermind.Evm;
using Nethermind.Specs.Forks;
using Nethermind.Facade;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum.Modules
{
    [RpcModule(ModuleType.Arbitrum)]
    public class ArbitrumEthRpcModule(
        IEthRpcModule baseModule,
        IBlockchainBridge blockchainBridge,
        IBlockFinder blockFinder,
        IJsonRpcConfig rpcConfig)
        : IEthRpcModule
    {
        private readonly IEthRpcModule _baseModule = baseModule ?? throw new ArgumentNullException(nameof(baseModule));
        private readonly IBlockchainBridge _blockchainBridge = blockchainBridge ?? throw new ArgumentNullException(nameof(blockchainBridge));
        private readonly IBlockFinder _blockFinder = blockFinder ?? throw new ArgumentNullException(nameof(blockFinder));
        private readonly IJsonRpcConfig _rpcConfig = rpcConfig ?? throw new ArgumentNullException(nameof(rpcConfig));

        public ResultWrapper<string> eth_call(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError)
            {
                return ResultWrapper<string>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumCallTxExecutor(_blockchainBridge, _rpcConfig, originalBaseFee)
                .Execute(transactionCall, stateOverride, searchResult);
        }

        public ResultWrapper<UInt256?> eth_estimateGas(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError)
            {
                return ResultWrapper<UInt256?>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumEstimateGasTxExecutor(_blockchainBridge, _rpcConfig, originalBaseFee)
                .Execute(transactionCall, stateOverride, searchResult);
        }

        public ResultWrapper<AccessListResultForRpc?> eth_createAccessList(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            bool optimize = true)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError)
            {
                return ResultWrapper<AccessListResultForRpc?>.Fail(searchResult.Error, searchResult.ErrorCode);
            }

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumCreateAccessListTxExecutor(_blockchainBridge, _rpcConfig, originalBaseFee, optimize)
                .Execute(transactionCall, null, searchResult);
        }

        public ResultWrapper<string> eth_protocolVersion() => _baseModule.eth_protocolVersion();
        public ResultWrapper<SyncingResult> eth_syncing() => _baseModule.eth_syncing();
        public ResultWrapper<byte[]> eth_snapshot() => _baseModule.eth_snapshot();
        public ResultWrapper<Address> eth_coinbase() => _baseModule.eth_coinbase();
        public Task<ResultWrapper<UInt256?>> eth_gasPrice() => _baseModule.eth_gasPrice();
        public ResultWrapper<UInt256?> eth_blobBaseFee() => _baseModule.eth_blobBaseFee();
        public ResultWrapper<UInt256?> eth_maxPriorityFeePerGas() => _baseModule.eth_maxPriorityFeePerGas();
        public ResultWrapper<FeeHistoryResults> eth_feeHistory(int blockCount, BlockParameter newestBlock, double[]? rewardPercentiles = null)
            => _baseModule.eth_feeHistory(blockCount, newestBlock, rewardPercentiles);
        public ResultWrapper<IEnumerable<Address>> eth_accounts() => _baseModule.eth_accounts();
        public Task<ResultWrapper<long?>> eth_blockNumber() => _baseModule.eth_blockNumber();
        public Task<ResultWrapper<UInt256?>> eth_getBalance(Address address, BlockParameter? blockParameter = null)
            => _baseModule.eth_getBalance(address, blockParameter);
        public ResultWrapper<byte[]> eth_getStorageAt(Address address, UInt256 positionIndex, BlockParameter? blockParameter = null)
            => _baseModule.eth_getStorageAt(address, positionIndex, blockParameter);
        public Task<ResultWrapper<UInt256>> eth_getTransactionCount(Address address, BlockParameter? blockParameter)
            => _baseModule.eth_getTransactionCount(address, blockParameter);
        public ResultWrapper<UInt256?> eth_getBlockTransactionCountByHash(Hash256 blockHash)
            => _baseModule.eth_getBlockTransactionCountByHash(blockHash);
        public ResultWrapper<UInt256?> eth_getBlockTransactionCountByNumber(BlockParameter blockParameter)
            => _baseModule.eth_getBlockTransactionCountByNumber(blockParameter);
        public ResultWrapper<UInt256?> eth_getUncleCountByBlockHash(Hash256 blockHash)
            => _baseModule.eth_getUncleCountByBlockHash(blockHash);
        public ResultWrapper<UInt256?> eth_getUncleCountByBlockNumber(BlockParameter blockParameter)
            => _baseModule.eth_getUncleCountByBlockNumber(blockParameter);
        public ResultWrapper<byte[]> eth_getCode(Address address, BlockParameter? blockParameter = null)
            => _baseModule.eth_getCode(address, blockParameter);
        public ResultWrapper<string> eth_sign(Address addressData, byte[] message)
            => _baseModule.eth_sign(addressData, message);
        public Task<ResultWrapper<Hash256>> eth_sendTransaction(TransactionForRpc rpcTx)
            => _baseModule.eth_sendTransaction(rpcTx);
        public Task<ResultWrapper<Hash256>> eth_sendRawTransaction(byte[] transaction)
            => _baseModule.eth_sendRawTransaction(transaction);
        public ResultWrapper<IReadOnlyList<SimulateBlockResult<SimulateCallResult>>> eth_simulateV1(SimulatePayload<TransactionForRpc> payload, BlockParameter? blockParameter = null)
            => _baseModule.eth_simulateV1(payload, blockParameter);
        public ResultWrapper<BlockForRpc> eth_getBlockByHash(Hash256 blockHash, bool returnFullTransactionObjects)
            => _baseModule.eth_getBlockByHash(blockHash, returnFullTransactionObjects);
        public ResultWrapper<BlockForRpc> eth_getBlockByNumber(BlockParameter blockParameter, bool returnFullTransactionObjects)
            => _baseModule.eth_getBlockByNumber(blockParameter, returnFullTransactionObjects);
        public ResultWrapper<TransactionForRpc?> eth_getTransactionByHash(Hash256 transactionHash)
            => _baseModule.eth_getTransactionByHash(transactionHash);
        public ResultWrapper<string?> eth_getRawTransactionByHash(Hash256 transactionHash)
            => _baseModule.eth_getRawTransactionByHash(transactionHash);
        public ResultWrapper<TransactionForRpc[]> eth_pendingTransactions()
            => _baseModule.eth_pendingTransactions();
        public ResultWrapper<TransactionForRpc> eth_getTransactionByBlockHashAndIndex(Hash256 blockHash, UInt256 positionIndex)
            => _baseModule.eth_getTransactionByBlockHashAndIndex(blockHash, positionIndex);
        public ResultWrapper<TransactionForRpc> eth_getTransactionByBlockNumberAndIndex(BlockParameter blockParameter, UInt256 positionIndex)
            => _baseModule.eth_getTransactionByBlockNumberAndIndex(blockParameter, positionIndex);
        public ResultWrapper<ReceiptForRpc?> eth_getTransactionReceipt(Hash256 txHash)
            => _baseModule.eth_getTransactionReceipt(txHash);
        public ResultWrapper<ReceiptForRpc[]?> eth_getBlockReceipts(BlockParameter blockParameter)
            => _baseModule.eth_getBlockReceipts(blockParameter);
        public ResultWrapper<BlockForRpc?> eth_getUncleByBlockHashAndIndex(Hash256 blockHash, UInt256 positionIndex)
            => _baseModule.eth_getUncleByBlockHashAndIndex(blockHash, positionIndex);
        public ResultWrapper<BlockForRpc?> eth_getUncleByBlockNumberAndIndex(BlockParameter blockParameter, UInt256 positionIndex)
            => _baseModule.eth_getUncleByBlockNumberAndIndex(blockParameter, positionIndex);
        public ResultWrapper<UInt256?> eth_newFilter(Filter filter)
            => _baseModule.eth_newFilter(filter);
        public ResultWrapper<UInt256?> eth_newBlockFilter()
            => _baseModule.eth_newBlockFilter();
        public ResultWrapper<UInt256?> eth_newPendingTransactionFilter()
            => _baseModule.eth_newPendingTransactionFilter();
        public ResultWrapper<bool?> eth_uninstallFilter(UInt256 filterId)
            => _baseModule.eth_uninstallFilter(filterId);
        public ResultWrapper<IEnumerable<object>> eth_getFilterChanges(UInt256 filterId)
            => _baseModule.eth_getFilterChanges(filterId);
        public ResultWrapper<IEnumerable<FilterLog>> eth_getFilterLogs(UInt256 filterId)
            => _baseModule.eth_getFilterLogs(filterId);
        public ResultWrapper<IEnumerable<FilterLog>> eth_getLogs(Filter filter)
            => _baseModule.eth_getLogs(filter);
        public ResultWrapper<AccountProof> eth_getProof(Address accountAddress, UInt256[] storageKeys, BlockParameter blockParameter)
            => _baseModule.eth_getProof(accountAddress, storageKeys, blockParameter);
        public ResultWrapper<ulong> eth_chainId()
            => _baseModule.eth_chainId();
        public ResultWrapper<AccountForRpc?> eth_getAccount(Address accountAddress, BlockParameter? blockParameter)
            => _baseModule.eth_getAccount(accountAddress, blockParameter);
        public ResultWrapper<AccountInfoForRpc?> eth_getAccountInfo(Address accountAddress, BlockParameter? blockParameter)
            => _baseModule.eth_getAccountInfo(accountAddress, blockParameter);
        public ResultWrapper<JsonNode> eth_config()
            => _baseModule.eth_config();

        private abstract class ArbitrumTxExecutor<TResult>(
            IBlockchainBridge blockchainBridge,
            IJsonRpcConfig rpcConfig,
            UInt256 originalBaseFee)
        {
            protected readonly IBlockchainBridge BlockchainBridge = blockchainBridge;
            protected readonly IJsonRpcConfig RpcConfig = rpcConfig;

            public ResultWrapper<TResult> Execute(
                TransactionForRpc transactionCall,
                Dictionary<Address, AccountOverride>? stateOverride,
                SearchResult<BlockHeader> searchResult)
            {
                if (transactionCall.Gas is null)
                {
                    transactionCall.Gas = searchResult.Object.GasLimit;
                }

                transactionCall.EnsureDefaults(RpcConfig.GasCap);

                var tx = transactionCall.ToTransaction();
                tx.ChainId = BlockchainBridge.GetChainId();

                // Create ArbitrumBlockHeader with original base fee
                ArbitrumBlockHeader arbitrumHeader = new(searchResult.Object, originalBaseFee);

                // Set base fee to 0 for EVM execution
                arbitrumHeader.BaseFeePerGas = 0;

                if (tx.IsContractCreation && tx.DataLength == 0)
                {
                    return ResultWrapper<TResult>.Fail("Contract creation without any data provided.", ErrorCodes.InvalidInput);
                }

                return ExecuteTx(arbitrumHeader, tx, stateOverride);
            }

            protected abstract ResultWrapper<TResult> ExecuteTx(
                BlockHeader header,
                Transaction tx,
                Dictionary<Address, AccountOverride>? stateOverride);
        }

        private class ArbitrumCallTxExecutor : ArbitrumTxExecutor<string>
        {
            public ArbitrumCallTxExecutor(
                IBlockchainBridge blockchainBridge,
                IJsonRpcConfig rpcConfig,
                UInt256 originalBaseFee)
                : base(blockchainBridge, rpcConfig, originalBaseFee)
            {
            }

            protected override ResultWrapper<string> ExecuteTx(
                BlockHeader header,
                Transaction tx,
                Dictionary<Address, AccountOverride>? stateOverride)
            {
                CallOutput result = BlockchainBridge.Call(header, tx, stateOverride, default);

                return result.Error is null
                    ? ResultWrapper<string>.Success(result.OutputData.ToHexString(true))
                    : ResultWrapper<string>.Fail("VM execution error.", ErrorCodes.ExecutionError, result.Error);
            }
        }

        private class ArbitrumEstimateGasTxExecutor : ArbitrumTxExecutor<UInt256?>
        {
            public ArbitrumEstimateGasTxExecutor(
                IBlockchainBridge blockchainBridge,
                IJsonRpcConfig rpcConfig,
                UInt256 originalBaseFee)
                : base(blockchainBridge, rpcConfig, originalBaseFee)
            {
            }

            protected override ResultWrapper<UInt256?> ExecuteTx(
                BlockHeader header,
                Transaction tx,
                Dictionary<Address, AccountOverride>? stateOverride)
            {
                CallOutput result = BlockchainBridge.EstimateGas(header, tx, RpcConfig.EstimateErrorMargin, stateOverride);

                return result switch
                {
                    { Error: null } => ResultWrapper<UInt256?>.Success((UInt256)result.GasSpent),
                    { InputError: true } => ResultWrapper<UInt256?>.Fail(result.Error, ErrorCodes.InvalidInput),
                    _ => ResultWrapper<UInt256?>.Fail(result.Error, ErrorCodes.ExecutionError)
                };
            }
        }

        private class ArbitrumCreateAccessListTxExecutor : ArbitrumTxExecutor<AccessListResultForRpc?>
        {
            private readonly bool _optimize;

            public ArbitrumCreateAccessListTxExecutor(
                IBlockchainBridge blockchainBridge,
                IJsonRpcConfig rpcConfig,
                UInt256 originalBaseFee,
                bool optimize)
                : base(blockchainBridge, rpcConfig, originalBaseFee)
            {
                _optimize = optimize;
            }

            protected override ResultWrapper<AccessListResultForRpc?> ExecuteTx(
                BlockHeader header,
                Transaction tx,
                Dictionary<Address, AccountOverride>? stateOverride)
            {
                CallOutput result = BlockchainBridge.CreateAccessList(header, tx, default, _optimize);

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
