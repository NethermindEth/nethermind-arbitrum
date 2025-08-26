// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core.Specs;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Config;

public sealed class ArbitrumChainSpecBasedSpecProvider(
    ChainSpec chainSpec,
    Func<IArbosVersionProvider> arbosVersionProviderFactory,
    ILogManager logManager = null!)
    : ChainSpecBasedSpecProvider(chainSpec, logManager)
{
    private IArbosVersionProvider? _arbosVersionProvider;

    // Even though we mutate the spec, this is fine as each scope has its own spec provider instance
    public sealed override IReleaseSpec GetSpec(ForkActivation activation)
    {
        // Use lazy initialization because opening arbos in ArbitrumInitializeBlockchain fails
        // as it has not been initialized yet.
        _arbosVersionProvider ??= arbosVersionProviderFactory();

        IReleaseSpec spec = base.GetSpec(activation);

        ReleaseSpec mutableSpec = (ReleaseSpec)spec;

        // shanghai
        mutableSpec.IsEip4895Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Eleven;
        mutableSpec.IsEip3651Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Eleven;
        mutableSpec.IsEip3855Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Eleven;
        mutableSpec.IsEip3860Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Eleven;

        // cancun
        mutableSpec.IsEip4844Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Twenty;
        mutableSpec.IsEip1153Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Twenty;
        mutableSpec.IsEip4788Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Twenty;
        mutableSpec.IsEip5656Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Twenty;
        mutableSpec.IsEip6780Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Twenty;

        // prague
        mutableSpec.IsEip7702Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Forty;
        mutableSpec.IsEip7251Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Forty;
        mutableSpec.IsEip2537Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Forty;
        mutableSpec.IsEip7002Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Forty;
        mutableSpec.IsEip6110Enabled = _arbosVersionProvider.CurrentArbosVersion >= ArbosVersion.Forty;

        return mutableSpec;
    }
}
