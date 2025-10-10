// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.JsonRpc.Modules;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum;

public sealed class ArbitrumRpcModuleFactory(
    ArbitrumBlockTreeInitializer initializer,
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    ArbitrumRpcTxSource txSource,
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager,
    CachedL1PriceData cachedL1PriceData,
    IBlockProcessingQueue processingQueue,
    IArbitrumConfig arbitrumConfig,
    IVerifyBlockHashConfig verifyBlockHashConfig,
    IJsonSerializer jsonSerializer,
    IProcessExitSource? processExitSource = null) : ModuleFactoryBase<IArbitrumRpcModule>
{
    public override IArbitrumRpcModule Create()
    {
        if (!verifyBlockHashConfig.Enabled || string.IsNullOrWhiteSpace(verifyBlockHashConfig.ArbNodeRpcUrl))
            return new ArbitrumRpcModule(
                initializer, blockTree, trigger, txSource, chainSpec, specHelper,
                logManager, cachedL1PriceData, processingQueue, arbitrumConfig);

        ILogger logger = logManager.GetClassLogger<ArbitrumRpcModule>();
        if (logger.IsInfo)
            logger.Info($"Block hash verification enabled: verify every {verifyBlockHashConfig.VerifyEveryNBlocks} blocks, url={verifyBlockHashConfig.ArbNodeRpcUrl}");

        return new ArbitrumRpcModuleWithComparison(
            initializer, blockTree, trigger, txSource, chainSpec, specHelper,
            logManager, cachedL1PriceData, processingQueue, arbitrumConfig, verifyBlockHashConfig, jsonSerializer, processExitSource);
    }
}
