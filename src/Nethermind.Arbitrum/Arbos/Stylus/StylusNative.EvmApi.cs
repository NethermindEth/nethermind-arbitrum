// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

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

        byte[] input = [];
        if (data != null && data->Ptr != IntPtr.Zero && data->Len != UIntPtr.Zero)
        {
            input = new byte[(int)data->Len];
            Marshal.Copy(data->Ptr, input, 0, input.Length);
        }

        StylusEvmRequestType request = (StylusEvmRequestType)(reqType - EvmApiMethodReqOffset);
        (byte[] result, byte[] rawData, ulong gasCost) = api.Handle(request, input);

        *outResult = api.AllocateGoSlice(result);
        *outRawData = api.AllocateGoSlice(rawData);
        *outCost = gasCost;
    }
}

public interface IStylusEvmApi : IDisposable
{
    StylusEvmResponse Handle(StylusEvmRequestType requestType, byte[] input);
    GoSliceData AllocateGoSlice(byte[]? bytes);
}

public readonly record struct StylusEvmResponse(byte[] Result, byte[] RawData, ulong GasCost);
