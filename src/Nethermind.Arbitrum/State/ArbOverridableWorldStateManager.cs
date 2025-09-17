// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.State;

public class ArbOverridableWorldStateManager(IOverridableWorldScope overridableWorldScope): IOverridableWorldScope
{
    public IDisposable BeginScope(BlockHeader? header)
    {
        return overridableWorldScope.BeginScope(header);
    }

    public IWorldState WorldState => new ArbWorldState(overridableWorldScope.WorldState);

    public IStateReader GlobalStateReader => overridableWorldScope.GlobalStateReader;
}
