// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core.Specs;
using Nethermind.Specs;

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

        if (spec is not ReleaseSpec mutableSpec)
            return spec;

        // Get current ArbOS version
        ulong currentArbosVersion = _arbosVersionProvider.Get();

        ApplyArbitrumOverrides(mutableSpec, currentArbosVersion);

        return mutableSpec;
    }

    private static void ApplyArbitrumOverrides(ReleaseSpec spec, ulong arbosVersion)
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
        
        // Disable validator-related EIPs that don't apply to Arbitrum Layer 2
        spec.IsEip7002Enabled = false; // Withdrawal requests - not needed on L2
        spec.IsEip6110Enabled = false; // Deposit requests - not needed on L2
        spec.IsEip7251Enabled = false; // Consolidation requests - not needed on L2

        // Disable contract code validation as Arbitrum stores Stylus bytecode
        spec.IsEip3541Enabled = false;
    }
}
