// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Programs;

public class StylusEvmApi(ArbitrumVirtualMachine vm, Address actingAddress): IStylusEvmApi
{
    private readonly List<GCHandle> _handles = [];
    private readonly IStylusVmHost _vmHostBridge = vm;

    public (byte[] result, byte[] rawData, ulong gasCost) Handle(StylusEvmRequestType requestType, byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;

        switch (requestType)
        {
            case StylusEvmRequestType.GetBytes32:
                // TODO: implement gas cost
                ReadOnlySpan<byte> keyGetBytes32 = Get32Bytes(ref inputSpan);

                ReadOnlySpan<byte> result = vm.WorldState.Get(new StorageCell(actingAddress,
                    new UInt256(keyGetBytes32, isBigEndian: true)));
                if (result.Length == 32)
                    return (result.ToArray(), [], 0);

                byte[] bytes32 = new byte[32];
                result.CopyTo(bytes32.AsSpan()[(32 - result.Length)..]);
                return (bytes32.ToArray(), [], 0);

            case StylusEvmRequestType.SetTrieSlots:
                // TODO: Implement gas cost calculation
                ulong gasLeftSetTrieSlots = GetUlong(ref inputSpan);
                ReadOnlySpan<byte> keySetTrieSlots = Get32Bytes(ref inputSpan);
                ReadOnlySpan<byte> valueSetTrieSlots = Get32Bytes(ref inputSpan);

                vm.WorldState.Set(new StorageCell(actingAddress, new UInt256(keySetTrieSlots, isBigEndian: true)), valueSetTrieSlots.ToArray());
                break;

            case StylusEvmRequestType.GetTransientBytes32:
                ReadOnlySpan<byte> keyGetTransientBytes32 =  Get32Bytes(ref inputSpan);

                ReadOnlySpan<byte> resultGetTransientBytes32 = vm.WorldState.GetTransientState(new StorageCell(actingAddress, new UInt256(keyGetTransientBytes32, isBigEndian: true)));
                if (resultGetTransientBytes32.Length == 32)
                    return (resultGetTransientBytes32.ToArray(), [], 0);

                byte[] retGetTransientBytes32 = new byte[32];
                resultGetTransientBytes32.CopyTo(
                    retGetTransientBytes32.AsSpan()[(32 - resultGetTransientBytes32.Length)..]);
                return (retGetTransientBytes32.ToArray(), [], 0);

            case StylusEvmRequestType.SetTransientBytes32:
                ReadOnlySpan<byte> keySetTransientBytes32 = Get32Bytes(ref inputSpan);
                ReadOnlySpan<byte> valueSetTransientBytes32 = Get32Bytes(ref inputSpan);

                vm.WorldState.SetTransientState(
                    new StorageCell(actingAddress, new UInt256(keySetTransientBytes32, isBigEndian: true)),
                    valueSetTransientBytes32.ToArray());
                break;

            case StylusEvmRequestType.AccountBalance:
                // TODO: implement gas cost
                Address addressAccountBalance = GetAddress(ref inputSpan);

                var balance = vm.WorldState.GetBalance(addressAccountBalance).ToBigEndian();
                return (balance, [], 0);

            case StylusEvmRequestType.AccountCode:
                // TODO: implement gas cost
                Address addressAccountCode = GetAddress(ref inputSpan);
                ulong gasLeftAccountCode = GetUlong(ref inputSpan);

                var code = vm.WorldState.GetCode(addressAccountCode);
                return ([], code ?? [], 0);

            case StylusEvmRequestType.AccountCodeHash:
                // TODO: implement gas cost
                Address addressAccountCodeHash = GetAddress(ref inputSpan);
                ulong gasLeftAccountCodeHash = GetUlong(ref inputSpan);

                ValueHash256 codeHash = vm.WorldState.GetCodeHash(addressAccountCodeHash);
                return (codeHash.ToByteArray(), [], 0);

            case StylusEvmRequestType.ContractCall:
            case StylusEvmRequestType.DelegateCall:
            case StylusEvmRequestType.StaticCall:
                ExecutionType executionType = requestType switch
                {
                    StylusEvmRequestType.ContractCall => ExecutionType.CALL,
                    StylusEvmRequestType.DelegateCall => ExecutionType.DELEGATECALL,
                    StylusEvmRequestType.StaticCall => ExecutionType.STATICCALL,
                    _ => throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null)
                };
                Address contractAddress = GetAddress(ref inputSpan);
                UInt256 callValue = new(Get32Bytes(ref inputSpan));
                ulong gasLeftReportedByRust = GetUlong(ref inputSpan);
                ulong gasRequestedByRust = GetUlong(ref inputSpan);
                ReadOnlySpan<byte> callData= inputSpan;

                (byte[] ret, ulong cost, EvmExceptionType? err) = _vmHostBridge.StylusCall(executionType,
                    contractAddress,
                    callData,
                    gasLeftReportedByRust,
                    gasRequestedByRust,
                    callValue);

                byte status = 0;
                if (err != null) status = 2;

                return ([status], ret, cost);

            case StylusEvmRequestType.Create1:
            case StylusEvmRequestType.Create2:
                ulong gasLimit = GetUlong(ref inputSpan);
                UInt256 endowment = new(Get32Bytes(ref inputSpan));

                UInt256 salt = UInt256.Zero;
                if (requestType == StylusEvmRequestType.Create2) salt = new UInt256(Get32Bytes(ref inputSpan));

                ReadOnlySpan<byte> createCode = inputSpan;
                (Address created, byte[] returnData, ulong costCreate, EvmExceptionType? errCreate) = _vmHostBridge.StylusCreate(
                    createCode,
                    endowment,
                    salt,
                    gasLimit);

                if (errCreate != null)
                    // after zero, the rest is error bytes
                    return ([0], [], gasLimit);

                byte[] resultCreate = new byte[21];
                resultCreate[0] = 1;
                created.Bytes.CopyTo(resultCreate.AsSpan()[1..]);
                return (resultCreate, returnData, costCreate);

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

        Address GetAddress(ref ReadOnlySpan<byte> inp)
        {
            Address ret = new(inp[..20]);
            inp = inp[20..];
            return ret;
        }

        ulong GetUlong(ref ReadOnlySpan<byte> inp)
        {
            ulong ret = BinaryPrimitives.ReadUInt64BigEndian(inp[..8]);
            inp = inp[8..];
            return ret;
        }

        ReadOnlySpan<byte> Get32Bytes(ref ReadOnlySpan<byte> inp)
        {
            ReadOnlySpan<byte> ret = inp[..32];
            inp = inp[32..];
            return ret;
        }
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
