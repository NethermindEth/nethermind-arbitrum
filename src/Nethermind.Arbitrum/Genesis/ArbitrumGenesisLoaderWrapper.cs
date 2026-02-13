// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.Processing;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Genesis;

/// <summary>
/// Wraps the core GenesisLoader to handle Arbitrum-specific genesis loading behavior.
/// When GenesisStateUnavailable is true (e.g., system tests with external ELs),
/// skips genesis loading and allows DigestInitMessage to initialize genesis later.
/// </summary>
public class ArbitrumGenesisLoaderWrapper(
    ChainSpec chainSpec,
    IGenesisLoader innerLoader,
    ILogManager logManager) : IGenesisLoader
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumGenesisLoaderWrapper>();

    public void Load()
    {
        if (chainSpec.GenesisStateUnavailable)
        {
            _logger.Info("GenesisStateUnavailable=true: Skipping genesis loading at startup. " +
                         "Genesis will be initialized via DigestInitMessage from CL.");
            return;
        }

        innerLoader.Load();
    }
}
