// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Api.Steps;
using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.Init.Steps;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Init;

[RunnerStepDependencies(typeof(InitDatabase))]
public class ArbitrumInitializeGenesis : IStep
{
    private readonly ArbitrumNethermindApi _api;
    private readonly IArbitrumSpecHelper _specHelper;
    private readonly ILogger _logger;

    public ArbitrumInitializeGenesis(ArbitrumNethermindApi api, IArbitrumSpecHelper specHelper, ILogger logger)
    {
        _api = api;
        _specHelper = specHelper;
        _logger = _api.LogManager.GetClassLogger();
    }

    public Task Execute(CancellationToken cancellationToken)
    {
        if (!_specHelper.Enabled)
            return Task.CompletedTask;

        long arbitrumGenesisBlockNum = (long)_specHelper.GenesisBlockNum;

        if (arbitrumGenesisBlockNum == 0)
        {
            _logger.Info("Arbitrum genesis block number is 0, skipping initialization");
            return Task.CompletedTask;
        }

        Block? arbitrumGenesisBlock = _api.BlockTree!.FindBlock(arbitrumGenesisBlockNum, BlockTreeLookupOptions.None);

        if (arbitrumGenesisBlock is null)
        {
            throw new InvalidOperationException(
                $"Arbitrum genesis block {arbitrumGenesisBlockNum} not found in BlockTree. " +
                "Ensure the snapshot was downloaded correctly before this step.");
        }

        _logger.Info($"Arbitrum genesis block {arbitrumGenesisBlockNum} already exists (hash: {arbitrumGenesisBlock.Hash})");

        // Re-establish state registration by running the state initialization
        _logger.Info("Re-initializing genesis state registration...");

        IWorldState worldState = _api.WorldStateManager.GlobalWorldState;
        using (worldState.BeginScope(IWorldState.PreGenesis))
        {
            worldState.CommitTree(arbitrumGenesisBlockNum);
            _logger.Info($"State tree committed for genesis block {arbitrumGenesisBlockNum}");
        }

        return Task.CompletedTask;
    }
}
