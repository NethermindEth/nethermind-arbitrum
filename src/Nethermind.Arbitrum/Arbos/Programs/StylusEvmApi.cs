// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Programs;

public class StylusEvmApi(IStylusVmHost vmHostBridge, Address actingAddress, StylusMemoryModel memoryModel, TracingInfo? tracingInfo = null) : IStylusEvmApi
{
    private const int AddressSize = 20;
    private const int Hash256Size = 32;
    private const int UInt256Size = 32;
    private const int UInt64Size = 8;
    private const int UInt32Size = 4;
    private const int UInt16Size = 2;

    private readonly List<GCHandle> _handles = [];

    public StylusEvmResponse Handle(StylusEvmRequestType requestType, byte[] input)
    {
        if (Out.IsTargetBlock)
            Out.Log($"stylus api request={(int)requestType} input={input.ToHexString()} actingAddress={actingAddress}");

        try
        {
            StylusEvmResponse response = requestType switch
            {
                // Storage operations
                StylusEvmRequestType.GetBytes32 => HandleGetBytes32(input),
                StylusEvmRequestType.SetTrieSlots => HandleSetTrieSlots(input),
                StylusEvmRequestType.GetTransientBytes32 => HandleGetTransientBytes32(input),
                StylusEvmRequestType.SetTransientBytes32 => HandleSetTransientBytes32(input),

                // Account operations
                StylusEvmRequestType.AccountBalance => HandleAccountBalance(input),
                StylusEvmRequestType.AccountCode => HandleAccountCode(input),
                StylusEvmRequestType.AccountCodeHash => HandleAccountCodeHash(input),

                // Contract operations
                StylusEvmRequestType.ContractCall or StylusEvmRequestType.DelegateCall or StylusEvmRequestType.StaticCall => HandleCall(requestType, input),
                StylusEvmRequestType.Create1 or StylusEvmRequestType.Create2 => HandleCreate(requestType, input),

                // System operations
                StylusEvmRequestType.EmitLog => HandleEmitLog(input),
                StylusEvmRequestType.AddPages => HandleAddPages(input),
                StylusEvmRequestType.CaptureHostIo => HandleCaptureHostIO(input),

                _ => throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null)
            };

            if (Out.IsTargetBlock)
                Out.Log($"stylus api request={(int)requestType} gasCost={response.GasCost} result={response.Result.ToHexString()} data={response.RawData.ToHexString()}");

            return response;
        } catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
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

    private StylusEvmResponse HandleGetBytes32(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, Hash256Size);
        UInt256 index = GetUInt256(ref inputSpan);
        StorageCell storageCell = new(actingAddress, index);
        ulong gasCost = WasmGas.WasmStateLoadCost(vmHostBridge, storageCell);

