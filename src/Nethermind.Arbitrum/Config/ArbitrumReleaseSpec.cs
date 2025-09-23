// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Precompiles;
using Nethermind.Core.Specs;
using Nethermind.Specs;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumReleaseSpec : ReleaseSpec, IReleaseSpec
{
    private ulong? _arbOsVersion;
    private FrozenSet<AddressAsKey>? _arbitrumPrecompiles;
    private ulong? _precompilesCachedForVersion;

    /// <summary>
    /// Gets or sets the ArbOS version. When the version changes, clears cached data
    /// (EVM instructions) to force regeneration with the new version.
    /// </summary>
    public ulong? ArbOsVersion
    {
        get => _arbOsVersion;
        set
        {
            if (Out.IsTargetBlock)
                Out.Log($"spec set arbos version={value}");

            if (_arbOsVersion == value)
                return;
            _arbOsVersion = value;

            // Clear EVM instruction caches for consistency
            // (some instructions may have different behavior based on the ArbOS version)
            IReleaseSpec spec = this;
            spec.EvmInstructionsNoTrace = null;
            spec.EvmInstructionsTraced = null;
        }
    }

    /// <summary>
    /// Provides version-aware precompile caching. Overrides base class's lazy-cached
    /// property because _precompiles is private and cannot be invalidated when
    /// ArbOS version changes on the same instance.
    /// </summary>
    FrozenSet<AddressAsKey> IReleaseSpec.Precompiles
    {
        get
        {
            if (_arbitrumPrecompiles is null || _precompilesCachedForVersion != _arbOsVersion)
            {
                _arbitrumPrecompiles = BuildPrecompilesCache();
                _precompilesCachedForVersion = _arbOsVersion;
            }
            return _arbitrumPrecompiles;
        }
    }

    public override FrozenSet<AddressAsKey> BuildPrecompilesCache()
    {
        if (Out.IsTargetBlock)
            Out.Log($"spec build precompiles arbosVersion={ArbOsVersion}");

        // Get Ethereum precompiles based on fork activation flags (EIP-198, EIP-152, EIP-2537, etc.)
        FrozenSet<AddressAsKey> ethereumPrecompiles = base.BuildPrecompilesCache();

        // Create a mutable set starting with Ethereum precompiles
        HashSet<AddressAsKey> allPrecompiles = [.. ethereumPrecompiles];

        // KZG (0x0a) handling for Arbitrum:
        // Arbitrum doesn't support blob transactions (EIP-4844), but DOES include KZG precompile
        // for fraud proofs starting from ArbOS v30 (Stylus upgrade).
        // If chainspec sets IsEip4844Enabled=true, KZG is already in ethereumPrecompiles
        // If chainspec doesn't set it, we need to add KZG manually for ArbOS v30+
        // Note: Blob transactions remain disabled regardless - that's controlled by chainspec eip4844TransitionTimestamp
        if (!IsEip4844Enabled && ArbOsVersion.GetValueOrDefault() >= ArbosVersion.Stylus)
            allPrecompiles.Add(PrecompiledAddresses.PointEvaluation);

        // Add Arbitrum precompiles based on ArbOS version
        // This ensures IsPrecompile() returns accurate results for gas charging (EIP-2929)
        foreach ((Address address, ulong minVersion) in Arbos.Precompiles.PrecompileMinArbOSVersions)
            if (ArbOsVersion.GetValueOrDefault() >= minVersion)
                allPrecompiles.Add(address);

        return allPrecompiles.ToFrozenSet();
    }
}
