// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Extensions;
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
    IBlocksConfig blocksConfig,
    IProcessExitSource? processExitSource = null) : ModuleFactoryBase<IArbitrumRpcModule>
{
    public override IArbitrumRpcModule Create()
    {
        IArbitrumRpcModule arbitrumRpcModule;
        if (!verifyBlockHashConfig.Enabled || string.IsNullOrWhiteSpace(verifyBlockHashConfig.ArbNodeRpcUrl))
            arbitrumRpcModule = new ArbitrumRpcModule(
                initializer, blockTree, trigger, txSource, chainSpec, specHelper,
                logManager, cachedL1PriceData, processingQueue, arbitrumConfig, blocksConfig);
        else
        {
            ILogger logger = logManager.GetClassLogger<ArbitrumRpcModule>();
            if (logger.IsInfo)
                logger.Info($"Block hash verification enabled: verify every {verifyBlockHashConfig.VerifyEveryNBlocks} blocks, url={verifyBlockHashConfig.ArbNodeRpcUrl}");

            arbitrumRpcModule = new ArbitrumRpcModuleWithComparison(
                initializer, blockTree, trigger, txSource, chainSpec, specHelper,
                logManager, cachedL1PriceData, processingQueue, arbitrumConfig, verifyBlockHashConfig, jsonSerializer, blocksConfig, processExitSource);
        }

        if (arbitrumConfig.GenesisConfig is not null)
        {
            DigestInitMessage message = new(arbitrumConfig.InitialL1BaseFee, Bytes.FromHexString(arbitrumConfig.GenesisConfig));
            if (arbitrumRpcModule.DigestInitMessage(message).Result.ResultType == ResultType.Failure)
            {
                throw new ArgumentException("Failed to initialize genesis state: invalid genesis in config");
            }
        }

        return arbitrumRpcModule;
    }
}
