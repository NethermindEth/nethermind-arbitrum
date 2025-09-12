// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Programs;

public class StylusEvmApi(ArbitrumVirtualMachine vmHostBridge, Address actingAddress, StylusMemoryModel memoryModel, TracingInfo? tracingInfo = null): IStylusEvmApi
{
    private const int AddressSize = 20;
    private const int Hash256Size = 32;
    private const int UInt64Size = 8;
    private const int UInt32Size = 4;
    private const int UInt16Size = 2;

    private readonly List<GCHandle> _handles = [];

    public StylusEvmResponse Handle(StylusEvmRequestType requestType, byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;

        return requestType switch
        {
            // Storage operations
            StylusEvmRequestType.GetBytes32 => HandleGetBytes32(ref inputSpan),
            StylusEvmRequestType.SetTrieSlots => HandleSetTrieSlots(ref inputSpan),
            StylusEvmRequestType.GetTransientBytes32 => HandleGetTransientBytes32(ref inputSpan),
            StylusEvmRequestType.SetTransientBytes32 => HandleSetTransientBytes32(ref inputSpan),

            // Account operations
            StylusEvmRequestType.AccountBalance => HandleAccountBalance(ref inputSpan),
            StylusEvmRequestType.AccountCode => HandleAccountCode(ref inputSpan),
            StylusEvmRequestType.AccountCodeHash => HandleAccountCodeHash(ref inputSpan),

            // Contract operations
            StylusEvmRequestType.ContractCall or StylusEvmRequestType.DelegateCall or StylusEvmRequestType.StaticCall => HandleCall(requestType, ref inputSpan),
            StylusEvmRequestType.Create1 or StylusEvmRequestType.Create2 => HandleCreate(requestType, ref inputSpan),

            // System operations
            StylusEvmRequestType.EmitLog => HandleEmitLog(ref inputSpan),
            StylusEvmRequestType.AddPages => HandleAddPages(ref inputSpan),
            StylusEvmRequestType.CaptureHostIo => HandleCaptureHostIO(ref inputSpan),

            _ => throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null)
        };
    }

    private StylusEvmResponse HandleGetBytes32(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, Hash256Size);
        ReadOnlySpan<byte> key = Get32Bytes(ref inputSpan);
        StorageCell storageCell = new(actingAddress, new UInt256(key, isBigEndian: true));
        ulong gasCost = WasmGas.WasmStateLoadCost(vmHostBridge, storageCell);

        ReadOnlySpan<byte> result = vmHostBridge.WorldState.Get(storageCell);
        return new StylusEvmResponse(PadTo32Bytes(result), [], gasCost);
    }

    private StylusEvmResponse HandleSetTrieSlots(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, UInt64Size + Hash256Size + Hash256Size);
        ulong gas = GetUlong(ref inputSpan);
        ulong gasLeft = gas;
        bool isOutOfGas = false;
        while (inputSpan.Length > 0)
        {
            ReadOnlySpan<byte> key = Get32Bytes(ref inputSpan);
            ReadOnlySpan<byte> value = Get32Bytes(ref inputSpan);
            StorageCell cell = new(actingAddress, new UInt256(key, isBigEndian: true));
            var gasCost = WasmGas.WasmStateStoreCost(vmHostBridge, cell, value);

            if (gasCost > gasLeft)
            {
                gasLeft = 0;
                isOutOfGas = true;
                break;
            }
            gasLeft -= gasCost;
            vmHostBridge.WorldState.Set(cell, value.ToArray());
        }

        StylusApiStatus status = isOutOfGas ? StylusApiStatus.OutOfGas : StylusApiStatus.Success;
        return new StylusEvmResponse([(byte)status], [], gas - gasLeft);
    }

    private StylusEvmResponse HandleGetTransientBytes32(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, Hash256Size);
        ReadOnlySpan<byte> key = Get32Bytes(ref inputSpan);

        ReadOnlySpan<byte> result = vmHostBridge.WorldState.GetTransientState(new StorageCell(actingAddress, new UInt256(key, isBigEndian: true)));
        return new StylusEvmResponse(PadTo32Bytes(result), [], 0);
    }

    private StylusEvmResponse HandleSetTransientBytes32(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, Hash256Size + Hash256Size);
        ReadOnlySpan<byte> key = Get32Bytes(ref inputSpan);
        ReadOnlySpan<byte> value = Get32Bytes(ref inputSpan);

        vmHostBridge.WorldState.SetTransientState(
            new StorageCell(actingAddress, new UInt256(key, isBigEndian: true)),
            value.ToArray());
        return new StylusEvmResponse([(byte)StylusApiStatus.Success], [], 0);
    }

    private StylusEvmResponse HandleAccountBalance(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, AddressSize);
        Address address = GetAddress(ref inputSpan);

        var gasCost = WasmGas.WasmAccountTouchCost(vmHostBridge, address, false);
        var balance = vmHostBridge.WorldState.GetBalance(address).ToBigEndian();
        return new StylusEvmResponse(balance, [], gasCost);
    }

    private StylusEvmResponse HandleAccountCode(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, AddressSize + UInt64Size);
        Address address = GetAddress(ref inputSpan);
        ulong gasLeft = GetUlong(ref inputSpan);
        var gasCost = WasmGas.WasmAccountTouchCost(vmHostBridge, address, true);
        if (gasCost > gasLeft) return new StylusEvmResponse([], [], gasCost);
        var code = vmHostBridge.WorldState.GetCode(address);
        return new StylusEvmResponse([], code ?? [], gasCost);
    }

    private StylusEvmResponse HandleAccountCodeHash(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, AddressSize + UInt64Size);
        Address address = GetAddress(ref inputSpan);
        var gasCost = WasmGas.WasmAccountTouchCost(vmHostBridge, address, true);
        ValueHash256 codeHash = vmHostBridge.WorldState.GetCodeHash(address);
        return new StylusEvmResponse(codeHash.ToByteArray(), [], gasCost);
    }

    private StylusEvmResponse HandleCall(StylusEvmRequestType requestType, ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, AddressSize + Hash256Size + UInt64Size + UInt64Size);

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
        ReadOnlySpan<byte> callData = inputSpan;

        (byte[] ret, ulong cost, EvmExceptionType? err) = vmHostBridge.StylusCall(
            executionType,
            contractAddress,
            callData,
            gasLeftReportedByRust,
            gasRequestedByRust,
            callValue);

        byte status = err != EvmExceptionType.None ? (byte)2 : (byte)0;
        return new StylusEvmResponse([status], ret, cost);
    }

    private StylusEvmResponse HandleCreate(StylusEvmRequestType requestType, ref ReadOnlySpan<byte> inputSpan)
    {
        int minLength = UInt64Size + Hash256Size;
        if (requestType == StylusEvmRequestType.Create2)
            minLength += Hash256Size;
        ValidateInputLength(inputSpan, minLength);

        ulong gasLimit = GetUlong(ref inputSpan);
        UInt256 endowment = new(Get32Bytes(ref inputSpan));
        UInt256 salt = requestType == StylusEvmRequestType.Create2 ? new UInt256(Get32Bytes(ref inputSpan)) : UInt256.Zero;
        ReadOnlySpan<byte> createCode = inputSpan;

        (Address created, byte[] returnData, ulong costCreate, EvmExceptionType? errCreate) = vmHostBridge.StylusCreate(
            createCode,
            endowment,
            salt,
            gasLimit);

        if (errCreate != null)
            return new StylusEvmResponse([0], [], gasLimit);

        byte[] result = new byte[AddressSize + 1];
        result[0] = 1;
        created.Bytes.CopyTo(result.AsSpan()[1..]);
        return new StylusEvmResponse(result, returnData, costCreate);
    }

    private StylusEvmResponse HandleEmitLog(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, UInt32Size);
        uint topicsNum = GetU32(ref inputSpan);
        ValidateInputLength(inputSpan, (int)(topicsNum * Hash256Size));

        Hash256[] topics = new Hash256[topicsNum];
        for (int i = 0; i < topicsNum; i++)
            topics[i] = new Hash256(Get32Bytes(ref inputSpan));

        ReadOnlySpan<byte> data = GetRest(ref inputSpan);
        LogEntry logEntry = new(vmHostBridge.EvmState.Env.ExecutingAccount, data.ToArray(), topics);
        vmHostBridge.EvmState.AccessTracker.Logs.Add(logEntry);
        return new StylusEvmResponse([], [], 0);
    }

    private StylusEvmResponse HandleAddPages(ref ReadOnlySpan<byte> inputSpan)
    {
        ValidateInputLength(inputSpan, UInt16Size);
        ushort pages = GetU16(ref inputSpan);
        (ushort openNow, ushort openEver)  = WasmStore.Instance.AddStylusPages(pages);
        var gasCost = memoryModel.GetGasCost(pages, openNow, openEver);
        return new StylusEvmResponse([], [], gasCost);
    }

    private StylusEvmResponse HandleCaptureHostIO(ref ReadOnlySpan<byte> inputSpan)
    {
        if (tracingInfo is null)
        {
            GetRest(ref inputSpan);
        }
        else
        {
            ulong startInk = GetUlong(ref inputSpan);
            ulong endInk = GetUlong(ref inputSpan);
            uint nameLen = GetU32(ref inputSpan);
            uint argsLen = GetU32(ref inputSpan);
            uint outsLen = GetU32(ref inputSpan);
            string name = System.Text.Encoding.UTF8.GetString(GetFixed(ref inputSpan, (int)nameLen));
            ReadOnlySpan<byte> args = GetFixed(ref inputSpan, (int)argsLen);
            ReadOnlySpan<byte> outs = GetFixed(ref inputSpan, (int)outsLen);
            tracingInfo.CaptureEvmTraceForHostio(name, args, outs, startInk, endInk);
        }
        return new StylusEvmResponse([], [], 0);

    }

    private static void ValidateInputLength(ReadOnlySpan<byte> input, int requiredLength)
    {
        if (input.Length < requiredLength)
            throw new ArgumentException($"Input too short. Expected at least {requiredLength} bytes, got {input.Length}");
    }

    private static byte[] PadTo32Bytes(ReadOnlySpan<byte> input)
    {
        if (input.Length == Hash256Size)
            return input.ToArray();

        byte[] padded = new byte[Hash256Size];
        input.CopyTo(padded.AsSpan()[(Hash256Size - input.Length)..]);
        return padded;
    }

    private static Address GetAddress(ref ReadOnlySpan<byte> input)
    {
        Address result = new(input[..AddressSize]);
        input = input[AddressSize..];
        return result;
    }

    private static ulong GetUlong(ref ReadOnlySpan<byte> input)
    {
        ulong result = BinaryPrimitives.ReadUInt64BigEndian(input[..UInt64Size]);
        input = input[UInt64Size..];
        return result;
    }

    private static uint GetU32(ref ReadOnlySpan<byte> input)
    {
        uint result = BinaryPrimitives.ReadUInt32BigEndian(input[..UInt32Size]);
        input = input[UInt32Size..];
        return result;
    }

    private static ushort GetU16(ref ReadOnlySpan<byte> input)
    {
        ushort result = BinaryPrimitives.ReadUInt16BigEndian(input[..UInt16Size]);
        input = input[UInt16Size..];
        return result;
    }

    private static ReadOnlySpan<byte> Get32Bytes(ref ReadOnlySpan<byte> input)
    {
        ReadOnlySpan<byte> result = input[..Hash256Size];
        input = input[Hash256Size..];
        return result;
    }

    private static ReadOnlySpan<byte> GetRest(ref ReadOnlySpan<byte> input)
    {
        ReadOnlySpan<byte> result = input;
        input = [];
        return result;
    }

    private static ReadOnlySpan<byte> GetFixed(ref ReadOnlySpan<byte> input, int needed)
    {
        ReadOnlySpan<byte> result = input[..needed];
        input = input[needed..];
        return result;
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
