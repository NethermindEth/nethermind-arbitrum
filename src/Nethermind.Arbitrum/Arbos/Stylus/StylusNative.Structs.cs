// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Nethermind.Arbitrum.Arbos.Stylus;

public enum UserOutcomeKind : byte
{
    Success = 0,
    Revert = 1,
    Failure = 2,
    OutOfInk = 3,
    OutOfStack = 4
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeRequestHandler
{
    public delegate* unmanaged[Cdecl]<nuint, uint, RustBytes*, ulong*, GoSliceData*, GoSliceData*, void> HandleRequestFptr;
    public nuint Id;
}

[StructLayout(LayoutKind.Sequential)]
public struct GoSliceData
{
    public nint Ptr;
    public nuint Len;
}

public class GoSliceHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private GCHandle _gcHandle;
    private readonly int _length;

    private GoSliceHandle() : base(true)
    {
        _length = 0;
    }

    private GoSliceHandle(GCHandle gcHandle, IntPtr ptr, int length) : base(true)
    {
        _gcHandle = gcHandle;
        _length = length;
        SetHandle(ptr);
    }

    public static GoSliceHandle Empty { get; } = new();
    public int Length => _length;
    public GoSliceData Data => new() { Ptr = handle, Len = (nuint)_length };

    public static GoSliceHandle From(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return Empty;

        GCHandle gcHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return new GoSliceHandle(gcHandle, gcHandle.AddrOfPinnedObject(), bytes.Length);
        }
        catch
        {
            gcHandle.Free();
            throw;
        }
    }

    public static GoSliceHandle From(string? value)
    {
        return !string.IsNullOrEmpty(value)
            ? From(Encoding.UTF8.GetBytes(value))
            : Empty;
    }

    public unsafe ReadOnlySpan<byte> AsSpan()
    {
        return IsInvalid || IsClosed
            ? ReadOnlySpan<byte>.Empty
            : new ReadOnlySpan<byte>(handle.ToPointer(), _length);
    }

    public byte[] ToArray()
    {
        return AsSpan().ToArray();
    }

    protected override bool ReleaseHandle()
    {
        if (_gcHandle.IsAllocated)
            _gcHandle.Free();

        SetHandle(IntPtr.Zero);
        return true;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct RustBytes
{
    public nint Ptr;
    public nuint Len;
    public nuint Cap;
}

[StructLayout(LayoutKind.Sequential)]
public struct PricingParams
{
    public uint InkPrice;
}

[StructLayout(LayoutKind.Sequential)]
public struct StylusConfig
{
    public ushort Version;
    public uint MaxDepth;
    public PricingParams Pricing;
}

[StructLayout(LayoutKind.Sequential)]
[InlineArray(32)]
public struct Bytes32
{
    private byte _element0;

    public Bytes32(ReadOnlySpan<byte> data)
    {
        if (data.Length != 32)
            throw new ArgumentException("Data must be 32 bytes long but was " + data.Length);

        data.CopyTo(this);
    }

    public readonly byte[] ToArray() => this[..].ToArray();

    public void SetBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length != 32)
            throw new ArgumentException($"Data must be 32 bytes long but was {data.Length}");

        data.CopyTo(this);
    }
}

[StructLayout(LayoutKind.Sequential)]
[InlineArray(20)]
public struct Bytes20 : IEquatable<Bytes20>
{
    private byte _element0;

    public Bytes20(ReadOnlySpan<byte> data)
    {
        if (data.Length != 20)
            throw new ArgumentException("Data must be 20 bytes long but was " + data.Length);

        data.CopyTo(this);
    }

    public readonly byte[] ToArray() => this[..].ToArray();

    public void SetBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length != 20)
            throw new ArgumentException($"Data must be 20 bytes long but was {data.Length}");

        data.CopyTo(this);
    }

    public bool Equals(Bytes20 other)
    {
        return this[..].SequenceEqual(other[..]);
    }

    public override bool Equals(object? obj)
    {
        return obj is Bytes32 other && Equals(other);
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.AddBytes(this[..]);
        return hash.ToHashCode();
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct EvmData
{
    public ulong ArbosVersion;
    public Bytes32 BlockBaseFee;
    public ulong ChainId;
    public Bytes20 BlockCoinbase;
    public ulong BlockGasLimit;
    public ulong BlockNumber;
    public ulong BlockTimestamp;
    public Bytes20 ContractAddress;
    public Bytes32 ModuleHash;
    public Bytes20 MsgSender;
    public Bytes32 MsgValue;
    public Bytes32 TxGasPrice;
    public Bytes20 TxOrigin;
    public uint Reentrant;
    public uint ReturnDataLen;
    public bool Cached;
    public bool Tracing;
}

[StructLayout(LayoutKind.Sequential)]
public struct StylusData
{
    public ushort InitCost;
    public ushort CachedInitCost;
    public ushort Footprint;
    public uint AsmEstimate;
}
