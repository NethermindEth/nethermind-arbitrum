// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.State.SnapServer;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.State;

public class ArbWorldStateManager(IWorldStateManager worldStateManager, ILogManager logManager): IWorldStateManager
{
    public IWorldState GlobalWorldState { get; } = new ArbWorldState(worldStateManager.GlobalWorldState, logManager);

    public IStateReader GlobalStateReader => worldStateManager.GlobalStateReader;

    public ISnapServer? SnapServer => worldStateManager.SnapServer;

    public IReadOnlyKeyValueStore? HashServer => worldStateManager.HashServer;

    public IWorldState CreateResettableWorldState()
    {
        // should create a new one everytime
        return new ArbWorldState(worldStateManager.CreateResettableWorldState(), logManager);
    }

    public IWorldState CreateWorldStateForWarmingUp(IWorldState forWarmup)
    {
        // should create a new one everytime
        return new ArbWorldState(worldStateManager.CreateWorldStateForWarmingUp(forWarmup), logManager);
    }

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
    {
        add => worldStateManager.ReorgBoundaryReached += value;
        remove => worldStateManager.ReorgBoundaryReached -= value;
    }

    public IOverridableWorldScope CreateOverridableWorldScope()
    {
        return new ArbOverridableWorldStateManager(worldStateManager.CreateOverridableWorldScope(), logManager);
    }

    public bool VerifyTrie(BlockHeader stateAtBlock, CancellationToken cancellationToken)
    {
        return worldStateManager.VerifyTrie(stateAtBlock, cancellationToken);
    }

    public void FlushCache(CancellationToken cancellationToken)
    {
        worldStateManager.FlushCache(cancellationToken);
    }
}
