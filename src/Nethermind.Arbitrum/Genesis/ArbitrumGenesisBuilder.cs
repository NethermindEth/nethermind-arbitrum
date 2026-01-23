// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// Builds Arbitrum genesis block by initializing ArbOS state.
/// </summary>
public class ArbitrumGenesisBuilder(
    ChainSpec chainSpec,
    ISpecProvider specProvider,
    IArbitrumSpecHelper specHelper,
    IWorldState worldState,
    ArbitrumGenesisStateInitializer stateInitializer,
    ILogManager logManager)
    : IGenesisBuilder
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumGenesisBuilder>();

    public Block Build()
    {
        ChainSpecInitMessageProvider initMessageProvider = new(chainSpec, specHelper);
        ParsedInitMessage initMessage = initMessageProvider.GetInitMessage();

        stateInitializer.ValidateInitMessage(initMessage);

        Block genesis = stateInitializer.InitializeAndBuildGenesisBlock(initMessage, worldState, specProvider);

        _logger.Info($"Arbitrum genesis block built: Number={genesis.Header.Number}, Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

        return genesis;
    }
}
