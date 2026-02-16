// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

namespace Nethermind.Arbitrum.Arbos.Storage;

/// <summary>
/// Represents a gas constraint with target gas per second, adjustment window, and backlog.
/// Each constraint is stored in a separate sub-storage with three ulong values at offsets 0, 1, 2.
/// </summary>
public class GasConstraint
{
    private const ulong TargetOffset = 0;
    private const ulong AdjustmentWindowOffset = 1;
    private const ulong BacklogOffset = 2;

    private readonly ArbosStorage _storage;
    private readonly ArbosStorageBackedULong _target;
    private readonly ArbosStorageBackedULong _adjustmentWindow;
    private readonly ArbosStorageBackedULong _backlog;

    public GasConstraint(ArbosStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        _storage = storage;
        _target = new ArbosStorageBackedULong(storage, TargetOffset);
        _adjustmentWindow = new ArbosStorageBackedULong(storage, AdjustmentWindowOffset);
        _backlog = new ArbosStorageBackedULong(storage, BacklogOffset);
    }

    /// <summary>
    /// Gets the gas target per second for this constraint.
    /// </summary>
    public ulong Target => _target.Get();

    /// <summary>
    /// Gets the adjustment window in seconds for this constraint.
    /// </summary>
    public ulong AdjustmentWindow => _adjustmentWindow.Get();

    /// <summary>
    /// Gets the backlog value for this constraint.
    /// </summary>
    public ulong Backlog => _backlog.Get();

    /// <summary>
    /// Sets the gas target per second for this constraint.
    /// </summary>
    public void SetTarget(ulong value)
    {
        _target.Set(value);
    }

    /// <summary>
    /// Sets the adjustment window in seconds for this constraint.
    /// </summary>
    public void SetAdjustmentWindow(ulong value)
    {
        _adjustmentWindow.Set(value);
    }

    /// <summary>
    /// Sets the backlog value for this constraint.
    /// </summary>
    public void SetBacklog(ulong value)
    {
        _backlog.Set(value);
    }

    /// <summary>
    /// Clears all fields of this constraint.
    /// </summary>
    public void Clear()
    {
        _storage.Clear(TargetOffset);
        _storage.Clear(AdjustmentWindowOffset);
        _storage.Clear(BacklogOffset);
    }
}
