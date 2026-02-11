// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class ArbitrumBinaryTestWriter
{
    public static void WriteUInt256(BinaryWriter writer, UInt256 value)
    {
        Span<byte> bytes = stackalloc byte[32];
        value.ToBigEndian(bytes);
        writer.Write(bytes);
    }

    public static void WriteBigInteger256(BinaryWriter writer, UInt256 value)
    {
        WriteUInt256(writer, value);
    }

    public static void WriteAddress(BinaryWriter writer, Address address)
    {
        writer.Write(address.Bytes);
    }

    public static void WriteAddressFrom256(BinaryWriter writer, Address address)
    {
        Span<byte> bytes = stackalloc byte[32];
        if (address != Address.Zero)
            address.Bytes.CopyTo(bytes[12..]);
        writer.Write(bytes);
    }

    public static void WriteHash256(BinaryWriter writer, Hash256 hash)
    {
        writer.Write(hash.Bytes);
    }

    public static void WriteByteString(BinaryWriter writer, byte[] data)
    {
        WriteULongBigEndian(writer, (ulong)data.Length);
        writer.Write(data);
    }

    public static void WriteULongBigEndian(BinaryWriter writer, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(bytes, value);
        if (BitConverter.IsLittleEndian)
            bytes.Reverse();
        writer.Write(bytes);
    }
}
