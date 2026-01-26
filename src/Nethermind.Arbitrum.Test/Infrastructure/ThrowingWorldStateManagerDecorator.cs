// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.State;
using Nethermind.State.SnapServer;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Test.Infrastructure;

/// <summary>
/// Decorator that wraps IWorldStateManager and can be configured to throw on FlushCache.
/// Used for testing exception handling in maintenance operations.
/// </summary>
public sealed class ThrowingWorldStateManagerDecorator(
    IWorldStateManager inner,
    Exception? exceptionToThrow = null)
    : IWorldStateManager
{
    public IWorldStateScopeProvider GlobalWorldState => inner.GlobalWorldState;
    public IStateReader GlobalStateReader => inner.GlobalStateReader;
    public ISnapServer? SnapServer => inner.SnapServer;
    public IReadOnlyKeyValueStore? HashServer => inner.HashServer;

    public event EventHandler<ReorgBoundaryReached>? ReorgBoundaryReached
    {
        add => inner.ReorgBoundaryReached += value;
        remove => inner.ReorgBoundaryReached -= value;
    }

    public IWorldStateScopeProvider CreateResettableWorldState() => inner.CreateResettableWorldState();
    public IOverridableWorldScope CreateOverridableWorldScope() => inner.CreateOverridableWorldScope();

    public bool VerifyTrie(BlockHeader stateAtBlock, CancellationToken cancellationToken)
        => inner.VerifyTrie(stateAtBlock, cancellationToken);

    public void FlushCache(CancellationToken cancellationToken)
    {
        if (exceptionToThrow is not null)
            throw exceptionToThrow;
        inner.FlushCache(cancellationToken);
    }
}
