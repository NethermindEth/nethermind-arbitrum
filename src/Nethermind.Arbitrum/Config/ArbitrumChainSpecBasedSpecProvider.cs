// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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

