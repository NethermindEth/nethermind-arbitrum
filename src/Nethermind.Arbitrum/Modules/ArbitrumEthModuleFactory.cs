// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
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
using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumEthModuleFactory(
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
    IBlocksConfig blocksConfig,
    ArbitrumChainSpecEngineParameters chainSpecParams,
    IArbitrumConfig arbitrumConfig) : ModuleFactoryBase<IEthRpcModule>
{
    public override IEthRpcModule Create()
    {
        return new ArbitrumEthRpcModule(
            jsonRpcConfig,
            blockchainBridgeFactory.CreateBlockchainBridge(),
            blockTree,
            receiptStorage,
            stateReader,
            txPool,
            txSender,
            wallet,
            logManager,
            specProvider,
            gasPriceOracle,
            ethSyncingInfo,
            feeHistoryOracle,
            protocolsManager,
            forkInfo,
            blocksConfig.SecondsPerSlot,
            chainSpecParams,
            arbitrumConfig);
    }
}
