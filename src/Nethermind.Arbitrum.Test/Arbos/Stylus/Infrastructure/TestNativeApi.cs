// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Nethermind.Arbitrum.Arbos.Stylus;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public enum ApiStatus : byte
{
    Success = 0,
    Failure = 1,
    OutOfGas = 2,
    WriteProtection = 3
}

public record CapturedHostIo(ulong StartInk, ulong EndInk, string Name, byte[] Args, byte[] Outs);

public class TestNativeApi : IStylusEvmApi
{
    private readonly Dictionary<byte[], byte[]> _storage = new(new BytesEqualityComparer());
    private readonly List<GCHandle> _handles = [];
    private readonly List<CapturedHostIo> _traces = new();

    public IReadOnlyList<CapturedHostIo> Traces => _traces.AsReadOnly();

    public (byte[] result, byte[] rawData, ulong gasCost) Handle(StylusEvmRequestType requestType, byte[] input)
    {
        switch (requestType)
        {
            case StylusEvmRequestType.GetBytes32:
                byte[] key1 = input[..];
                return (_storage.TryGetValue(key1, out byte[]? r) ? r : new byte[32], [], 2100);
            case StylusEvmRequestType.SetTrieSlots:
                byte[] key2 = input[8..40];
                byte[] value = input[40..];
                _storage[key2] = value;
                break;
            case StylusEvmRequestType.GetTransientBytes32:
                break;
            case StylusEvmRequestType.SetTransientBytes32:
                break;
            case StylusEvmRequestType.ContractCall:
                break;
            case StylusEvmRequestType.DelegateCall:
                break;
            case StylusEvmRequestType.StaticCall:
                break;
            case StylusEvmRequestType.Create1:
                break;
            case StylusEvmRequestType.Create2:
                break;
            case StylusEvmRequestType.EmitLog:
                break;
            case StylusEvmRequestType.AccountBalance:
                break;
            case StylusEvmRequestType.AccountCode:
                break;
            case StylusEvmRequestType.AccountCodeHash:
                break;
            case StylusEvmRequestType.AddPages:
                break;
            case StylusEvmRequestType.CaptureHostIo:
                ulong startInk = BinaryPrimitives.ReadUInt64BigEndian(input[..8]);
                ulong endInk = BinaryPrimitives.ReadUInt64BigEndian(input[8..16]);
                uint nameLen = BinaryPrimitives.ReadUInt32BigEndian(input[16..20]);
                uint argsLen = BinaryPrimitives.ReadUInt32BigEndian(input[20..24]);
                uint outsLen = BinaryPrimitives.ReadUInt32BigEndian(input[24..28]);
                int nameTakeTill = (int)nameLen + 28;
                string name = Encoding.UTF8.GetString(input[28..nameTakeTill]);
                int argsTakeTill = nameTakeTill + (int)argsLen;
                byte[] args = input[nameTakeTill..argsTakeTill];
                int outsTakeTill = argsTakeTill + (int)outsLen;
                byte[] outs = input[argsTakeTill..outsTakeTill];

                _traces.Add(new(startInk, endInk, name, args, outs));

                return ([], [], 0);
            default:
                throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null);
        }

        return ([(byte)ApiStatus.Success], [], 0);
    }

    public GoSliceData AllocateGoSlice(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return new GoSliceData { Ptr = IntPtr.Zero, Len = UIntPtr.Zero };
        }

        GCHandle pinnedData = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        _handles.Add(pinnedData);
        return new GoSliceData
        {
            Ptr = pinnedData.AddrOfPinnedObject(),
            Len = (UIntPtr)bytes.Length
        };
    }

    public void Dispose()
    {
        foreach (GCHandle handle in _handles)
        {
            handle.Free();
        }
    }

    private class BytesEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y) =>
            x == y || (x != null && y != null && x.SequenceEqual(y));

        public int GetHashCode(byte[] bytes) =>
            bytes?.Aggregate(17, (current, b) => current * 31 + b) ?? 0;
    }
}
