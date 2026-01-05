// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Math;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Represents a dimension for multidimensional gas tracking.
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
/// Fixed-size inline buffer for resource gas values.
/// </summary>
[InlineArray(MultiGas.NumResourceKinds)]
public struct GasBuffer
{
    private ulong _element0;
}

/// <summary>
/// Tracks gas usage across multiple resource kinds, while also maintaining
/// a single-dimensional total gas sum and refund amount.
/// </summary>
[DebuggerDisplay("Total = {_total}, Refund = {_refund}")]
[StructLayout(LayoutKind.Sequential)]
public struct MultiGas
{
    internal const int NumResourceKinds = 8;

    private GasBuffer _gas;
    private ulong _total;
    private ulong _refund;

    /// <summary>
    /// Gets the SSTORE refund computed at the end of the transaction.
    /// </summary>
    public readonly ulong Refund => _refund;

    /// <summary>
    /// Gets the total gas accumulated across all resource kinds.
    /// </summary>
    public readonly ulong Total => _total;

    /// <summary>
    /// Returns total minus refund. Matches Nitro's SingleGas() method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ulong SingleGas() => _total.SaturateSub(_refund);

    /// <summary>
    /// Returns a copy with the refund field set to the specified value.
    /// Called at the transaction end after calculating the capped refund.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly MultiGas WithRefund(ulong refund) => this with { _refund = refund };

    /// <summary>
    /// Returns the gas amount for the specified resource kind.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ulong Get(ResourceKind kind)
    {
        int index = (int)kind;
        if ((uint)index >= NumResourceKinds)
            ThrowArgumentOutOfRange(kind);
        return _gas[index];
    }

    /// <summary>
    /// Increments the given resource kind and the total in place.
    /// On overflow, the affected field(s) are clamped to MaxUint64.
    /// This is the primary hot-path method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Increment(ResourceKind kind, ulong gas)
    {
        int index = (int)kind;
        if ((uint)index >= NumResourceKinds)
            ThrowArgumentOutOfRange(kind);
        ref ulong kindGas = ref _gas[index];
        kindGas = kindGas.SaturateAdd(gas);
        _total = _total.SaturateAdd(gas);
    }

    /// <summary>
    /// Adds all dimensions from x into this in place.
    /// On overflow, the affected field(s) are clamped to MaxUint64.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(in MultiGas x)
    {
        Span<ulong> thisGas = _gas;
        ReadOnlySpan<ulong> otherGas = x._gas;
        for (int i = 0; i < NumResourceKinds; i++)
            thisGas[i] = thisGas[i].SaturateAdd(otherGas[i]);

        _total = _total.SaturateAdd(x._total);
        _refund = _refund.SaturateAdd(x._refund);
    }

    /// <summary>
    /// Saturating subtraction (clamps to 0 on underflow).
    /// Returns a new MultiGas with each dimension subtracted.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly MultiGas SaturatingSub(in MultiGas x)
    {
        MultiGas result = this;
        Span<ulong> resultGas = result._gas;
        ReadOnlySpan<ulong> otherGas = x._gas;

        for (int i = 0; i < NumResourceKinds; i++)
            resultGas[i] = resultGas[i].SaturateSub(otherGas[i]);

        result._total = _total.SaturateSub(x._total);
        result._refund = _refund.SaturateSub(x._refund);
        return result;
    }

    /// <summary>
    /// Safe subtraction returning (result, underflowed).
    /// Used for GetTotalUsedMultiGas calculation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly (MultiGas result, bool underflow) SafeSub(in MultiGas x)
    {
        bool underflow = _total < x._total;
        return (SaturatingSub(x), underflow);
    }

    private static void ThrowArgumentOutOfRange(ResourceKind kind)
        => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Invalid resource kind");
}
