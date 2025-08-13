// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Runtime.InteropServices;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Programs;



public class StylusEvmApi(ArbitrumVirtualMachine vm, Address actingAddress): IStylusEvmApi
{
    private readonly List<GCHandle> _handles = [];

    public (byte[] result, byte[] rawData, ulong gasCost) Handle(StylusEvmRequestType requestType, byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;

        switch (requestType)
        {
            case StylusEvmRequestType.GetBytes32:
                // TODO: implement gas cost
                ReadOnlySpan<byte> result = vm.WorldState.Get(new StorageCell(actingAddress, new UInt256(inputSpan[..32], isBigEndian: true)));
                if (result.Length == 32)
                    return (result.ToArray(), [], 0);

                byte[] bytes32 = new byte[32];
                result.CopyTo(bytes32.AsSpan().Slice(32 - result.Length));
                return (bytes32.ToArray(), [], 0);
                break;
            case StylusEvmRequestType.SetTrieSlots:
                // TODO: Implement gas cost calculation

                // The First 8 bytes are for the available gas
                ReadOnlySpan<byte> key = inputSpan[8..40];
                ReadOnlySpan<byte> value = inputSpan[40..];
                vm.WorldState.Set(new StorageCell(actingAddress, new UInt256(key, isBigEndian: true)), value.ToArray());
                break;
            case StylusEvmRequestType.GetTransientBytes32:
                break;
            case StylusEvmRequestType.SetTransientBytes32:
                break;
            case StylusEvmRequestType.AccountBalance:
                break;
            case StylusEvmRequestType.AccountCode:
                break;
            case StylusEvmRequestType.AccountCodeHash:
                break;
            case StylusEvmRequestType.ContractCall:
            case StylusEvmRequestType.DelegateCall:
            case StylusEvmRequestType.StaticCall:
                break;
            case StylusEvmRequestType.Create1:
            case StylusEvmRequestType.Create2:
                break;
            case StylusEvmRequestType.EmitLog:
                return ([], [], 0);
            case StylusEvmRequestType.CaptureHostIo:
                return ([], [], 0);
            case StylusEvmRequestType.AddPages:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null);
        }

        return ([0], [], 0);
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
