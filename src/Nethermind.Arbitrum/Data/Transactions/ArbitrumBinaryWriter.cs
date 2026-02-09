// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Data.Transactions;

public static class ArbitrumBinaryWriter
{
    public static void WriteUInt24BigEndian(Span<byte> destination, uint value)
    {
        if (destination.Length < 3)
            throw new ArgumentException("Destination span is too small to write a 24-bit unsigned integer.");

        if (value > Math.Utils.MaxUint24)
            throw new ArgumentOutOfRangeException(nameof(value), $"Value {value} exceeds the maximum for a 24-bit unsigned integer.");

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

    public static void WriteByteString(BinaryWriter writer, byte[] data)
    {
        Span<byte> lengthBytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(lengthBytes, (ulong)data.Length);
        writer.Write(lengthBytes);
        writer.Write(data);
    }
}
