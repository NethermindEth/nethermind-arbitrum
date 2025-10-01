// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumChainSpecBasedSpecProvider : ChainSpecBasedSpecProvider
{
    public ArbitrumChainSpecBasedSpecProvider(
        ChainSpec chainSpec,
        ILogManager logManager)
        : base(chainSpec, logManager)
    {
    }

    protected override ReleaseSpec CreateEmptyReleaseSpec() => new ArbitrumReleaseSpec();
}

