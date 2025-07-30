// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethermind.Blockchain.Find;
using Nethermind.Core;
using Nethermind.Core.Crypto;
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
using Nethermind.State.Proofs;
using Nethermind.Arbitrum.Execution;


namespace Nethermind.Arbitrum.Modules
{
    [RpcModule(ModuleType.Eth)]
    public class ArbitrumEthRpcModule : IEthRpcModule
    {
        private readonly EthRpcModule _baseModule;
        private readonly ArbitrumTransactionProcessor _arbitrumTxProcessor;
        private readonly IBlockFinder _blockFinder;

        public ArbitrumEthRpcModule(
            EthRpcModule baseModule,
            ArbitrumTransactionProcessor arbitrumTxProcessor,
            IBlockFinder blockFinder)
        {
            _baseModule = baseModule ?? throw new ArgumentNullException(nameof(baseModule));
            _arbitrumTxProcessor = arbitrumTxProcessor ?? throw new ArgumentNullException(nameof(arbitrumTxProcessor));
            _blockFinder = blockFinder ?? throw new ArgumentNullException(nameof(blockFinder));
        }

        // Override the three methods that need original base fee handling
        public ResultWrapper<UInt256?> eth_estimateGas(
            TransactionForRpc transactionCall, 
            BlockParameter? blockParameter = null, 
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            return ExecuteWithOriginalBaseFee(
                blockParameter,
                () => _baseModule.eth_estimateGas(transactionCall, blockParameter, stateOverride));
        }

        public ResultWrapper<string> eth_call(
            TransactionForRpc transactionCall, 
            BlockParameter? blockParameter = null, 
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            return ExecuteWithOriginalBaseFee(
                blockParameter,
                () => _baseModule.eth_call(transactionCall, blockParameter, stateOverride));
        }

        public ResultWrapper<AccessListResultForRpc?> eth_createAccessList(
            TransactionForRpc transactionCall, 
            BlockParameter? blockParameter = null, 
            bool optimize = true)
        {
            return ExecuteWithOriginalBaseFee(
                blockParameter,
                () => _baseModule.eth_createAccessList(transactionCall, blockParameter, optimize));
        }

        // Helper method to handle original base fee logic
        private T ExecuteWithOriginalBaseFee<T>(BlockParameter? blockParameter, Func<T> execution)
        {
            var searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult.IsError)
            {
                return execution();
            }

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;
            _arbitrumTxProcessor.SetOriginalBaseFeeForExecution(originalBaseFee);

            try
            {
                return execution();
            }
            finally
            {
                _arbitrumTxProcessor.SetOriginalBaseFeeForExecution(UInt256.Zero);
            }
        }

