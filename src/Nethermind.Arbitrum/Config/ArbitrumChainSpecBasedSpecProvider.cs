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
    public sealed override IReleaseSpec GetSpec(ForkActivation activation)
    {
        IReleaseSpec spec = base.GetSpec(activation);
        ReleaseSpec mutableSpec = (ReleaseSpec)spec;
        ulong currentArbosVersion = arbosVersionProvider.Get();

        // Shanghai EIPs (ArbOS v11+)
        bool shanghaiEnabled = currentArbosVersion >= ArbosVersion.Eleven;
        mutableSpec.IsEip3651Enabled = shanghaiEnabled;
        mutableSpec.IsEip3855Enabled = shanghaiEnabled;
        mutableSpec.IsEip3860Enabled = shanghaiEnabled;

        // Cancun EIPs (ArbOS v20+)
        bool cancunEnabled = currentArbosVersion >= ArbosVersion.Twenty;
        mutableSpec.IsEip1153Enabled = cancunEnabled;
        mutableSpec.IsEip4788Enabled = cancunEnabled;
        mutableSpec.IsEip5656Enabled = cancunEnabled;
        mutableSpec.IsEip6780Enabled = cancunEnabled;

        // Prague EIPs (ArbOS v40+)
        bool pragueEnabled = currentArbosVersion >= ArbosVersion.Forty;
        mutableSpec.IsEip7702Enabled = pragueEnabled;
        mutableSpec.IsEip7251Enabled = pragueEnabled;
        mutableSpec.IsEip2537Enabled = pragueEnabled;
        mutableSpec.IsEip7002Enabled = pragueEnabled;
        mutableSpec.IsEip6110Enabled = pragueEnabled;

        return mutableSpec;
    }
}
