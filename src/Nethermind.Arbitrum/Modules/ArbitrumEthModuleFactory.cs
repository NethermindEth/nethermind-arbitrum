// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
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
    IEthereumEcdsa ecdsa) : ModuleFactoryBase<IEthRpcModule>
{
    public TransactionQueue? TransactionQueue { get; set; }
    public SequencerState? SequencerState { get; set; }

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
            TransactionQueue,
            SequencerState,
            ecdsa);
    }
}
