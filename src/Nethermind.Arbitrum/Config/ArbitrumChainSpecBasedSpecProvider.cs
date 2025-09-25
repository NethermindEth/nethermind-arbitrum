// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumChainSpecBasedSpecProvider : ChainSpecBasedSpecProvider
{
    private readonly ulong _arbosVersion;

    public ArbitrumChainSpecBasedSpecProvider(
        ChainSpec chainSpec,
        IArbosVersionProvider arbosVersionProvider,
        ILogManager logManager)
        : base(chainSpec, logManager)
    {
        _arbosVersion = arbosVersionProvider.Get();
    }

    protected override ReleaseSpec CreateEmptyReleaseSpec() => new ArbitrumReleaseSpec();

    protected override ReleaseSpec CreateReleaseSpec(ChainSpec chainSpec, long releaseStartBlock, ulong? releaseStartTimestamp = null)
    {
        ArbitrumReleaseSpec releaseSpec = (ArbitrumReleaseSpec)base.CreateReleaseSpec(chainSpec, releaseStartBlock, releaseStartTimestamp);

        ApplyArbitrumOverrides(releaseSpec);

        return releaseSpec;
    }

    private void ApplyArbitrumOverrides(ArbitrumReleaseSpec spec)
    {
        // Shanghai EIPs (ArbOS v11+)
        bool shanghaiEnabled = _arbosVersion >= ArbosVersion.Eleven;
        spec.IsEip3651Enabled = shanghaiEnabled;
        spec.IsEip3855Enabled = shanghaiEnabled;
        spec.IsEip3860Enabled = shanghaiEnabled;

        // Cancun EIPs (ArbOS v20+)
        bool cancunEnabled = _arbosVersion >= ArbosVersion.Twenty;
        spec.IsEip1153Enabled = cancunEnabled;
        spec.IsEip4788Enabled = cancunEnabled;
        spec.IsEip5656Enabled = cancunEnabled;
        spec.IsEip6780Enabled = cancunEnabled;

        // Prague EIPs (ArbOS v40+)
        bool pragueEnabled = _arbosVersion >= ArbosVersion.Forty;
        spec.IsEip7702Enabled = pragueEnabled;
        spec.IsEip7251Enabled = pragueEnabled;
        spec.IsEip2537Enabled = pragueEnabled;
        spec.IsEip7002Enabled = pragueEnabled;
        spec.IsEip6110Enabled = pragueEnabled;

        // Disable this EIP in Arbitrum
        spec.IsEip3541Enabled = false;
    }
}

