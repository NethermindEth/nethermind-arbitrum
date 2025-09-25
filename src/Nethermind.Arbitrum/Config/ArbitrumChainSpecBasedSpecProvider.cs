// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumChainSpecBasedSpecProvider : ChainSpecBasedSpecProvider
{
    private readonly IArbosVersionProvider _arbosVersionProvider;

    public ArbitrumChainSpecBasedSpecProvider(
        ChainSpec chainSpec,
        IArbosVersionProvider arbosVersionProvider,
        ILogManager logManager)
        : base(chainSpec, logManager)
    {
        _arbosVersionProvider = arbosVersionProvider;
    }

    protected override ReleaseSpec CreateEmptyReleaseSpec() => new ArbitrumReleaseSpec();

    protected override ReleaseSpec CreateReleaseSpec(ChainSpec chainSpec, long releaseStartBlock, ulong? releaseStartTimestamp = null)
    {
        ArbitrumReleaseSpec releaseSpec = (ArbitrumReleaseSpec)base.CreateReleaseSpec(chainSpec, releaseStartBlock, releaseStartTimestamp);

        // Pass the provider to the spec, don't apply overrides during construction
        releaseSpec.SetArbosVersionProvider(_arbosVersionProvider);

        return releaseSpec;
    }
}

