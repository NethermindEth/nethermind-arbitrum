// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.State;

public interface IArbitrumWorldState : IWorldState
{
    void CreateEmptyAccountIfDeleted(Address address);
}

public class ArbitrumWorldState : WorldState, IArbitrumWorldState
{
    private readonly ArbitrumStateProvider _arbitrumStateProvider;

    public ArbitrumWorldState(IWorldStateScopeProvider scopeProvider, ILogManager? logManager)
        : this(scopeProvider, new ArbitrumStateProvider(logManager!), logManager)
    {
    }

    private ArbitrumWorldState(IWorldStateScopeProvider scopeProvider, ArbitrumStateProvider stateProvider, ILogManager? logManager)
        : base(scopeProvider, stateProvider, logManager)
    {
        _arbitrumStateProvider = stateProvider;
    }

    public void CreateEmptyAccountIfDeleted(Address address)
    {
        if (Out.IsTargetBlock)
            Out.Log("account", address.ToString(), "created-if-deleted");
        _arbitrumStateProvider.CreateEmptyAccountIfDeleted(address);
    }
}
