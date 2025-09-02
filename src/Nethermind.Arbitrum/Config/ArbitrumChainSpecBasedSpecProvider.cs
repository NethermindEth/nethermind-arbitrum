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
    IArbosVersionProvider arbosVersionProvider,
    ILogManager logManager = null!)
    : ChainSpecBasedSpecProvider(chainSpec, logManager)
{
    // Even though we mutate the spec, this is fine as each scope has its own spec provider instance
    public override IReleaseSpec GetSpecInternal(ForkActivation activation)
    {
        IReleaseSpec spec = base.GetSpecInternal(activation);

        ReleaseSpec mutableSpec = (ReleaseSpec)spec;

        // shanghai
        mutableSpec.IsEip4895Enabled = arbosVersionProvider.Get() >= ArbosVersion.Eleven;
        mutableSpec.IsEip3651Enabled = arbosVersionProvider.Get() >= ArbosVersion.Eleven;
        mutableSpec.IsEip3855Enabled = arbosVersionProvider.Get() >= ArbosVersion.Eleven;
        mutableSpec.IsEip3860Enabled = arbosVersionProvider.Get() >= ArbosVersion.Eleven;

        // cancun
        mutableSpec.IsEip4844Enabled = arbosVersionProvider.Get() >= ArbosVersion.Twenty;
        mutableSpec.IsEip1153Enabled = arbosVersionProvider.Get() >= ArbosVersion.Twenty;
        mutableSpec.IsEip4788Enabled = arbosVersionProvider.Get() >= ArbosVersion.Twenty;
        mutableSpec.IsEip5656Enabled = arbosVersionProvider.Get() >= ArbosVersion.Twenty;
        mutableSpec.IsEip6780Enabled = arbosVersionProvider.Get() >= ArbosVersion.Twenty;

        // prague
        mutableSpec.IsEip7702Enabled = arbosVersionProvider.Get() >= ArbosVersion.Forty;
        mutableSpec.IsEip7251Enabled = arbosVersionProvider.Get() >= ArbosVersion.Forty;
        mutableSpec.IsEip2537Enabled = arbosVersionProvider.Get() >= ArbosVersion.Forty;
        mutableSpec.IsEip7002Enabled = arbosVersionProvider.Get() >= ArbosVersion.Forty;
        mutableSpec.IsEip6110Enabled = arbosVersionProvider.Get() >= ArbosVersion.Forty;

        return mutableSpec;
    }
}
