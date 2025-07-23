// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Data.Transactions;

public class ArbitrumBinaryWriter
{
    public static void WriteUInt24BigEndian(Span<byte> destination, uint value)
    {
        if (destination.Length < 3)
            throw new ArgumentException("Destination span is too small to write a 24-bit unsigned integer.");

        if (value > Math.Utils.MaxUint24)
            throw new ArgumentOutOfRangeException($"Value {value} exceeds the maximum for a 24-bit unsigned integer.");

        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)value;
    }

    public static void WriteBool(Span<byte> destination, bool value)
    {
        if (destination.Length < 1)
            throw new ArgumentException("Destination span is too small to write a boolean value.");

        destination[0] = value ? (byte)1 : (byte)0;
    }
}
