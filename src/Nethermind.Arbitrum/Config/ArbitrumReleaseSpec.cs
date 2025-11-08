// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Precompiles;
using Nethermind.Core.Specs;
using Nethermind.Specs;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumReleaseSpec : ReleaseSpec
{
    private ulong? _arbOsVersion;

    /// <summary>
    /// Gets or sets the ArbOS version. When the version changes, clears cached data
    /// (precompiles and EVM instructions) to force regeneration with the new version.
    /// </summary>
    public ulong? ArbOsVersion
    {
        get => _arbOsVersion;
        set
        {
            if (_arbOsVersion == value) return;
            _arbOsVersion = value;

            // Clear precompile cache - it depends on the ArbOS version
            ClearPrecompilesCache();

            // Clear EVM instruction caches for consistency
            // (some instructions may have different behavior based on the ArbOS version)
            IReleaseSpec spec = this;
            spec.EvmInstructionsNoTrace = null;
            spec.EvmInstructionsTraced = null;
        }
    }

    public override FrozenSet<AddressAsKey> BuildPrecompilesCache()
    {
        // Get Ethereum precompiles based on fork activation flags (EIP-198, EIP-152, EIP-2537, etc.)
        FrozenSet<AddressAsKey> ethereumPrecompiles = base.BuildPrecompilesCache();

        // Create a mutable set starting with Ethereum precompiles
        HashSet<AddressAsKey> allPrecompiles = [..ethereumPrecompiles];

        // KZG (0x0a) handling for Arbitrum:
        // Arbitrum doesn't support blob transactions (EIP-4844), but DOES include KZG precompile for fraud proofs
        // If chainspec sets IsEip4844Enabled=true, KZG is already in ethereumPrecompiles (mainnet case)
        // If chainspec doesn't set it, we need to add KZG manually
        if (!IsEip4844Enabled)
            allPrecompiles.Add(PrecompiledAddresses.PointEvaluation);
        // Note: Blob transactions remain disabled regardless - that's controlled by chainspec eip4844TransitionTimestamp

        // Add Arbitrum precompiles based on ArbOS version
        // This ensures IsPrecompile() returns accurate results for gas charging (EIP-2929)
        foreach ((Address address, ulong minVersion) in Arbos.Precompiles.PrecompileMinArbOSVersions)
            if (ArbOsVersion.GetValueOrDefault() >= minVersion)
                allPrecompiles.Add(address);

        return allPrecompiles.ToFrozenSet();
    }
}
