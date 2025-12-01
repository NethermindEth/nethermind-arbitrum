// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Data;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Resource kinds for multi-dimensional gas tracking.
/// </summary>
public enum ResourceKind : byte
{
    Unknown = 0,
    Computation = 1,
    HistoryGrowth = 2,
    StorageAccess = 3,
    StorageGrowth = 4,
    L1Calldata = 5,
    L2Calldata = 6,
    WasmComputation = 7,
}

/// <summary>
/// Multi-dimensional gas tracking structure.
/// Mutable struct with array-based storage matching Nitro pattern.
/// </summary>
[JsonConverter(typeof(MultiGasJsonConverter))]
[StructLayout(LayoutKind.Auto)]
public struct MultiGas : IEquatable<MultiGas>
{
    public const int NumResourceKinds = 8;

    /// <summary>
    /// Inline array storage for gas dimensions. Stored inline in struct (no heap allocation).
    /// </summary>
    [InlineArray(NumResourceKinds)]
    private struct GasArray
    {
        private ulong _element0;
    }

    private GasArray _gas;

    /// <summary>Total gas across all dimensions</summary>
    public ulong Total;

    /// <summary>Gas refunds (e.g., from SSTORE)</summary>
    public ulong Refund;

    /// <summary>Zero multigas value</summary>
    public static MultiGas Zero => default;

    #region Factory Methods

    /// <summary>Create MultiGas with only Unknown dimension set (for operations not yet categorized, e.g. SLOAD per NIT-3484)</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas UnknownGas(ulong amount)
    {
        MultiGas result = default;
        result._gas[(int)ResourceKind.Unknown] = amount;
        result.Total = amount;
        return result;
    }

    /// <summary>Create MultiGas with only Computation dimension set</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas ComputationGas(ulong amount)
    {
        MultiGas result = default;
        result._gas[(int)ResourceKind.Computation] = amount;
        result.Total = amount;
        return result;
    }

    /// <summary>Create MultiGas with only HistoryGrowth dimension set</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas HistoryGrowthGas(ulong amount)
    {
        MultiGas result = default;
        result._gas[(int)ResourceKind.HistoryGrowth] = amount;
        result.Total = amount;
        return result;
    }

    /// <summary>Create MultiGas with only StorageAccess dimension set</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas StorageAccessGas(ulong amount)
    {
        MultiGas result = default;
        result._gas[(int)ResourceKind.StorageAccess] = amount;
        result.Total = amount;
        return result;
    }

    /// <summary>Create MultiGas with only StorageGrowth dimension set</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas StorageGrowthGas(ulong amount)
    {
        MultiGas result = default;
        result._gas[(int)ResourceKind.StorageGrowth] = amount;
        result.Total = amount;
        return result;
    }

    /// <summary>Create MultiGas with only L1Calldata dimension set</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas L1CalldataGas(ulong amount)
    {
        MultiGas result = default;
        result._gas[(int)ResourceKind.L1Calldata] = amount;
        result.Total = amount;
        return result;
    }

    /// <summary>Create MultiGas with only L2Calldata dimension set</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas L2CalldataGas(ulong amount)
    {
        MultiGas result = default;
        result._gas[(int)ResourceKind.L2Calldata] = amount;
        result.Total = amount;
        return result;
    }

    /// <summary>Create MultiGas with only WasmComputation dimension set</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MultiGas WasmComputationGas(ulong amount)
    {
        MultiGas result = default;
        result._gas[(int)ResourceKind.WasmComputation] = amount;
        result.Total = amount;
        return result;
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Create MultiGas with all values specified (used for deserialization)
    /// </summary>
    public MultiGas(
        ulong unknown,
        ulong computation,
        ulong historyGrowth,
        ulong storageAccess,
        ulong storageGrowth,
        ulong l1Calldata,
        ulong l2Calldata,
        ulong wasmComputation,
        ulong total,
        ulong refund)
    {
        _gas[(int)ResourceKind.Unknown] = unknown;
        _gas[(int)ResourceKind.Computation] = computation;
        _gas[(int)ResourceKind.HistoryGrowth] = historyGrowth;
        _gas[(int)ResourceKind.StorageAccess] = storageAccess;
        _gas[(int)ResourceKind.StorageGrowth] = storageGrowth;
        _gas[(int)ResourceKind.L1Calldata] = l1Calldata;
        _gas[(int)ResourceKind.L2Calldata] = l2Calldata;
        _gas[(int)ResourceKind.WasmComputation] = wasmComputation;
        Total = total;
        Refund = refund;
    }

    /// <summary>
    /// Create MultiGas from a span of gas values (used for deserialization)
    /// </summary>
    public MultiGas(ReadOnlySpan<ulong> gas, ulong total, ulong refund)
    {
        if (gas.Length != NumResourceKinds)
            throw new ArgumentException($"Expected {NumResourceKinds} gas values", nameof(gas));

        for (int i = 0; i < NumResourceKinds; i++)
            _gas[i] = gas[i];
        Total = total;
        Refund = refund;
    }

    #endregion

    #region Accessors

    /// <summary>Get gas for a specific resource kind</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ulong Get(ResourceKind kind) => _gas[(int)kind];

    /// <summary>Single-dimensional gas (total - refund)</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ulong SingleGas() => Total - Refund;

    /// <summary>Get a read-only span of all gas dimensions</summary>
    public readonly ReadOnlySpan<ulong> AsSpan() =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<GasArray, ulong>(ref Unsafe.AsRef(in _gas)), NumResourceKinds);

