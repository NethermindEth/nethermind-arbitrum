// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public enum ApiStatus : byte
{
    Success = 0,
    Failure = 1,
    OutOfGas = 2,
    WriteProtection = 3
}

public record CapturedHostIo(ulong StartInk, ulong EndInk, string Name, byte[] Args, byte[] Outs);

public class TestStylusEvmApi : IStylusEvmApi
{
    private readonly Dictionary<byte[], byte[]> _storage = new(Bytes.EqualityComparer);
    private readonly List<GCHandle> _handles = [];
    private readonly List<CapturedHostIo> _traces = new();

    public IReadOnlyList<CapturedHostIo> Traces => _traces.AsReadOnly();

    public StylusEvmResponse Handle(StylusEvmRequestType requestType, ReadOnlyMemory<byte> input)
    {
        ReadOnlySpan<byte> inputSpan = input.Span;
        switch (requestType)
        {
            case StylusEvmRequestType.GetBytes32:
                byte[] key1 = inputSpan.ToArray();
                return new(_storage.TryGetValue(key1, out byte[]? r) ? r : new byte[32], [], 2100UL);
            case StylusEvmRequestType.SetTrieSlots:
                byte[] key2 = inputSpan[8..40].ToArray();
                byte[] value = inputSpan[40..].ToArray();
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
                return new([], [], 0UL);
            case StylusEvmRequestType.AccountBalance:
                break;
            case StylusEvmRequestType.AccountCode:
                break;
            case StylusEvmRequestType.AccountCodeHash:
                break;
            case StylusEvmRequestType.AddPages:
                break;
            case StylusEvmRequestType.CaptureHostIo:
                // Layout is based on arbos/programs/api.go:
                // startInc: 8 bytes
                // endInk: 8 bytes
                // nameLen: 4 bytes
                // argsLen: 4 bytes
                // outsLen: 4 bytes
                // name: UTF-8 string, based on nameLen
                // args: byte[], based on argsLen
                // outs: byte[], based on outsLen

                ulong startInk = BinaryPrimitives.ReadUInt64BigEndian(inputSpan[..8]);
                ulong endInk = BinaryPrimitives.ReadUInt64BigEndian(inputSpan[8..16]);
                uint nameLen = BinaryPrimitives.ReadUInt32BigEndian(inputSpan[16..20]);
                uint argsLen = BinaryPrimitives.ReadUInt32BigEndian(inputSpan[20..24]);
                uint outsLen = BinaryPrimitives.ReadUInt32BigEndian(inputSpan[24..28]);
                int nameTakeTill = (int)nameLen + 28;
                string name = Encoding.UTF8.GetString(inputSpan[28..nameTakeTill]);
                int argsTakeTill = nameTakeTill + (int)argsLen;
                byte[] args = inputSpan[nameTakeTill..argsTakeTill].ToArray();
                int outsTakeTill = argsTakeTill + (int)outsLen;
                byte[] outs = inputSpan[argsTakeTill..outsTakeTill].ToArray();

                _traces.Add(new(startInk, endInk, name, args, outs));

                return new([], [], 0UL);
            default:
                throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null);
        }

        return new([(byte)ApiStatus.Success], [], 0UL);
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
}
