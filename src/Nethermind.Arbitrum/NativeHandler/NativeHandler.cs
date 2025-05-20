using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Nethermind.Arbitrum.Exceptions;


namespace Nethermind.Arbitrum.NativeHandler;

public static class EvmApiRegistry
{
    private static readonly ConcurrentDictionary<nuint, INativeApi> Handlers = new();
    private static uint _nextId = 0;

    public static nuint Register(INativeApi api)
    {
        // Interlocked.Increment deals with uint, result is uint.
        // Implicit conversion from uint to nuint for dictionary key is safe.
        nuint id = Interlocked.Increment(ref _nextId);
        Handlers[id] = api;
        return id;
    }

    public static INativeApi Get(nuint id)
    {
        if (!Handlers.TryGetValue(id, out var api))
        {
            throw new StylusEvmApiNotRegistered(id);
        }
        return api;
    }

    public static void Unregister(nuint id) => Handlers.TryRemove(id, out _);
}

public static class RegisterHandler
{
    private static readonly HandleRequestDelegate GlobalHandleRequestDelegate = HandleRequest;
    private static readonly IntPtr GlobalHandleRequestFnPtr = Marshal.GetFunctionPointerForDelegate(GlobalHandleRequestDelegate);

    private const int EvmApiMethodReqOffset = 0x10000000;

    /// <summary>
    /// Creates a native request handler structure for a registered API.
    /// </summary>
    /// <param name="registeredApiId">The ID obtained from <see cref="EvmApiRegistry.Register"/>.</param>
    /// <returns>A <see cref="NativeRequestHandler"/> struct to be passed to native code.</returns>
    public static NativeRequestHandler Create(nuint registeredApiId)
    {
        return new NativeRequestHandler
        {
            HandleRequestFptr = GlobalHandleRequestFnPtr,
            // Store the nuint ID as UIntPtr, which are compatible pointer-sized types.
            Id = (UIntPtr)registeredApiId
        };
    }

    /// <summary>
    /// Handles requests from native code. This method is called via a function pointer.
    /// </summary>
    private static void HandleRequest(
        UIntPtr apiId,
        uint reqType,
        ref RustBytes data,
        ref ulong outCost,
        out GoSliceData outResult,
        out GoSliceData outRawData)
    {
        var api = EvmApiRegistry.Get(apiId);
        var input = new byte[(int)data.Len];
        if (data.Ptr != IntPtr.Zero)
        {
            Marshal.Copy(data.Ptr, input, 0, input.Length);
        }

        var request = (RequestType)(reqType - EvmApiMethodReqOffset);
        var (result, rawData, gasCost) = api.Handle(request, input);

        outResult = AllocateGoSlice(result);
        outRawData = AllocateGoSlice(rawData);
        outCost = gasCost;
    }

    private static GoSliceData AllocateGoSlice(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return new GoSliceData { Ptr = IntPtr.Zero, Len = UIntPtr.Zero };
        }

        IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, buffer, bytes.Length);

        return new GoSliceData
        {
            Ptr = buffer,
            Len = (UIntPtr)bytes.Length
        };
    }
}