    #endregion

    #region Immutable Operations (return new MultiGas)

    /// <summary>Immutable: Returns new MultiGas with incremented value</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly MultiGas SaturatingIncrement(ResourceKind kind, ulong amount)
    {
        MultiGas result = this;
        result.SaturatingIncrementInto(kind, amount);
        return result;
    }

    /// <summary>Immutable: Returns new MultiGas with added values</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly MultiGas SaturatingAdd(in MultiGas other)
    {
        MultiGas result = this;
        result.SaturatingAddInto(in other);
        return result;
    }

    /// <summary>Immutable: Returns new MultiGas with subtracted values</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly MultiGas SaturatingSub(in MultiGas other)
    {
        MultiGas result = this;
        result.SaturatingSubInto(in other);
        return result;
    }

    /// <summary>Immutable: Returns new MultiGas with refund set</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly MultiGas WithRefund(ulong refund)
    {
        MultiGas result = this;
        result.Refund = refund;
        return result;
    }

    /// <summary>Immutable: Returns new MultiGas with refund adjusted</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly MultiGas AddRefund(long refundDelta)
    {
        MultiGas result = this;
        result.AddRefundInto(refundDelta);
        return result;
    }

    #endregion

    #region Mutable Operations (modify in place - hot path optimization)

    /// <summary>Mutable: Increments value in place</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SaturatingIncrementInto(ResourceKind kind, ulong amount)
    {
        ref ulong slot = ref _gas[(int)kind];
        slot = SaturatingAddUInt64(slot, amount);
        Total = SaturatingAddUInt64(Total, amount);
    }

    /// <summary>Mutable: Adds values in place</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SaturatingAddInto(in MultiGas other)
    {
        for (int i = 0; i < NumResourceKinds; i++)
            _gas[i] = SaturatingAddUInt64(_gas[i], other._gas[i]);
        Total = SaturatingAddUInt64(Total, other.Total);
        Refund = SaturatingAddUInt64(Refund, other.Refund);
    }

    /// <summary>Mutable: Subtracts values in place</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SaturatingSubInto(in MultiGas other)
    {
        for (int i = 0; i < NumResourceKinds; i++)
            _gas[i] = SaturatingSubUInt64(_gas[i], other._gas[i]);
        Total = SaturatingSubUInt64(Total, other.Total);
        Refund = SaturatingSubUInt64(Refund, other.Refund);
    }

    /// <summary>Mutable: Adjusts refund in place</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRefundInto(long refundDelta)
    {
        long newRefund = (long)Refund + refundDelta;
        Refund = newRefund > 0 ? (ulong)newRefund : 0;
    }

    /// <summary>Mutable: Sets refund in place</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetRefund(ulong refund) => Refund = refund;

    #endregion

    #region Utility Methods

    /// <summary>Check if all multigas values are zero</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsZero()
    {
        if (Total != 0 || Refund != 0)
            return false;

        for (int i = 0; i < NumResourceKinds; i++)
        {
            if (_gas[i] != 0)
                return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SaturatingAddUInt64(ulong a, ulong b)
    {
        ulong result = a + b;
        return result < a ? ulong.MaxValue : result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong SaturatingSubUInt64(ulong a, ulong b)
    {
        return a > b ? a - b : 0;
    }

    #endregion

    #region Equality

    public readonly bool Equals(MultiGas other)
    {
        if (Total != other.Total || Refund != other.Refund)
            return false;

        for (int i = 0; i < NumResourceKinds; i++)
        {
            if (_gas[i] != other._gas[i])
                return false;
        }
        return true;
    }

    public override readonly bool Equals(object? obj) => obj is MultiGas other && Equals(other);

    public override readonly int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(_gas[(int)ResourceKind.Computation]);
        hash.Add(_gas[(int)ResourceKind.StorageAccess]);
        hash.Add(Total);
        hash.Add(Refund);
        return hash.ToHashCode();
    }

    public static bool operator ==(MultiGas left, MultiGas right) => left.Equals(right);
    public static bool operator !=(MultiGas left, MultiGas right) => !left.Equals(right);

    #endregion

    public override readonly string ToString() =>
        $"MultiGas(Comp:{_gas[(int)ResourceKind.Computation]}, HistGrow:{_gas[(int)ResourceKind.HistoryGrowth]}, " +
        $"StorAccess:{_gas[(int)ResourceKind.StorageAccess]}, StorGrow:{_gas[(int)ResourceKind.StorageGrowth]}, " +
        $"L1:{_gas[(int)ResourceKind.L1Calldata]}, L2:{_gas[(int)ResourceKind.L2Calldata]}, " +
        $"Wasm:{_gas[(int)ResourceKind.WasmComputation]}, Total:{Total}, Refund:{Refund})";
}
