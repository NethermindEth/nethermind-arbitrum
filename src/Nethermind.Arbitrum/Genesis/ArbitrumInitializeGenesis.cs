// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.Init.Steps;

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

        if (arbitrumGenesisBlockNum == 0)
        {
            logger.Info("Arbitrum genesis block number is 0, skipping initialization");
            return Task.CompletedTask;
        }

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
        }

        return Task.CompletedTask;
    }
}
