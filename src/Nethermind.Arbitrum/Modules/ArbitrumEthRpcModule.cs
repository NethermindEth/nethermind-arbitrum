// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Core;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Rpc;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Blockchain.Find;
using Nethermind.Db.LogIndex;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
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
using Nethermind.Serialization.Rlp;
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
        private readonly TransactionQueue _transactionQueue;
        private readonly SequencerState _sequencerState;
        private readonly IEthereumEcdsa _ecdsa;

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
            ILogIndexConfig? logIndexConfig,
            ulong? secondsPerSlot,
            ArbitrumChainSpecEngineParameters chainSpecParams,
            TransactionQueue transactionQueue,
            SequencerState sequencerState,
            IEthereumEcdsa ecdsa)
            : base(rpcConfig, blockchainBridge, blockFinder, receiptFinder, stateReader, txPool, txSender, wallet, logManager, specProvider, gasPriceOracle, ethSyncingInfo, feeHistoryOracle, protocolsManager, forkInfo, logIndexConfig, secondsPerSlot)
        {
            _chainSpecParams = chainSpecParams;
            _transactionQueue = transactionQueue;
            _sequencerState = sequencerState;
            _ecdsa = ecdsa;
        }

        public override async Task<ResultWrapper<Hash256>> eth_sendRawTransaction(byte[] transaction)
        {
            Transaction tx;
            try
            {
                tx = Rlp.Decode<Transaction>(transaction,
                    RlpBehaviors.AllowUnsigned | RlpBehaviors.SkipTypedWrapping | RlpBehaviors.InMempoolForm);
            }
            catch (RlpException)
            {
                return ResultWrapper<Hash256>.Fail("Invalid RLP.", ErrorCodes.TransactionRejected);
            }

            tx.SenderAddress = _ecdsa.RecoverAddress(tx);
            // Force hash computation before enqueuing so tx.Hash is available for the response
            _ = tx.Hash;

            switch (_sequencerState.Mode)
            {
                case SequencerMode.Active:
                    Exception? enqueueError = await _transactionQueue.EnqueueAsync(new TxQueueItem(tx, CancellationToken.None));
                    return enqueueError is not null
                        ? ResultWrapper<Hash256>.Fail(enqueueError.Message, ErrorCodes.TransactionRejected)
                        : ResultWrapper<Hash256>.Success(tx.Hash!);
                case SequencerMode.Forwarding:
                    TransactionForwarder? forwarder = _sequencerState.Forwarder;
                    if (forwarder is null)
                        return ResultWrapper<Hash256>.Fail("Sequencer temporarily not available.", ErrorCodes.TransactionRejected);

                    Exception? forwardError = await forwarder.ForwardTransactionAsync(Rlp.Encode(tx).Bytes, CancellationToken.None);
                    return forwardError is not null
                        ? ResultWrapper<Hash256>.Fail(forwardError.Message, ErrorCodes.TransactionRejected)
                        : ResultWrapper<Hash256>.Success(tx.Hash!);
                default:
                    return ResultWrapper<Hash256>.Fail("Sequencer temporarily not available.", ErrorCodes.TransactionRejected);
            }
        }

        public new ResultWrapper<TransactionForRpc[]> eth_pendingTransactions()
        {
            return ResultWrapper<TransactionForRpc[]>.Success([]);
        }

        protected override ResultWrapper<BlockForRpc?> GetBlock(BlockParameter blockParameter, bool returnFullTransactionObjects)
        {
            SearchResult<Block> searchResult = _blockFinder.SearchForBlock(blockParameter, true);
            if (searchResult.IsError)
                return ResultWrapper<BlockForRpc?>.Success(null);

            Block? block = searchResult.Object;
            if (block is null)
                return ResultWrapper<BlockForRpc?>.Success(null);

            if (returnFullTransactionObjects)
                _blockchainBridge.RecoverTxSenders(block);

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, _logger);
            return ResultWrapper<BlockForRpc?>.Success(new ArbitrumBlockForRpc(block, returnFullTransactionObjects, _specProvider, headerInfo));
        }

        public new async Task<ResultWrapper<UInt256>> eth_getTransactionCount(Address address, BlockParameter? blockParameter)
        {
            _logger.Warn($"eth_getTransactionCount [{address}, {blockParameter}]");

            if (blockParameter != BlockParameter.Pending || _blockFinder.Head?.Header is null)
            {
                ResultWrapper<UInt256> ethGetTransactionCount = await base.eth_getTransactionCount(address, blockParameter);

                _logger.Warn($"eth_getTransactionCount: default implementation result={ethGetTransactionCount.Data}");

                return ethGetTransactionCount;
            }

            _stateReader.TryGetAccount(_blockFinder.Head?.Header, address, out AccountStruct account);

            _logger.Warn($"eth_getTransactionCount: arbitrum implementation nonce={account.Nonce} balance={account.Balance} head={_blockFinder.Head?.Header.Number}");

            return ResultWrapper<UInt256>.Success(account.Nonce);
        }

        public override ResultWrapper<string> eth_call(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            SearchResult<BlockHeader> searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult is { IsError: true, Error: not null })
                return ResultWrapper<string>.Fail(searchResult.Error, searchResult.ErrorCode);

            if (searchResult.Object == null)
                return ResultWrapper<string>.Fail("Block not found", 0);

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumCallTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, originalBaseFee, _chainSpecParams)
                .Execute(transactionCall, blockParameter, stateOverride, searchResult);
        }

        public override ResultWrapper<UInt256?> eth_estimateGas(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null)
        {
            SearchResult<BlockHeader> searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult is { IsError: true, Error: not null })
                return ResultWrapper<UInt256?>.Fail(searchResult.Error, searchResult.ErrorCode);

            if (searchResult.Object == null)
                return ResultWrapper<UInt256?>.Fail("Block not found", 0);

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            ResultWrapper<UInt256?> ethEstimateGas = new ArbitrumEstimateGasTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, originalBaseFee, _chainSpecParams)
                .Execute(transactionCall, blockParameter, stateOverride, searchResult);

            _logger.Warn($"eth_estimateGas: estimateGas result={ethEstimateGas.Data}");

            return ethEstimateGas;
        }

        public override ResultWrapper<AccessListResultForRpc?> eth_createAccessList(
            TransactionForRpc transactionCall,
            BlockParameter? blockParameter = null,
            Dictionary<Address, AccountOverride>? stateOverride = null,
            bool optimize = true)
        {
            SearchResult<BlockHeader> searchResult = _blockFinder.SearchForHeader(blockParameter);
            if (searchResult is { IsError: true, Error: not null })
                return ResultWrapper<AccessListResultForRpc?>.Fail(searchResult.Error, searchResult.ErrorCode);

            if (searchResult.Object == null)
                return ResultWrapper<AccessListResultForRpc?>.Fail("Block not found", 0);

            UInt256 originalBaseFee = searchResult.Object.BaseFeePerGas;

            return new ArbitrumCreateAccessListTxExecutor(_blockchainBridge, _blockFinder, _rpcConfig, originalBaseFee, _chainSpecParams, optimize)
                .Execute(transactionCall, blockParameter, stateOverride, searchResult);
        }

        public override ResultWrapper<ReceiptForRpc?> eth_getTransactionReceipt(Hash256 txHash)
        {
            (TxReceipt? receipt, ulong blockTimestamp, TxGasInfo? gasInfo, int logIndexStart) = _blockchainBridge.GetTxReceiptInfo(txHash);
            if (receipt is null || gasInfo is null)
                return ResultWrapper<ReceiptForRpc?>.Success(null);

            ulong l1BlockNumber = 0;
            if (receipt.BlockHash is not null)
            {
                BlockHeader? header = _blockFinder.FindHeader(receipt.BlockHash);
                if (header is not null)
                    l1BlockNumber = ArbitrumBlockHeaderInfo.Deserialize(header, _logger).L1BlockNumber;
            }

            ArbitrumReceiptForRpc result = new(
                txHash,
                receipt,
                blockTimestamp,
                gasInfo.Value,
                l1BlockNumber,
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
            ulong l1BlockNumber = ArbitrumBlockHeaderInfo.Deserialize(block.Header, _logger).L1BlockNumber;

            ReceiptForRpc[] result = receipts
                .Zip(block.Transactions, (receipt, tx) =>
                    (ReceiptForRpc)new ArbitrumReceiptForRpc(
                        tx.Hash!,
                        receipt,
                        block.Timestamp,
                        tx.GetGasInfo(spec, block.Header),
                        l1BlockNumber,
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
                        transactionCall.Gas = searchResult.Value.Object?.GasLimit;
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

                if (tx is { IsContractCreation: true, DataLength: 0 })
                    return ResultWrapper<TResult>.Fail("Contract creation without any data provided.", ErrorCodes.InvalidInput);

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
                    _ => ResultWrapper<string>.Fail(result.Error, ErrorCodes.Default)
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
                    _ => ResultWrapper<UInt256?>.Fail(result.Error, ErrorCodes.Default)
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
                CallOutput result = _blockchainBridge.CreateAccessList(header, tx, stateOverride, token, optimize);

                AccessListResultForRpc rpcAccessListResult = new(
                    accessList: AccessListForRpc.FromAccessList(result.AccessList ?? tx.AccessList),
                    gasUsed: GetResultGas(tx, result),
                    result.Error);

                return result switch
                {
                    { Error: null } => ResultWrapper<AccessListResultForRpc?>.Success(rpcAccessListResult),
                    { InputError: true } => ResultWrapper<AccessListResultForRpc?>.Fail(result.Error, ErrorCodes.InvalidInput),
                    _ => ResultWrapper<AccessListResultForRpc?>.Fail(result.Error, ErrorCodes.Default),
                };
            }

            private static UInt256 GetResultGas(Transaction transaction, CallOutput result)
            {
                long gas = result.GasSpent;
                long operationGas = result.OperationGas;
                if (result.AccessList is null)
                    return (UInt256)gas;

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
                    gas += updatedAccessListCost;

                return (UInt256)gas;
            }
        }
    }
}
