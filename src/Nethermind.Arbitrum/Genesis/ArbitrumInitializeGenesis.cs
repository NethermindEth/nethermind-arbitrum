// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.Init.Steps;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Init;

[RunnerStepDependencies(typeof(InitDatabase))]
public class ArbitrumInitializeGenesis : IStep
{
    private readonly ArbitrumNethermindApi _api;
    private readonly IArbitrumSpecHelper _specHelper;

    public ArbitrumInitializeGenesis(ArbitrumNethermindApi api, IArbitrumSpecHelper specHelper)
    {
        _api = api;
        _specHelper = specHelper;
    }

    public Task Execute(CancellationToken cancellationToken)
    {
        if (!_specHelper.Enabled)
            return Task.CompletedTask;

        var logger = _api.LogManager.GetClassLogger();
        long arbitrumGenesisBlockNum = (long)_specHelper.GenesisBlockNum;

        Block? arbitrumGenesisBlock = _api.BlockTree!.FindBlock(arbitrumGenesisBlockNum, BlockTreeLookupOptions.None);

        if (arbitrumGenesisBlock != null)
        {
            logger.Info($"Arbitrum genesis block {arbitrumGenesisBlockNum} already exists (hash: {arbitrumGenesisBlock.Hash})");

            // Re-establish state registration by running the state initialization
            logger.Info($"Re-initializing genesis state registration...");

            var worldState = _api.WorldStateManager.GlobalWorldState;

            using (worldState.BeginScope(IWorldState.PreGenesis))
            {
                worldState.CommitTree(arbitrumGenesisBlockNum);
                logger.Info($"✓ State tree committed for genesis block {arbitrumGenesisBlockNum}");
            }

            return Task.CompletedTask;
        }

        logger.Info($"Initializing Arbitrum genesis at block {arbitrumGenesisBlockNum}...");

        var nodeStorage = _api.Context.Resolve<INodeStorage>();

        var blockTreeInitializer = new ArbitrumBlockTreeInitializer(
            _api.ChainSpec!,
            _api.SpecProvider!,
            _specHelper,
            _api.WorldStateManager!,
            _api.BlockTree,
            nodeStorage,
            _api.DbProvider!.CodeDb,
            _api.LogManager
        );

        var initMessage = new ParsedInitMessage(
            _api.ChainSpec!.ChainId,
            new UInt256(100000000),
            null,
            null
        );

        blockTreeInitializer.Initialize(initMessage);

        arbitrumGenesisBlock = _api.BlockTree.FindBlock(arbitrumGenesisBlockNum, BlockTreeLookupOptions.None);
        if (arbitrumGenesisBlock != null)
        {
            logger.Info($"Genesis initialized successfully at block {arbitrumGenesisBlock.Number}, hash: {arbitrumGenesisBlock.Hash}");
        }
        else
        {
            logger.Error($"Genesis initialization failed!");
        }

        return Task.CompletedTask;
    }
}
