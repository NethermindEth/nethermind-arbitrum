// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Math;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Represents a dimension for multidimensional gas tracking.
/// Layout mirrors Nitro <c>multigas.ResourceKind</c> (/v3.10.0/go-ethereum/arbitrum/multigas/resources.go).
/// </summary>
public enum ResourceKind : byte
{
    Unknown = 0,
    Computation = 1,
    HistoryGrowth = 2,
    StorageAccessRead = 3,
    StorageAccessWrite = 4,
    StorageGrowth = 5,
    SingleDim = 6,
    L2Calldata = 7,
    WasmComputation = 8,
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
    internal const int NumResourceKinds = 9;

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
    /// Returns true if all gas values are zero.
    /// </summary>
    public readonly bool IsZero()
    {
        if (_total != 0 || Refund != 0)
            return false;

        ReadOnlySpan<ulong> gas = _gas;
        for (int i = 0; i < NumResourceKinds; i++)
            if (gas[i] != 0)
                return false;

        return true;
    }

    /// <summary>
    /// Returns total minus refund.
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
    /// Returns the combined storage-access cost — sum of <see cref="ResourceKind.StorageAccessRead"/>
    /// and <see cref="ResourceKind.StorageAccessWrite"/>. The tracers preserve the pre-split
    /// <c>StateAccess</c> aggregate by calling this on the per-step delta.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ulong StorageAccessTotal()
    {
        ReadOnlySpan<ulong> gas = _gas;
        return gas[(int)ResourceKind.StorageAccessRead].SaturateAdd(gas[(int)ResourceKind.StorageAccessWrite]);
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

    /// <summary>
    /// Throws ArgumentOutOfRangeException if the resource kind is invalid.
    /// </summary>
    public static void CheckResourceKind(ResourceKind kind)
    {
        if ((uint)kind >= NumResourceKinds)
            ThrowArgumentOutOfRange(kind);
    }

    /// <summary>
    /// Converts MultiGas to a JSON-friendly object for RPC responses.
    /// </summary>
    public readonly MultiGasForJson ToJson() => new(in this);

    /// <summary>
    /// Returns a debug string representation of MultiGas.
    /// </summary>
    public override readonly string ToString()
    {
        ReadOnlySpan<ulong> gas = _gas;
        return $"[total={_total} refund={_refund} gas=[{gas[0]},{gas[1]},{gas[2]},{gas[3]},{gas[4]},{gas[5]},{gas[6]},{gas[7]},{gas[8]}]]";
    }

    /// <summary>
    /// Encodes MultiGas as: [ total, refund, gas[0], gas[1], ..., gas[NumResourceKinds-1] ]
    /// </summary>
    public readonly void Encode(RlpStream stream)
    {
        int contentLength = GetRlpContentLength();
        stream.StartSequence(contentLength);

        stream.Encode(_total);
        stream.Encode(Refund);
        for (int i = 0; i < NumResourceKinds; i++)
            stream.Encode(_gas[i]);
    }

    /// <summary>
    /// Decodes MultiGas positionally: each encoded per-kind entry is written into the
    /// gas slot at its own index, missing trailing entries default to zero, and any
    /// entries beyond <see cref="NumResourceKinds"/> are skipped. Compatibility is
    /// limited to *length* variation — the decoder does NOT semantically remap legacy
    /// ResourceKind orderings. If the enum layout ever changes such that historical
    /// payloads must keep their semantic tagging, callers are responsible for migration.
    /// </summary>
    public static MultiGas Decode(ref Rlp.ValueDecoderContext context)
    {
        int lastCheck = context.ReadSequenceLength() + context.Position;

        MultiGas result = default;

        ulong total = context.DecodeULong();
        ulong refund = context.DecodeULong();

        Span<ulong> gasSpan = result._gas;
        int i = 0;
        while (context.Position < lastCheck)
        {
            ulong val = context.DecodeULong();
            if (i < NumResourceKinds)
                gasSpan[i] = val;
            i++;
        }

        result._total = total;
        result._refund = refund;
        return result;
    }

    /// <summary>
    /// Gets the full RLP length including sequence prefix.
    /// </summary>
    public readonly int GetRlpLength() => Rlp.LengthOfSequence(GetRlpContentLength());

    private readonly int GetRlpContentLength()
    {
        // total + refund + NumResourceKinds gas values
        int length = Rlp.LengthOf(_total);
        length += Rlp.LengthOf(Refund);
        for (int i = 0; i < NumResourceKinds; i++)
            length += Rlp.LengthOf(_gas[i]);
        return length;
    }
}

/// <summary>
/// JSON-serializable representation of MultiGas for RPC responses.
/// Field names mirror Nitro <c>multiGasJSON</c> (/v3.10.0/go-ethereum/arbitrum/multigas/resources.go).
/// </summary>
public readonly struct MultiGasForJson(in MultiGas mg)
{
    [JsonPropertyName("unknown")]
    public ulong Unknown { get; } = mg.Get(ResourceKind.Unknown);

    [JsonPropertyName("computation")]
    public ulong Computation { get; } = mg.Get(ResourceKind.Computation);

    [JsonPropertyName("historyGrowth")]
    public ulong HistoryGrowth { get; } = mg.Get(ResourceKind.HistoryGrowth);

    [JsonPropertyName("storageAccessRead")]
    public ulong StorageAccessRead { get; } = mg.Get(ResourceKind.StorageAccessRead);

    [JsonPropertyName("storageAccessWrite")]
    public ulong StorageAccessWrite { get; } = mg.Get(ResourceKind.StorageAccessWrite);

    [JsonPropertyName("storageGrowth")]
    public ulong StorageGrowth { get; } = mg.Get(ResourceKind.StorageGrowth);

    [JsonPropertyName("singleDim")]
    public ulong SingleDim { get; } = mg.Get(ResourceKind.SingleDim);

    [JsonPropertyName("l2Calldata")]
    public ulong L2Calldata { get; } = mg.Get(ResourceKind.L2Calldata);

    [JsonPropertyName("wasmComputation")]
    public ulong WasmComputation { get; } = mg.Get(ResourceKind.WasmComputation);

    [JsonPropertyName("refund")]
    public ulong Refund { get; } = mg.Refund;

    [JsonPropertyName("total")]
    public ulong Total { get; } = mg.Total;
}
