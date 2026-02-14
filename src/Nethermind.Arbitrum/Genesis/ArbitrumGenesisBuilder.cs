// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// Builds Arbitrum genesis block by initializing ArbOS state.
/// When GenesisStateUnavailable is true (e.g., system tests with external ELs),
/// skips state initialization and waits for DigestInitMessage from the CL.
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
        if (chainSpec.GenesisStateUnavailable)
        {
            _logger.Info("GenesisStateUnavailable=true: Skipping ArbOS state initialization. " +
                         "Genesis state will be initialized via DigestInitMessage from CL.");

            Block genesis = chainSpec.Genesis;
            genesis.Header.Hash = genesis.Header.CalculateHash();

            _logger.Info($"Arbitrum genesis block built (state unavailable): Number={genesis.Header.Number}, " +
                         $"Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

            return genesis;
        }

        ChainSpecInitMessageProvider initMessageProvider = new(chainSpec, specHelper);
        ParsedInitMessage initMessage = initMessageProvider.GetInitMessage();

        stateInitializer.ValidateInitMessage(initMessage);

        Block genesisBlock = stateInitializer.InitializeAndBuildGenesisBlock(initMessage, worldState, specProvider);

        _logger.Info($"Arbitrum genesis block built: Number={genesisBlock.Header.Number}, Hash={genesisBlock.Header.Hash}, StateRoot={genesisBlock.Header.StateRoot}");

        return genesisBlock;
    }
}
