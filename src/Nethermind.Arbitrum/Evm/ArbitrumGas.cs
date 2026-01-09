// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.CompilerServices;
using Nethermind.Evm.Gas;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Arbitrum gas data type with embedded EthereumGas and MultiGas breakdown.
/// This struct holds the gas state (values) while ArbitrumAccountingPolicy contains the logic.
/// </summary>
public struct ArbitrumGas : IGas<ArbitrumGas>
{
    internal EthereumGas Ethereum;
    internal MultiGas Accumulated;
    internal MultiGas Retained;
    internal ulong InitialGas;

    /// <summary>
    /// Returns a readonly copy of the accumulated multi-gas breakdown.
    /// </summary>
    public readonly MultiGas GetAccumulated() => Accumulated;

    /// <summary>
    /// Returns net accumulated gas (accumulated - retained).
    /// </summary>
    public readonly MultiGas GetTotalAccumulated()
    {
        (MultiGas result, bool underflow) = Accumulated.SafeSub(Retained);
        return underflow ? Accumulated.SaturatingSub(Retained) : result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToLong(in ArbitrumGas gas) => EthereumGas.ToLong(in gas.Ethereum);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGas FromLong(long value) => new()
    {
        Ethereum = EthereumGas.FromLong(value),
        InitialGas = (ulong)value
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOutOfGas(in ArbitrumGas gas) => EthereumGas.IsOutOfGas(in gas.Ethereum);

    public static ArbitrumGas Zero
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new() { Ethereum = EthereumGas.Zero };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGas Max(in ArbitrumGas a, in ArbitrumGas b)
        => EthereumGas.ToLong(in a.Ethereum) >= EthereumGas.ToLong(in b.Ethereum) ? a : b;

    /// <summary>
    /// Returns the single-dimensional gas value (total - refund) from the accumulated MultiGas.
    /// This matches Nitro's SingleGas() method for protocol boundaries.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong SingleGas(in ArbitrumGas gas) => gas.GetTotalAccumulated().SingleGas();

    /// <summary>
    /// Multiplies gas by a UInt256 value. Used for fee calculations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt256 MultiplyByUInt256(in ArbitrumGas gas, in UInt256 multiplier)
        => multiplier * SingleGas(in gas);

    /// <summary>
    /// Returns the accumulated MultiGas as a JSON-serializable object with hex encoding.
    /// </summary>
    public static object ToJsonSerializable(in ArbitrumGas gas)
        => gas.GetTotalAccumulated().ToJsonHex();

    /// <summary>
    /// Creates a new ArbitrumGas with specified available gas while preserving
    /// an existing MultiGas breakdown. Used by GasChargingHook to preserve intrinsic
    /// gas breakdown when creating available gas for EVM execution.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArbitrumGas FromLongWithAccumulated(long value, in MultiGas accumulated) => new()
    {
        Ethereum = EthereumGas.FromLong(value),
        InitialGas = (ulong)value,
        Accumulated = accumulated
    };
}
