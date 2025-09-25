// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Specs;
using System.Collections.Frozen;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumReleaseSpec : ReleaseSpec
{
    public override FrozenSet<AddressAsKey> BuildPrecompilesCache()
    {
        var basePrecompiles = base.BuildPrecompilesCache();

        HashSet<AddressAsKey> precompiles = new(basePrecompiles);
        precompiles.Add(ArbosAddresses.ArbSysAddress);
        precompiles.Add(ArbosAddresses.ArbGasInfoAddress);

        return precompiles.ToFrozenSet();
    }
}
