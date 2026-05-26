// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Consensus.Processing;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// No-op genesis loader for comparison mode (GenesisStateUnavailable=true).
/// Genesis will be initialized via DigestMessage from CL instead.
/// </summary>
public class NoOpGenesisLoader(ILogManager logManager) : IGenesisLoader
{
    private readonly ILogger _logger = logManager.GetClassLogger<NoOpGenesisLoader>();

    public void Load()
    {
        if (_logger.IsInfo)
            _logger.Info("GenesisStateUnavailable=true: Skipping genesis loading. " +
                         "Genesis will be initialized via DigestInitMessage from CL.");
    }
}
