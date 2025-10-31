// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core.Specs;

namespace Nethermind.Arbitrum.Config;

public sealed class ArbitrumDynamicSpecProvider : SpecProviderDecorator
{
    private readonly IArbosVersionProvider _arbosVersionProvider;

    public ArbitrumDynamicSpecProvider(
        ISpecProvider baseSpecProvider,
        IArbosVersionProvider arbosVersionProvider)
        : base(baseSpecProvider)
    {
        _arbosVersionProvider = arbosVersionProvider;
    }

    public override IReleaseSpec GetSpecInternal(ForkActivation activation)
    {
        IReleaseSpec spec = base.GetSpecInternal(activation);

        if (spec is not ArbitrumReleaseSpec mutableSpec)
            return spec;

        // Get current ArbOS version
        ulong currentArbosVersion = _arbosVersionProvider.Get();

        if (mutableSpec.ArbOsVersion == currentArbosVersion)
            return mutableSpec;

        ApplyArbitrumOverrides(mutableSpec, currentArbosVersion);
        // Clear EVM instruction caches to force regeneration with updated spec
        spec.EvmInstructionsNoTrace = null;
        spec.EvmInstructionsTraced = null;

        return mutableSpec;
    }

    private static void ApplyArbitrumOverrides(ArbitrumReleaseSpec spec, ulong arbosVersion)
    {
        // Shanghai EIPs (ArbOS v11+)
        bool shanghaiEnabled = arbosVersion >= ArbosVersion.Eleven;
        spec.IsEip3651Enabled = shanghaiEnabled;
        spec.IsEip3855Enabled = shanghaiEnabled;
        spec.IsEip3860Enabled = shanghaiEnabled;

        // Cancun EIPs (ArbOS v20+)
        bool cancunEnabled = arbosVersion >= ArbosVersion.Twenty;
        spec.IsEip1153Enabled = cancunEnabled;
        spec.IsEip4788Enabled = cancunEnabled;
        spec.IsEip5656Enabled = cancunEnabled;
        spec.IsEip6780Enabled = cancunEnabled;

        // Prague EIPs (ArbOS v40+)
        bool pragueEnabled = arbosVersion >= ArbosVersion.Forty;
        spec.IsEip7702Enabled = pragueEnabled;
        spec.IsEip2537Enabled = pragueEnabled;

        // Disable contract code validation as Arbitrum stores Stylus bytecode
        spec.IsEip3541Enabled = false;

        spec.ArbOsVersion = arbosVersion;
    }
}
