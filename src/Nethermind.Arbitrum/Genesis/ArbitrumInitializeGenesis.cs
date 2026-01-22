// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Init.Steps;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum;

/// <summary>
/// Initializes Arbitrum genesis block AFTER block processors have started.
/// This must run after StartBlockProducer to ensure block processing infrastructure is ready.
/// </summary>
[RunnerStepDependencies(typeof(InitializeNetwork), typeof(RegisterRpcModules), typeof(RegisterPluginRpcModules))]
public class ArbitrumInitializeGenesis(ArbitrumNethermindApi api) : IStep
{
    private readonly ArbitrumNethermindApi _api = api;
    private readonly ILogger _logger = api.LogManager.GetClassLogger<ArbitrumInitializeGenesis>();

    public async Task Execute(CancellationToken cancellationToken)
    {
        IBlockTree blockTree = _api.BlockTree
            ?? throw new InvalidOperationException("BlockTree not initialized");

        IArbitrumSpecHelper specHelper = _api.Context.Resolve<IArbitrumSpecHelper>();
        ulong genesisBlockNum = specHelper.GenesisBlockNum;

        // Check if genesis already exists
        BlockHeader? existingGenesis = blockTree.FindHeader((long)genesisBlockNum);

        if (existingGenesis != null)
        {
            _logger.Info($"Arbitrum genesis already exists: {existingGenesis.Hash}");
            return;
        }

        _logger.Info($"Arbitrum genesis not found - creating from chainspec");

        // Create init message from chainspec
        ChainSpec chainSpec = _api.ChainSpec;
        ChainSpecInitMessageProvider initMessageProvider = new(chainSpec, specHelper);
        ParsedInitMessage initMessage = initMessageProvider.GetInitMessage();

        // Call ArbitrumBlockTreeInitializer directly - block processors are now running!
        ArbitrumBlockTreeInitializer initializer = _api.Context.Resolve<ArbitrumBlockTreeInitializer>();
        BlockHeader genesisHeader = initializer.Initialize(initMessage);

        _logger.Info($"Arbitrum genesis created successfully: {genesisHeader.Hash}");

        await Task.CompletedTask;
    }
}
