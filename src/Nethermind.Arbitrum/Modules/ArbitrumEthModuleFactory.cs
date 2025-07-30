// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core.Specs;
using Nethermind.Facade;
using Nethermind.Facade.Eth;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.JsonRpc.Modules.Eth.FeeHistory;
using Nethermind.JsonRpc.Modules.Eth.GasPrice;
using Nethermind.Logging;
using Nethermind.Network;
using Nethermind.State;
using Nethermind.TxPool;
using Nethermind.Wallet;
using Nethermind.Arbitrum.Execution;
namespace Nethermind.Arbitrum.Modules
{
    public class ArbitrumEthModuleFactory : ModuleFactoryBase<IEthRpcModule>
    {
        private readonly ITxPool _txPool;
        private readonly ITxSender _txSender;
        private readonly IWallet _wallet;
        private readonly IReadOnlyBlockTree _blockTree;
        private readonly IJsonRpcConfig _rpcConfig;
        private readonly ILogManager _logManager;
        private readonly IStateReader _stateReader;
        private readonly IBlockchainBridgeFactory _blockchainBridgeFactory;
        private readonly ISpecProvider _specProvider;
        private readonly IReceiptStorage _receiptStorage;
        private readonly IGasPriceOracle _gasPriceOracle;
        private readonly IEthSyncingInfo _ethSyncingInfo;
        private readonly IFeeHistoryOracle _feeHistoryOracle;
        private readonly IProtocolsManager _protocolsManager;
        private readonly ArbitrumTransactionProcessor _arbitrumTxProcessor;
        private readonly ulong _secondsPerSlot;

        public ArbitrumEthModuleFactory(
            ITxPool txPool,
            ITxSender txSender,
            IWallet wallet,
            IBlockTree blockTree,
            IJsonRpcConfig config,
            ILogManager logManager,
            IStateReader stateReader,
            IBlockchainBridgeFactory blockchainBridgeFactory,
            ISpecProvider specProvider,
            IReceiptStorage receiptStorage,
            IGasPriceOracle gasPriceOracle,
            IEthSyncingInfo ethSyncingInfo,
            IFeeHistoryOracle feeHistoryOracle,
            IProtocolsManager protocolsManager,
            ArbitrumTransactionProcessor arbitrumTxProcessor,
            ulong secondsPerSlot)
        {
            _txPool = txPool ?? throw new ArgumentNullException(nameof(txPool));
            _txSender = txSender ?? throw new ArgumentNullException(nameof(txSender));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _blockTree = blockTree.AsReadOnly();
            _rpcConfig = config ?? throw new ArgumentNullException(nameof(config));
            _logManager = logManager ?? throw new ArgumentNullException(nameof(logManager));
            _stateReader = stateReader ?? throw new ArgumentNullException(nameof(stateReader));
            _blockchainBridgeFactory = blockchainBridgeFactory ?? throw new ArgumentNullException(nameof(blockchainBridgeFactory));
            _specProvider = specProvider ?? throw new ArgumentNullException(nameof(specProvider));
            _receiptStorage = receiptStorage ?? throw new ArgumentNullException(nameof(receiptStorage));
            _gasPriceOracle = gasPriceOracle ?? throw new ArgumentNullException(nameof(gasPriceOracle));
            _ethSyncingInfo = ethSyncingInfo ?? throw new ArgumentNullException(nameof(ethSyncingInfo));
            _feeHistoryOracle = feeHistoryOracle ?? throw new ArgumentNullException(nameof(feeHistoryOracle));
            _protocolsManager = protocolsManager ?? throw new ArgumentNullException(nameof(protocolsManager));
            _arbitrumTxProcessor = arbitrumTxProcessor ?? throw new ArgumentNullException(nameof(arbitrumTxProcessor));
            _secondsPerSlot = secondsPerSlot;
        }

        public override IEthRpcModule Create()
        {
            // Create the base EthRpcModule
            var baseModule = new EthRpcModule(
                _rpcConfig,
                _blockchainBridgeFactory.CreateBlockchainBridge(),
                _blockTree,
                _receiptStorage,
                _stateReader,
                _txPool,
                _txSender,
                _wallet,
                _logManager,
                _specProvider,
                _gasPriceOracle,
                _ethSyncingInfo,
                _feeHistoryOracle,
                _protocolsManager,
                _secondsPerSlot);

            // Wrap it with ArbitrumEthRpcModule
            return new ArbitrumEthRpcModule(
                baseModule,
                _arbitrumTxProcessor,
                _blockTree);
        }
    }
}