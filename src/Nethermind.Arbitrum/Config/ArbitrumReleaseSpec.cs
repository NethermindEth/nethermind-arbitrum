// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Specs;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumReleaseSpec : ReleaseSpec
{
    public void SetArbosVersionProvider(IArbosVersionProvider provider)
    {
        if (provider != null)
        {
            ApplyArbosVersionOverrides(provider.Get());
        }
    }

    public override FrozenSet<AddressAsKey> BuildPrecompilesCache()
    {
        var basePrecompiles = base.BuildPrecompilesCache();

        HashSet<AddressAsKey> precompiles = new(basePrecompiles);
        precompiles.Add(ArbosAddresses.ArbSysAddress);
        precompiles.Add(ArbosAddresses.ArbGasInfoAddress);

        return precompiles.ToFrozenSet();
    }

    private void ApplyArbosVersionOverrides(ulong arbosVersion)
    {
        // Shanghai EIPs (ArbOS v11+)
        bool shanghaiEnabled = arbosVersion >= ArbosVersion.Eleven;
        IsEip3651Enabled = shanghaiEnabled;
        IsEip3855Enabled = shanghaiEnabled;
        IsEip3860Enabled = shanghaiEnabled;

        // Cancun EIPs (ArbOS v20+)
        bool cancunEnabled = arbosVersion >= ArbosVersion.Twenty;
        IsEip1153Enabled = cancunEnabled;
        IsEip4788Enabled = cancunEnabled;
        IsEip5656Enabled = cancunEnabled;
        IsEip6780Enabled = cancunEnabled;

        // Prague EIPs (ArbOS v40+)
        bool pragueEnabled = arbosVersion >= ArbosVersion.Forty;
        IsEip7702Enabled = pragueEnabled;
        IsEip7251Enabled = pragueEnabled;
        IsEip2537Enabled = pragueEnabled;
        IsEip7002Enabled = pragueEnabled;
        IsEip6110Enabled = pragueEnabled;

        // Disable contract code validation as Arbitrum stores Stylus bytecode in code storage
        IsEip3541Enabled = false;
    }
}
