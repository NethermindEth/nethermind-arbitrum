// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Specs;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumReleaseSpec : ReleaseSpec
{
    private IArbosVersionProvider? _arbosVersionProvider;
    private ulong? _cachedArbosVersion;
    private bool _overridesApplied;

    public void SetArbosVersionProvider(IArbosVersionProvider provider)
    {
        _arbosVersionProvider = provider;
    }

    private void EnsureOverridesApplied()
    {
        if (_arbosVersionProvider == null) return;

        ulong currentVersion = _arbosVersionProvider.Get();
        if (!_overridesApplied || _cachedArbosVersion != currentVersion)
        {
            _cachedArbosVersion = currentVersion;
            _overridesApplied = true;
            ApplyArbosVersionOverrides(currentVersion);
        }
    }

    private void ApplyArbosVersionOverrides(ulong arbosVersion)
    {
        // Shanghai EIPs (ArbOS v11+)
        bool shanghaiEnabled = arbosVersion >= ArbosVersion.Eleven;
        base.IsEip3651Enabled = shanghaiEnabled;
        base.IsEip3855Enabled = shanghaiEnabled;
        base.IsEip3860Enabled = shanghaiEnabled;

        // Cancun EIPs (ArbOS v20+)
        bool cancunEnabled = arbosVersion >= ArbosVersion.Twenty;
        base.IsEip1153Enabled = cancunEnabled;
        base.IsEip4788Enabled = cancunEnabled;
        base.IsEip5656Enabled = cancunEnabled;
        base.IsEip6780Enabled = cancunEnabled;

        // Prague EIPs (ArbOS v40+)
        bool pragueEnabled = arbosVersion >= ArbosVersion.Forty;
        base.IsEip7702Enabled = pragueEnabled;
        base.IsEip7251Enabled = pragueEnabled;
        base.IsEip2537Enabled = pragueEnabled;
        base.IsEip7002Enabled = pragueEnabled;
        base.IsEip6110Enabled = pragueEnabled;

        // Disable contract code validation as Arbitrum stores Stylus bytecode in code storage
        base.IsEip3541Enabled = false;
    }

    public override FrozenSet<AddressAsKey> BuildPrecompilesCache()
    {
        var basePrecompiles = base.BuildPrecompilesCache();

        HashSet<AddressAsKey> precompiles = new(basePrecompiles);
        precompiles.Add(ArbosAddresses.ArbSysAddress);
        precompiles.Add(ArbosAddresses.ArbGasInfoAddress);

        return precompiles.ToFrozenSet();
    }
}
