// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nethermind.Arbitrum.Arbos.Stylus;

public enum StylusEvmRequestType : int
{
    GetBytes32 = 0,
    SetTrieSlots = 1,
    GetTransientBytes32 = 2,
    SetTransientBytes32 = 3,
    ContractCall = 4,
    DelegateCall = 5,
    StaticCall = 6,
    Create1 = 7,
    Create2 = 8,
    EmitLog = 9,
    AccountBalance = 10,
    AccountCode = 11,
    AccountCodeHash = 12,
    AddPages = 13,
    CaptureHostIo = 14,
}

public enum StylusApiStatus : byte
{
    Success = 0,
    Failure = 1,
    OutOfGas = 2,
    WriteProtection = 3
}

public record StylusEnvApiRegistration(nuint Id) : IDisposable
{
    public void Dispose()
    {
        StylusEvmApiRegistry.Unregister(Id);
    }
}

public static class StylusEvmApiRegistry
{
    private const int EvmApiMethodReqOffset = 0x10000000;
    private static readonly ConcurrentDictionary<nuint, IStylusEvmApi> Handlers = new();
    private static uint _nextId;

    public static StylusEnvApiRegistration Register(IStylusEvmApi api)
    {
        nuint id = Interlocked.Increment(ref _nextId);
        Handlers[id] = api;
        return new StylusEnvApiRegistration(id);
    }

    public static void Unregister(nuint id) => Handlers.TryRemove(id, out _);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void HandleStylusEnvApiRequest(
        nuint apiId,
        uint reqType,
        RustBytes* data,
        ulong* outCost,
        GoSliceData* outResult,
        GoSliceData* outRawData)
    {
        if (!Handlers.TryGetValue(apiId, out IStylusEvmApi? api))
        {
            // We really shouldn't throw here, but it's better to fail fast than to silently ignore the request.
            throw new InvalidOperationException($"No Stylus EVM API registered with the specified ID {apiId}.");
        }

        byte[]? buffer = null;
        try
        {
            ReadOnlyMemory<byte> input = ReadOnlyMemory<byte>.Empty;
            if (data != null && data->Ptr != IntPtr.Zero && data->Len != UIntPtr.Zero)
            {
                int length = (int)data->Len;
                buffer = ArrayPool<byte>.Shared.Rent(length);
                Marshal.Copy(data->Ptr, buffer, 0, length);
                input = buffer.AsMemory(0, length);
            }

            StylusEvmRequestType request = (StylusEvmRequestType)(reqType - EvmApiMethodReqOffset);
            (byte[] result, byte[] rawData, ulong gasCost) = api.Handle(request, input);

            *outResult = api.AllocateGoSlice(result);
            *outRawData = api.AllocateGoSlice(rawData);
            *outCost = gasCost;
        }
        finally
        {
            if (buffer is not null)
                ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}

public interface IStylusEvmApi : IDisposable
{
    StylusEvmResponse Handle(StylusEvmRequestType requestType, ReadOnlyMemory<byte> input);
    GoSliceData AllocateGoSlice(byte[]? bytes);
}

public readonly record struct StylusEvmResponse(byte[] Result, byte[] RawData, ulong GasCost);
