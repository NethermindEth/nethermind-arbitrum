// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core.Specs;
using Nethermind.Evm;
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
        private readonly IBlockTree _blockTree;
        private readonly IJsonRpcConfig _jsonRpcConfig;
        private readonly ILogManager _logManager;
        private readonly IStateReader _stateReader;
        private readonly ArbitrumNethermindApi _api;
        private readonly ISpecProvider _specProvider;
        private readonly IReceiptStorage _receiptStorage;
        private readonly IGasPriceOracle _gasPriceOracle;
        private readonly IEthSyncingInfo _ethSyncingInfo;
        private readonly IFeeHistoryOracle _feeHistoryOracle;
        private readonly IProtocolsManager _protocolsManager;
        private readonly ArbitrumVirtualMachine _arbitrumVM;
        private readonly ulong? _secondsPerSlot;

        public ArbitrumEthModuleFactory(
            ITxPool txPool,
            ITxSender txSender,
            IWallet wallet,
            IBlockTree blockTree,
            IJsonRpcConfig jsonRpcConfig,
            ILogManager logManager,
            IStateReader stateReader,
            ArbitrumNethermindApi api,
            ISpecProvider specProvider,
            IReceiptStorage receiptStorage,
            IGasPriceOracle gasPriceOracle,
            IEthSyncingInfo ethSyncingInfo,
            IFeeHistoryOracle feeHistoryOracle,
            IProtocolsManager protocolsManager,
            ArbitrumVirtualMachine arbitrumVM,
            ulong? secondsPerSlot)
        {
            _txPool = txPool;
            _txSender = txSender;
            _wallet = wallet;
            _blockTree = blockTree;
            _jsonRpcConfig = jsonRpcConfig;
            _logManager = logManager;
            _stateReader = stateReader;
            _api = api;
            _specProvider = specProvider;
            _receiptStorage = receiptStorage;
            _gasPriceOracle = gasPriceOracle;
            _ethSyncingInfo = ethSyncingInfo;
            _feeHistoryOracle = feeHistoryOracle;
            _protocolsManager = protocolsManager;
            _arbitrumVM = arbitrumVM;
            _secondsPerSlot = secondsPerSlot;
        }

        public override IEthRpcModule Create()
        {
            var blockchainBridge = _api.CreateBlockchainBridge();

            return new ArbitrumEthRpcModule(
                _jsonRpcConfig,
                blockchainBridge,
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
                _arbitrumVM,
                _secondsPerSlot);
        }
    }
}
