// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Core.Specs;

namespace Nethermind.Arbitrum.Config;

public sealed class ArbitrumDynamicSpecProvider(
    ISpecProvider baseSpecProvider,
    IArbosVersionProvider arbosVersionProvider) : SpecProviderDecorator(baseSpecProvider)
{
    private readonly ConcurrentDictionary<(ForkActivation, ulong), ArbitrumReleaseSpec> _specCache = new();

    public override IReleaseSpec GetSpecInternal(ForkActivation activation)
    {
        IReleaseSpec spec = base.GetSpecInternal(activation);

        if (spec is not ArbitrumReleaseSpec baseSpec)
            return spec;

        ulong currentArbosVersion = arbosVersionProvider.Get();

        // Fast path: if base spec already has correct version, use it
        if (baseSpec.ArbOsVersion == currentArbosVersion)
            return baseSpec;

        // Get or create spec for this (activation, version) combination
        // ConcurrentDictionary.GetOrAdd ensures thread-safe lazy initialization
        (ForkActivation activation, ulong currentArbosVersion) key = (activation, currentArbosVersion);
        return _specCache.GetOrAdd(key, _ =>
        {
            // Clone the base spec to preserve all chainspec configuration
            // MemberwiseClone creates a shallow copy which is sufficient for our needs
            ArbitrumReleaseSpec newSpec = (ArbitrumReleaseSpec)baseSpec.Clone();

            // Apply ArbOS version-specific overrides
            // This will set ArbOsVersion and clear cached precompiles/EVM instructions
            ApplyArbitrumOverrides(newSpec, currentArbosVersion);

            return newSpec;
        });
    }

    private static void ApplyArbitrumOverrides(ArbitrumReleaseSpec spec, ulong arbosVersion)
    {
        spec.ArbOsVersion = arbosVersion;

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

        // RIP-7212: P-256 precompile (ArbOS v30+)
        spec.IsRip7212Enabled = arbosVersion >= ArbosVersion.Thirty;

        // Prague EIPs (ArbOS v40+)
        bool pragueV40Enabled = arbosVersion >= ArbosVersion.Forty;
        spec.IsEip7702Enabled = pragueV40Enabled;  // EIP-7702: Set EOA code from contract

        // EIP-2935: Historical block hash storage (ArbOS v40+)
        // Arbitrum uses a larger ring buffer (393,168 blocks vs Ethereum's 8,191)
        // This provides ~1 day of history at Arbitrum's 0.22s block time
        spec.IsEip2935Enabled = pragueV40Enabled;
        spec.IsEip7709Enabled = pragueV40Enabled; // BLOCKHASH opcode reads from state

        // Prague/Osaka EIPs (ArbOS v50+) - "Dia" release
        bool pragueV50Enabled = arbosVersion >= ArbosVersion.Fifty;
        spec.IsEip2537Enabled = pragueV50Enabled;  // EIP-2537: BLS12-381 precompiles (0x0b-0x13)
        spec.IsEip7823Enabled = pragueV50Enabled;  // EIP-7823: MODEXP 1024-byte upper bounds
        spec.IsEip7883Enabled = pragueV50Enabled;  // EIP-7883: MODEXP gas pricing changes
        spec.IsEip7939Enabled = pragueV50Enabled;  // EIP-7939: CLZ opcode (0x1e) for counting leading zeros
        spec.IsEip7951Enabled = pragueV50Enabled;  // EIP-7951: P-256 precompile gas cost update (3450 -> 6900)

        // Disable contract code validation as Arbitrum stores Stylus bytecode
        spec.IsEip3541Enabled = false;
    }
}
