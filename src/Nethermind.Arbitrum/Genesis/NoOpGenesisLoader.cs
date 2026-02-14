// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.Processing;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// No-op genesis loader for comparison mode (GenesisStateUnavailable=true).
/// Genesis will be initialized via DigestInitMessage from CL instead.
/// </summary>
public class NoOpGenesisLoader(ILogManager logManager) : IGenesisLoader
{
    private readonly ILogger _logger = logManager.GetClassLogger<NoOpGenesisLoader>();

    public void Load()
    {
        _logger.Info("GenesisStateUnavailable=true: Skipping genesis loading. " +
                     "Genesis will be initialized via DigestInitMessage from CL.");
    }
}
