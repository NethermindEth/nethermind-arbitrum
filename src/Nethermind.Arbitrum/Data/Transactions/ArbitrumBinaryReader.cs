using System.Buffers.Binary;
using System.Numerics;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Data.Transactions;

public static class ArbitrumBinaryReader
{
    public static bool TryReadByte(ref ReadOnlySpan<byte> span, out byte value)
    {
        if (span.Length < 1)
        {
            value = 0;
            return false;
        }
        value = span[0];
        span = span[1..];
        return true;
    }

    public static bool TryReadBytes(ref ReadOnlySpan<byte> span, int count, out ReadOnlySpan<byte> value)
    {
        if (span.Length < count)
        {
            value = ReadOnlySpan<byte>.Empty;
            return false;
        }
        value = span[..count];
        span = span[count..];
        return true;
    }

    public static bool TryReadULongBigEndian(ref ReadOnlySpan<byte> span, out ulong value)
    {
        if (span.Length < sizeof(ulong))
        {
            value = 0;
            return false;
        }
        value = BinaryPrimitives.ReadUInt64BigEndian(span);
        span = span[sizeof(ulong)..];
        return true;
    }

    public static bool TryReadHash256(ref ReadOnlySpan<byte> span, out Hash256 value)
    {
        if (span.Length < Hash256.Size)
        {
            value = Hash256.Zero;
            return false;
        }
        value = new Hash256(span[..Hash256.Size]);
        span = span[Hash256.Size..];
        return true;
    }

    public static bool TryReadAddress(ref ReadOnlySpan<byte> span, out Address value)
    {
        if (span.Length < Address.Size)
        {
            value = Address.Zero;
            return false;
        }
        value = new Address(span[..Address.Size]);
        span = span[Address.Size..];
        return true;
    }

    // Reads 32 bytes, takes the last 20
    public static bool TryReadAddressFrom256(ref ReadOnlySpan<byte> span, out Address value)
    {
        if (span.Length < Hash256.Size)
        {
            value = Address.Zero;
            return false;
        }

        value = new Address(span[(Hash256.Size - Address.Size)..Hash256.Size]);
        span = span[Hash256.Size..];
        return true;
    }

    public static bool TryReadBigInteger256(ref ReadOnlySpan<byte> span, out BigInteger value)
    {
        if (span.Length < Hash256.Size)
        {
            value = default;
            return false;
        }

        value = new BigInteger(span[..Hash256.Size], isUnsigned: true, isBigEndian: true);
        span = span[Hash256.Size..];
        return true;
    }

    public static bool TryReadUInt256(ref ReadOnlySpan<byte> span, out UInt256 value)
    {
        if (span.Length < Hash256.Size)
        {
            value = default;
            return false;
        }

        value = new UInt256(span[..Hash256.Size], isBigEndian: true);
        span = span[Hash256.Size..];
        return true;
    }

    public static bool TryReadUInt32BigEndian(ref ReadOnlySpan<byte> span, out uint value)
    {
        if (span.Length < 4)
        {
            value = default;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32BigEndian(span);
        span = span[4..];
        return true;
    }

    public static bool TryReadUIntFrom24BigEndian(ref ReadOnlySpan<byte> span, out uint value)
    {
        if (span.Length < 3)
        {
            value = default;
            return false;
        }

        value = (uint)((span[0] << 16) | (span[1] << 8) | span[2]);
        span = span[3..];
        return true;
    }

    public static bool TryReadUShortBigEndian(ref ReadOnlySpan<byte> span, out ushort value)
    {
        if (span.Length < 2)
        {
            value = default;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(span);
        span = span[2..];
        return true;
    }

    // Reads a uint64 length prefix, then the bytes.
    public static bool TryReadByteString(ref ReadOnlySpan<byte> span, ulong maxLen, out ReadOnlyMemory<byte> value)
    {
        value = ReadOnlyMemory<byte>.Empty;
        if (!TryReadULongBigEndian(ref span, out ulong length))
        {
            return false;
        }

        if (length > maxLen)
        {
            // Or throw, depending on desired behavior for invalid length
            return false;
        }

        if (length > int.MaxValue)
        {
            // Cannot create a span/memory longer than int.MaxValue
            return false;
        }
        int lenInt = (int)length;

        if (!TryReadBytes(ref span, lenInt, out ReadOnlySpan<byte> bytesSpan))
        {
            return false;
        }

        value = new ReadOnlyMemory<byte>(bytesSpan.ToArray());
        return true;
    }

    public static byte ReadByteOrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadByte(ref span, out byte val) ? val : throw new EndOfStreamException();
    }

    public static bool ReadBoolOrFail(ref ReadOnlySpan<byte> data)
    {
        return ReadByteOrFail(ref data) == 1;
    }

    public static ReadOnlySpan<byte> ReadBytesOrFail(ref ReadOnlySpan<byte> span, int count)
    {
        return TryReadBytes(ref span, count, out var val) ? val : throw new EndOfStreamException();
    }

    public static ulong ReadULongOrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadULongBigEndian(ref span, out ulong val) ? val : throw new EndOfStreamException();
    }

    public static Hash256 ReadHash256OrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadHash256(ref span, out Hash256 val) ? val : throw new EndOfStreamException();
    }

    public static Address ReadAddressOrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadAddress(ref span, out Address val) ? val : throw new EndOfStreamException();
    }

    public static Address ReadAddressFrom256OrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadAddressFrom256(ref span, out Address val) ? val : throw new EndOfStreamException();
    }

    public static BigInteger ReadBigInteger256OrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadBigInteger256(ref span, out BigInteger val) ? val : throw new EndOfStreamException();
    }

    public static UInt256 ReadUInt256OrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadUInt256(ref span, out UInt256 val) ? val : throw new EndOfStreamException();
    }

    public static uint ReadUInt32OrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadUInt32BigEndian(ref span, out uint val) ? val : throw new EndOfStreamException();
    }

    public static uint ReadUIntFrom24OrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadUIntFrom24BigEndian(ref span, out uint val) ? val : throw new EndOfStreamException();
    }

    public static ushort ReadUShortOrFail(ref ReadOnlySpan<byte> span)
    {
        return TryReadUShortBigEndian(ref span, out ushort val) ? val : throw new EndOfStreamException();
    }

    public static ReadOnlyMemory<byte> ReadByteStringOrFail(ref ReadOnlySpan<byte> span, ulong maxLen)
    {
        return TryReadByteString(ref span, maxLen, out var val) ? val : throw new EndOfStreamException();
    }
}
