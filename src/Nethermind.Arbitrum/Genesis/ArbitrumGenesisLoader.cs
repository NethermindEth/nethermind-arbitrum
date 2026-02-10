// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.State;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Genesis;

public class ArbitrumGenesisLoader(
    ISpecProvider specProvider,
    IWorldState worldState,
    ParsedInitMessage initMessage,
    ArbitrumGenesisStateInitializer stateInitializer,
    ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumGenesisLoader>();

    public Block Load()
    {
        _logger.Info("Loading genesis from DigestInitMessage");
        stateInitializer.ValidateInitMessage(initMessage);

        Block genesis = stateInitializer.InitializeAndBuildGenesisBlock(initMessage, worldState, specProvider);

        _logger.Info($"Arbitrum genesis block loaded from DigestInitMessage: Number={genesis.Header.Number}, Hash={genesis.Header.Hash}, StateRoot={genesis.Header.StateRoot}");

        return genesis;
    }
}