        ReadOnlySpan<byte> result = vmHostBridge.WorldState.Get(storageCell);
        return new StylusEvmResponse(PadTo32Bytes(result), [], gasCost);
    }

    private StylusEvmResponse HandleSetTrieSlots(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, UInt64Size + Hash256Size + Hash256Size);
        ulong gas = GetUlong(ref inputSpan);
        ulong gasLeft = gas;
        bool isOutOfGas = false;

        if (Out.IsTargetBlock)
            Out.Log($"stylus api setTrieSlots gasLeft={gas}");

        while (inputSpan.Length > 0)
        {
            UInt256 index = GetUInt256(ref inputSpan);
            ReadOnlySpan<byte> value = Get32Bytes(ref inputSpan).WithoutLeadingZeros();
            StorageCell cell = new(actingAddress, index);
            ulong gasCost = WasmGas.WasmStateStoreCost(vmHostBridge, cell, value);

            if (gasCost > gasLeft)
            {
                gasLeft = 0;
                isOutOfGas = true;
                break;
            }
            gasLeft -= gasCost;

            if (Out.IsTargetBlock)
                Out.Log($"stylus api setTrieSlots gasLeft={gasLeft} gasCost={gasCost}");

            vmHostBridge.WorldState.Set(cell, value.ToArray());
        }

        StylusApiStatus status = isOutOfGas ? StylusApiStatus.OutOfGas : StylusApiStatus.Success;
        return new StylusEvmResponse([(byte)status], [], gas - gasLeft);
    }

    private StylusEvmResponse HandleGetTransientBytes32(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, Hash256Size);
        UInt256 index = GetUInt256(ref inputSpan);

        ReadOnlySpan<byte> result = vmHostBridge.WorldState.GetTransientState(new StorageCell(actingAddress, index));
        return new StylusEvmResponse(PadTo32Bytes(result), [], 0);
    }

    private StylusEvmResponse HandleSetTransientBytes32(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, Hash256Size + Hash256Size);
        UInt256 index = GetUInt256(ref inputSpan);
        ReadOnlySpan<byte> value = Get32Bytes(ref inputSpan);

        vmHostBridge.WorldState.SetTransientState(
            new StorageCell(actingAddress, index),
            value.ToArray());
        return new StylusEvmResponse([(byte)StylusApiStatus.Success], [], 0);
    }

    private StylusEvmResponse HandleAccountBalance(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, AddressSize);
        Address address = GetAddress(ref inputSpan);

        ulong gasCost = WasmGas.WasmAccountTouchCost(vmHostBridge, address, false);
        byte[] balance = vmHostBridge.WorldState.GetBalance(address).ToBigEndian();
        return new StylusEvmResponse(balance, [], gasCost);
    }

    private StylusEvmResponse HandleAccountCode(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, AddressSize + UInt64Size);
        Address address = GetAddress(ref inputSpan);
        ulong gasLeft = GetUlong(ref inputSpan);
        ulong gasCost = WasmGas.WasmAccountTouchCost(vmHostBridge, address, true);
        if (gasCost > gasLeft)
            return new StylusEvmResponse([], [], gasCost);
        byte[]? code = vmHostBridge.WorldState.GetCode(address);
        return new StylusEvmResponse([], code ?? [], gasCost);
    }

    private StylusEvmResponse HandleAccountCodeHash(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, AddressSize);
        Address address = GetAddress(ref inputSpan);
        ulong gasCost = WasmGas.WasmAccountTouchCost(vmHostBridge, address, false);
        ValueHash256 codeHash = vmHostBridge.WorldState.GetCodeHash(address);
        return new StylusEvmResponse(codeHash.ToByteArray(), [], gasCost);
    }

    private StylusEvmResponse HandleCall(StylusEvmRequestType requestType, byte[] input)
    {
        ReadOnlyMemory<byte> inputMemory = input;
        ReadOnlySpan<byte> inputSpan = inputMemory.Span;

        int minLength = AddressSize + Hash256Size + UInt64Size + UInt64Size;
        ValidateInputLength(inputSpan, minLength);

        ExecutionType executionType = requestType switch
        {
            StylusEvmRequestType.ContractCall => ExecutionType.CALL,
            StylusEvmRequestType.DelegateCall => ExecutionType.DELEGATECALL,
            StylusEvmRequestType.StaticCall => ExecutionType.STATICCALL,
            _ => throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null)
        };

        Address contractAddress = GetAddress(ref inputSpan);
        UInt256 callValue = GetUInt256(ref inputSpan);
        ulong gasLeftReportedByRust = GetUlong(ref inputSpan);
        ulong gasRequestedByRust = GetUlong(ref inputSpan);
        ReadOnlyMemory<byte> callData = inputMemory[minLength..];

        if (Out.IsTargetBlock)
            Out.Log($"stylus api call gasLeft={gasLeftReportedByRust} gasRequested={gasRequestedByRust} value={callValue} data={callData.Span.ToHexString()}");

        StylusEvmResult result = vmHostBridge.StylusCall(
            executionType,
            contractAddress,
            callData,
            gasLeftReportedByRust,
            gasRequestedByRust,
            callValue);

        byte status = result.EvmException == EvmExceptionType.None
            ? (byte)StylusApiStatus.Success
            : (byte)StylusApiStatus.OutOfGas;

        if (Out.IsTargetBlock)
            Out.Log($"stylus api call result status={status} gas={result.GasCost} data={result.ReturnData.ToHexString()}");

        return new StylusEvmResponse([status], result.ReturnData, result.GasCost);
    }

    private StylusEvmResponse HandleCreate(StylusEvmRequestType requestType, byte[] input)
    {
        ReadOnlyMemory<byte> inputMemory = input;
        ReadOnlySpan<byte> inputSpan = inputMemory.Span;

        int minLength = UInt64Size + Hash256Size;
        if (requestType == StylusEvmRequestType.Create2)
            minLength += Hash256Size;
        ValidateInputLength(inputSpan, minLength);

        ulong gasLimit = GetUlong(ref inputSpan);
        UInt256 endowment = GetUInt256(ref inputSpan);
        UInt256 salt = requestType == StylusEvmRequestType.Create2 ? GetUInt256(ref inputSpan) : UInt256.Zero;
        ReadOnlyMemory<byte> createCode = inputMemory[minLength..];

        StylusEvmResult result = vmHostBridge.StylusCreate(
            createCode,
            endowment,
            salt,
            gasLimit);

        if (result.EvmException != EvmExceptionType.None)
            // TODO: need to add error strings here in the result
            return new StylusEvmResponse([0], [], gasLimit);

        byte[] returnResult = new byte[AddressSize + 1];
        returnResult[0] = 1;
        result.CreatedAddress!.Bytes.CopyTo(returnResult.AsSpan()[1..]);
        return new StylusEvmResponse(returnResult, result.ReturnData, result.GasCost);
    }

    private StylusEvmResponse HandleEmitLog(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, UInt32Size);
        uint topicsNum = GetU32(ref inputSpan);
        ValidateInputLength(inputSpan, (int)(topicsNum * Hash256Size));

        Hash256[] topics = new Hash256[topicsNum];
        for (int i = 0; i < topicsNum; i++)
            topics[i] = new Hash256(Get32Bytes(ref inputSpan));

        ReadOnlySpan<byte> data = GetRest(ref inputSpan);
        LogEntry logEntry = new(actingAddress, data.ToArray(), topics);
        vmHostBridge.VmState.AccessTracker.Logs.Add(logEntry);
        return new StylusEvmResponse([], [], 0);
    }

    private StylusEvmResponse HandleAddPages(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        ValidateInputLength(inputSpan, UInt16Size);
        ushort pages = GetU16(ref inputSpan);
        (ushort openNow, ushort openEver) = vmHostBridge.WasmStore.AddStylusPages(pages);
        ulong gasCost = memoryModel.GetGasCost(pages, openNow, openEver);

        if (Out.IsTargetBlock)
            Out.Log($"stylus api addPages requested={pages} openedNow={openNow} openedEver={openEver} gasCost={gasCost}");

        return new StylusEvmResponse([], [], gasCost);
    }

    private StylusEvmResponse HandleCaptureHostIO(byte[] input)
    {
        ReadOnlySpan<byte> inputSpan = input;
        if (tracingInfo is null && !Out.IsTargetBlock)
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
            tracingInfo?.CaptureEvmTraceForHostio(name, args, outs, startInk, endInk);

            if (Out.IsTargetBlock)
                Out.Log($"stylus api hostIO name={name} startInk={startInk} endInk={endInk} args={args.ToHexString()} outs={outs.ToHexString()}");
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

    private static UInt256 GetUInt256(ref ReadOnlySpan<byte> input)
    {
        ReadOnlySpan<byte> result = input[..UInt256Size];
        input = input[UInt256Size..];
        return new(result, isBigEndian: true);
    }
}
