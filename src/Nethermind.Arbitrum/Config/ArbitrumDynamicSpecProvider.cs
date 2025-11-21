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

        // RIP-7212: P-256 precompile (ArbOS v30+)
        spec.IsRip7212Enabled = arbosVersion >= ArbosVersion.Thirty;

        // EIP-2935: Historical block hash storage (ArbOS v40+)
        // Arbitrum uses a larger ring buffer (393,168 blocks vs Ethereum's 8,191)
        // This provides ~1 day of history at Arbitrum's 0.22s block time
        bool eip2935Enabled = arbosVersion >= ArbosVersion.Forty;
        spec.IsEip2935Enabled = eip2935Enabled;
        spec.IsEip7709Enabled = eip2935Enabled; // BLOCKHASH opcode reads from state

        // Disable contract code validation as Arbitrum stores Stylus bytecode
        spec.IsEip3541Enabled = false;

        spec.ArbOsVersion = arbosVersion;
    }
}