        // Delegate all other methods to base module
        public ResultWrapper<ulong> eth_chainId() => _baseModule.eth_chainId();
        public ResultWrapper<string> eth_protocolVersion() => _baseModule.eth_protocolVersion();
        public ResultWrapper<SyncingResult> eth_syncing() => _baseModule.eth_syncing();
        public ResultWrapper<Address> eth_coinbase() => _baseModule.eth_coinbase();
        public ResultWrapper<FeeHistoryResults> eth_feeHistory(int blockCount, BlockParameter newestBlock, double[]? rewardPercentiles = null) => 
            _baseModule.eth_feeHistory(blockCount, newestBlock, rewardPercentiles);
        public ResultWrapper<byte[]> eth_snapshot() => _baseModule.eth_snapshot();
        public ResultWrapper<UInt256?> eth_maxPriorityFeePerGas() => _baseModule.eth_maxPriorityFeePerGas();
        public ResultWrapper<UInt256?> eth_gasPrice() => _baseModule.eth_gasPrice();
        public ResultWrapper<UInt256?> eth_blobBaseFee() => _baseModule.eth_blobBaseFee();
        public ResultWrapper<IEnumerable<Address>> eth_accounts() => _baseModule.eth_accounts();
        public Task<ResultWrapper<long?>> eth_blockNumber() => _baseModule.eth_blockNumber();
        public Task<ResultWrapper<UInt256?>> eth_getBalance(Address address, BlockParameter? blockParameter = null) => 
            _baseModule.eth_getBalance(address, blockParameter);
        public ResultWrapper<byte[]> eth_getStorageAt(Address address, UInt256 positionIndex, BlockParameter? blockParameter = null) => 
            _baseModule.eth_getStorageAt(address, positionIndex, blockParameter);
        public Task<ResultWrapper<UInt256>> eth_getTransactionCount(Address address, BlockParameter? blockParameter = null) => 
            _baseModule.eth_getTransactionCount(address, blockParameter);
        public ResultWrapper<UInt256?> eth_getBlockTransactionCountByHash(Hash256 blockHash) => 
            _baseModule.eth_getBlockTransactionCountByHash(blockHash);
        public ResultWrapper<UInt256?> eth_getBlockTransactionCountByNumber(BlockParameter blockParameter) => 
            _baseModule.eth_getBlockTransactionCountByNumber(blockParameter);
        public ResultWrapper<ReceiptForRpc[]?> eth_getBlockReceipts(BlockParameter blockParameter) => 
            _baseModule.eth_getBlockReceipts(blockParameter);
        public ResultWrapper<UInt256?> eth_getUncleCountByBlockHash(Hash256 blockHash) => 
            _baseModule.eth_getUncleCountByBlockHash(blockHash);
        public ResultWrapper<UInt256?> eth_getUncleCountByBlockNumber(BlockParameter blockParameter) => 
            _baseModule.eth_getUncleCountByBlockNumber(blockParameter);
        public ResultWrapper<byte[]> eth_getCode(Address address, BlockParameter? blockParameter = null) => 
            _baseModule.eth_getCode(address, blockParameter);
        public ResultWrapper<string> eth_sign(Address addressData, byte[] message) => 
            _baseModule.eth_sign(addressData, message);
        public Task<ResultWrapper<Hash256>> eth_sendTransaction(TransactionForRpc rpcTx) => 
            _baseModule.eth_sendTransaction(rpcTx);
        public Task<ResultWrapper<Hash256>> eth_sendRawTransaction(byte[] transaction) => 
            _baseModule.eth_sendRawTransaction(transaction);
        public ResultWrapper<IReadOnlyList<SimulateBlockResult<SimulateCallResult>>> eth_simulateV1(SimulatePayload<TransactionForRpc> payload, BlockParameter? blockParameter = null) => 
            _baseModule.eth_simulateV1(payload, blockParameter);
        public ResultWrapper<BlockForRpc> eth_getBlockByHash(Hash256 blockHash, bool returnFullTransactionObjects = false) => 
            _baseModule.eth_getBlockByHash(blockHash, returnFullTransactionObjects);
        public ResultWrapper<BlockForRpc> eth_getBlockByNumber(BlockParameter blockParameter, bool returnFullTransactionObjects = false) => 
            _baseModule.eth_getBlockByNumber(blockParameter, returnFullTransactionObjects);
        public ResultWrapper<TransactionForRpc?> eth_getTransactionByHash(Hash256 transactionHash) => 
            _baseModule.eth_getTransactionByHash(transactionHash);
        public ResultWrapper<string?> eth_getRawTransactionByHash(Hash256 transactionHash) => 
            _baseModule.eth_getRawTransactionByHash(transactionHash);
        public ResultWrapper<TransactionForRpc[]> eth_pendingTransactions() => 
            _baseModule.eth_pendingTransactions();
        public ResultWrapper<TransactionForRpc> eth_getTransactionByBlockHashAndIndex(Hash256 blockHash, UInt256 positionIndex) => 
            _baseModule.eth_getTransactionByBlockHashAndIndex(blockHash, positionIndex);
        public ResultWrapper<TransactionForRpc> eth_getTransactionByBlockNumberAndIndex(BlockParameter blockParameter, UInt256 positionIndex) => 
            _baseModule.eth_getTransactionByBlockNumberAndIndex(blockParameter, positionIndex);
        public ResultWrapper<ReceiptForRpc?> eth_getTransactionReceipt(Hash256 txHashData) => 
            _baseModule.eth_getTransactionReceipt(txHashData);
        public ResultWrapper<BlockForRpc?> eth_getUncleByBlockHashAndIndex(Hash256 blockHashData, UInt256 positionIndex) => 
            _baseModule.eth_getUncleByBlockHashAndIndex(blockHashData, positionIndex);
        public ResultWrapper<BlockForRpc?> eth_getUncleByBlockNumberAndIndex(BlockParameter blockParameter, UInt256 positionIndex) => 
            _baseModule.eth_getUncleByBlockNumberAndIndex(blockParameter, positionIndex);
        public ResultWrapper<UInt256?> eth_newFilter(Filter filter) => 
            _baseModule.eth_newFilter(filter);
        public ResultWrapper<UInt256?> eth_newBlockFilter() => 
            _baseModule.eth_newBlockFilter();
        public ResultWrapper<UInt256?> eth_newPendingTransactionFilter() => 
            _baseModule.eth_newPendingTransactionFilter();
        public ResultWrapper<bool?> eth_uninstallFilter(UInt256 filterId) => 
            _baseModule.eth_uninstallFilter(filterId);
        public ResultWrapper<IEnumerable<object>> eth_getFilterChanges(UInt256 filterId) => 
            _baseModule.eth_getFilterChanges(filterId);
        public ResultWrapper<IEnumerable<FilterLog>> eth_getFilterLogs(UInt256 filterId) => 
            _baseModule.eth_getFilterLogs(filterId);
        public ResultWrapper<IEnumerable<FilterLog>> eth_getLogs(Filter filter) => 
            _baseModule.eth_getLogs(filter);
        public ResultWrapper<AccountProof> eth_getProof(Address accountAddress, UInt256[] hashRate, BlockParameter blockParameter) => 
            _baseModule.eth_getProof(accountAddress, hashRate, blockParameter);
        public ResultWrapper<AccountForRpc?> eth_getAccount(Address accountAddress, BlockParameter? blockParameter = null) => 
            _baseModule.eth_getAccount(accountAddress, blockParameter);
    }
}