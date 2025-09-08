// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
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
using System;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumEthModuleFactory : ModuleFactoryBase<IEthRpcModule>
{
    private readonly ITxPool _txPool;
    private readonly ITxSender _txSender;
    private readonly IWallet _wallet;
    private readonly IBlockTree _blockTree;
    private readonly IJsonRpcConfig _jsonRpcConfig;
    private readonly ILogManager _logManager;
    private readonly IStateReader _stateReader;
    private readonly IBlockchainBridgeFactory _blockchainBridgeFactory;
    private readonly ISpecProvider _specProvider;
    private readonly IReceiptStorage _receiptStorage;
    private readonly IGasPriceOracle _gasPriceOracle;
    private readonly IEthSyncingInfo _ethSyncingInfo;
    private readonly IFeeHistoryOracle _feeHistoryOracle;
    private readonly IProtocolsManager _protocolsManager;
    private readonly IForkInfo _forkInfo;
    private readonly ulong? _secondsPerSlot;

    public ArbitrumEthModuleFactory(
        ITxPool txPool,
        ITxSender txSender,
        IWallet wallet,
        IBlockTree blockTree,
        IJsonRpcConfig jsonRpcConfig,
        ILogManager logManager,
        IStateReader stateReader,
        IBlockchainBridgeFactory blockchainBridgeFactory,
        ISpecProvider specProvider,
        IReceiptStorage receiptStorage,
        IGasPriceOracle gasPriceOracle,
        IEthSyncingInfo ethSyncingInfo,
        IFeeHistoryOracle feeHistoryOracle,
        IProtocolsManager protocolsManager,
        IForkInfo forkInfo,
        IBlocksConfig blocksConfig)
    {
        _txPool = txPool;
        _txSender = txSender;
        _wallet = wallet;
        _blockTree = blockTree;
        _jsonRpcConfig = jsonRpcConfig;
        _logManager = logManager;
        _stateReader = stateReader;
        _blockchainBridgeFactory = blockchainBridgeFactory;
        _specProvider = specProvider;
        _receiptStorage = receiptStorage;
        _gasPriceOracle = gasPriceOracle;
        _ethSyncingInfo = ethSyncingInfo;
        _feeHistoryOracle = feeHistoryOracle;
        _protocolsManager = protocolsManager;
        _forkInfo = forkInfo;
        _secondsPerSlot = blocksConfig.SecondsPerSlot;
    }

    public override IEthRpcModule Create()
    {
        EthRpcModule baseEthModule = new(
            _jsonRpcConfig,
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
            _forkInfo,
            _secondsPerSlot);

        return new ArbitrumEthRpcModule(
            baseEthModule,
            _blockchainBridgeFactory.CreateBlockchainBridge(),
            _blockTree,
            _jsonRpcConfig);
    }
}
