// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

public class TestStylusEvmApiWithState(IWorldState state, Address actingAddress) : IStylusEvmApi
{
    private readonly List<GCHandle> _handles = [];

    public StylusEvmResponse Handle(StylusEvmRequestType requestType, byte[] input)
    {
        switch (requestType)
        {
            case StylusEvmRequestType.GetBytes32:
                // TODO: Implement gas cost calculation
                ReadOnlySpan<byte> result = state.Get(new StorageCell(actingAddress, new UInt256(input, isBigEndian: true)));
                if (result.Length == 32)
                    return new(result.ToArray(), [], 0);

                byte[] bytes32 = new byte[32];
                result.CopyTo(bytes32.AsSpan().Slice(32 - result.Length));
                return new(bytes32, [], 0);

            case StylusEvmRequestType.SetTrieSlots:
                // TODO: Implement gas cost calculation

                // First 8 bytes are for the available gas
                byte[] key = input[8..40];
                byte[] value = input[40..];
                state.Set(new StorageCell(actingAddress, new UInt256(key, isBigEndian: true)), value);
                break;
            case StylusEvmRequestType.EmitLog:
                return new([], [], 0);
            case StylusEvmRequestType.CaptureHostIo:
                return new([], [], 0);
        }

        return new([0], [], 0);
    }

    public GoSliceData AllocateGoSlice(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return new GoSliceData { Ptr = IntPtr.Zero, Len = UIntPtr.Zero };

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
            handle.Free();
    }
}
