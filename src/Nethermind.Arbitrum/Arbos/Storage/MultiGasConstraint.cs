// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Math;

namespace Nethermind.Arbitrum.Arbos.Storage;

/// <summary>
/// Operation type for backlog updates.
/// </summary>
internal enum BacklogOperation
{
    Grow,
    Shrink
}

/// <summary>
/// Represents a multi-gas pricing constraint that combines several gas resource types,
/// each with a corresponding weight (0 = unused).
/// <para>
/// Storage layout (12 slots total):
/// <list type="bullet">
///   <item>[0] target (uint64) - gas target per second</item>
///   <item>[1] adjustmentWindow (uint32 stored as uint64)</item>
///   <item>[2] backlog (uint64) - weighted backlog accumulator</item>
///   <item>[3] maxWeight (uint64) - maximum weight across all resources</item>
///   <item>[4..11] weightedResources[0..7] (8 x uint64)</item>
/// </list>
/// </para>
/// </summary>
public class MultiGasConstraint
{
    private const ulong TargetOffset = 0;
    private const ulong AdjustmentWindowOffset = 1;
    private const ulong BacklogOffset = 2;
    private const ulong MaxWeightOffset = 3;
    private const ulong WeightedResourcesBaseOffset = 4;

    private readonly ArbosStorage _storage;
    private readonly ArbosStorageBackedULong _target;
    private readonly ArbosStorageBackedULong _adjustmentWindow;
    private readonly ArbosStorageBackedULong _backlog;
    private readonly ArbosStorageBackedULong _maxWeight;
    private readonly ArbosStorageBackedULong[] _weightedResources;

    public MultiGasConstraint(ArbosStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        _storage = storage;
        _target = new ArbosStorageBackedULong(storage, TargetOffset);
        _adjustmentWindow = new ArbosStorageBackedULong(storage, AdjustmentWindowOffset);
        _backlog = new ArbosStorageBackedULong(storage, BacklogOffset);
        _maxWeight = new ArbosStorageBackedULong(storage, MaxWeightOffset);

        _weightedResources = new ArbosStorageBackedULong[MultiGas.NumResourceKinds];
        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            _weightedResources[i] = new ArbosStorageBackedULong(storage, WeightedResourcesBaseOffset + (ulong)i);
        }
    }

    /// <summary>
    /// Gets the gas target per second for this constraint.
    /// </summary>
    public ulong Target => _target.Get();

    /// <summary>
    /// Gets the adjustment window in seconds for this constraint.
    /// Saturates to uint.MaxValue if the stored value exceeds uint range.
    /// </summary>
    public uint AdjustmentWindow
    {
        get
        {
            ulong value = _adjustmentWindow.Get();
            return value > uint.MaxValue ? uint.MaxValue : (uint)value;
        }
    }

    /// <summary>
    /// Gets the weighted backlog value for this constraint.
    /// </summary>
    public ulong Backlog => _backlog.Get();

    /// <summary>
    /// Gets the maximum weight across all resources.
    /// </summary>
    public ulong MaxWeight => _maxWeight.Get();

    /// <summary>
    /// Sets the gas target per second for this constraint.
    /// </summary>
    public void SetTarget(ulong value) => _target.Set(value);

    /// <summary>
    /// Sets the adjustment window in seconds for this constraint.
    /// </summary>
    public void SetAdjustmentWindow(uint value) => _adjustmentWindow.Set(value);

    /// <summary>
    /// Sets the backlog value for this constraint.
    /// </summary>
    public void SetBacklog(ulong value) => _backlog.Set(value);

    /// <summary>
    /// Gets the weight for the specified resource kind.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If kind is invalid.</exception>
    public ulong GetResourceWeight(ResourceKind kind)
    {
        int index = (int)kind;
        if ((uint)index >= MultiGas.NumResourceKinds)
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Invalid resource kind");
        return _weightedResources[index].Get();
    }

    /// <summary>
    /// Sets per-resource weight multipliers for this constraint.
    /// Updates maxWeight to track the maximum weight.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">If any resource kind is invalid.</exception>
    public void SetResourceWeights(Dictionary<ResourceKind, ulong> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        ulong maxWeight = 0;

        // Validate all keys and find max weight
        foreach ((ResourceKind kind, ulong weight) in weights)
        {
            int index = (int)kind;
            if ((uint)index >= MultiGas.NumResourceKinds)
                throw new ArgumentOutOfRangeException(nameof(weights), kind, "Invalid resource kind in weights");
            if (weight > maxWeight)
                maxWeight = weight;
        }

        // Set all weights (unspecified ones become 0)
        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            ulong weight = weights.GetValueOrDefault((ResourceKind)i, 0UL);
            _weightedResources[i].Set(weight);
        }

        _maxWeight.Set(maxWeight);
    }

    /// <summary>
    /// Returns all resources that have non-zero weights.
    /// </summary>
    public Dictionary<ResourceKind, ulong> GetResourcesWithWeights()
    {
        var result = new Dictionary<ResourceKind, ulong>();
        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            ulong weight = _weightedResources[i].Get();
            if (weight != 0)
            {
                result[(ResourceKind)i] = weight;
            }
        }
        return result;
    }

    /// <summary>
    /// Returns the list of resource kinds that have non-zero weights.
    /// </summary>
    public ResourceKind[] GetUsedResources()
    {
        var result = new List<ResourceKind>();
        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            ulong weight = _weightedResources[i].Get();
            if (weight != 0)
            {
                result.Add((ResourceKind)i);
            }
        }
        return [.. result];
    }

    /// <summary>
    /// Adds the resource usage in multiGas to this constraint's backlog,
    /// weighted by each resource's weight multiplier.
    /// </summary>
    public void GrowBacklog(MultiGas multiGas)
    {
        UpdateBacklog(BacklogOperation.Grow, multiGas);
    }

    /// <summary>
    /// Subtracts the resource usage in multiGas from this constraint's backlog,
    /// weighted by each resource's weight multiplier.
    /// Clamps to zero on underflow.
    /// </summary>
    public void ShrinkBacklog(MultiGas multiGas)
    {
        UpdateBacklog(BacklogOperation.Shrink, multiGas);
    }

    private void UpdateBacklog(BacklogOperation op, MultiGas multiGas)
    {
        ulong totalBacklog = Backlog;

        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            ulong weight = _weightedResources[i].Get();
            if (weight == 0)
                continue;

            ulong resourceAmount = multiGas.Get((ResourceKind)i);
            ulong weightedAmount = resourceAmount.SaturateMul(weight);

            totalBacklog = op == BacklogOperation.Grow
                ? totalBacklog.SaturateAdd(weightedAmount)
                : totalBacklog.SaturateSub(weightedAmount);
        }

        SetBacklog(totalBacklog);
    }

    /// <summary>
    /// Clears all fields of this constraint including all resource weights.
    /// </summary>
    public void Clear()
    {
        _storage.Clear(TargetOffset);
        _storage.Clear(AdjustmentWindowOffset);
        _storage.Clear(BacklogOffset);
        _storage.Clear(MaxWeightOffset);

        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            _storage.Clear(WeightedResourcesBaseOffset + (ulong)i);
        }
    }
}
