// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.JsonRpc.Modules;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum;

public class ArbitrumRpcModuleFactory(
    ArbitrumBlockTreeInitializer initializer,
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    ArbitrumRpcTxSource txSource,
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager,
    CachedL1PriceData cachedL1PriceData,
    IBlockProcessingQueue processingQueue,
    IArbitrumConfig arbitrumConfig) : ModuleFactoryBase<IArbitrumRpcModule>
{
    public override IArbitrumRpcModule Create()
    {
        // If comparison mode is enabled, return the comparison-enabled version
        if (arbitrumConfig.ComparisonModeInterval > 0 && !string.IsNullOrWhiteSpace(arbitrumConfig.ComparisonModeRpcUrl))
        {
            ILogger logger = logManager.GetClassLogger<ArbitrumRpcModule>();
            if (logger.IsInfo)
                logger.Info($"Comparison mode enabled: interval={arbitrumConfig.ComparisonModeInterval}, url={arbitrumConfig.ComparisonModeRpcUrl}");

            return new ArbitrumRpcModuleWithComparison(
                initializer, blockTree, trigger, txSource, chainSpec, specHelper,
                logManager, cachedL1PriceData, processingQueue, arbitrumConfig);
        }

        // Otherwise, return the standard version (no overhead)
        return new ArbitrumRpcModule(
            initializer, blockTree, trigger, txSource, chainSpec, specHelper,
            logManager, cachedL1PriceData, processingQueue, arbitrumConfig);
    }
}
