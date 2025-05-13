using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Nethermind.Arbitrum.NativeHandler;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void HandleRequestDelegate(
    UIntPtr apiId,
    uint reqType,
    ref RustSlice data,
    ref ulong outCost,
    out GoSliceData outResult,
    out GoSliceData outRawData);

[StructLayout(LayoutKind.Sequential)]
public struct NativeRequestHandler
{
    public IntPtr handle_request_fptr; // function pointer
    public UIntPtr id;
}

public class EvmApiRegistry
{
    private static readonly ConcurrentDictionary<nuint, INativeApi> _handlers = new();
    private static uint _nextId = 1;

    public static nuint Register(INativeApi api)
    {
        var id = Interlocked.Increment(ref _nextId);
        _handlers[id] = api;
        return id;
    }

    public static INativeApi Get(nuint id)
    {
        if (!_handlers.TryGetValue(id, out var api))
            throw new InvalidOperationException($"No registered IEvmApi for id {id}");
        return api;
    }

    public static void Unregister(nuint id)
    {
        _handlers.TryRemove(id, out _);
    }
}

public static class RegisterHandler
{
    public static NativeRequestHandler CreateHandler(UIntPtr id)
    {
        var handlerDelegate = new HandleRequestDelegate(HandleRequest);
        IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(handlerDelegate);

        return new NativeRequestHandler
        {
            handle_request_fptr = fnPtr,
            id = id
        };
    }

    private const int EvmApiMethodReqOffset = 0x10000000;
    
    public static void HandleRequest(
        UIntPtr apiId,
        uint reqType,
        ref RustSlice data,
        ref ulong outCost,
        out GoSliceData outResult,
        out GoSliceData outRawData)
    {
        INativeApi api = EvmApiRegistry.Get(apiId);
        Console.WriteLine($"Handler: {(int)apiId} {reqType}");
        byte[] input = new byte[(int)data.len];
        Marshal.Copy(data.ptr, input, 0, input.Length);

        RequestType req = (RequestType)(reqType - EvmApiMethodReqOffset);

        var outputC = api.Handle(req, input);

        // TEMP: simulate response
        byte[] result = outputC.result;
        byte[] rawData = outputC.rawData;

        IntPtr resultPtr = Marshal.AllocHGlobal(result.Length);
        Marshal.Copy(result, 0, resultPtr, result.Length);
        
        
        IntPtr rawDataPtr = Marshal.AllocHGlobal(rawData.Length);
        Marshal.Copy(rawData, 0, rawDataPtr, rawData.Length);

        outResult = new GoSliceData
        {
            ptr = resultPtr,
            len = (UIntPtr)result.Length
        };

        outRawData = new GoSliceData
        {
            ptr = rawDataPtr,
            len = (UIntPtr)rawData.Length
        };

        outCost = outputC.gasCost; // simulated gas cost
        Console.WriteLine($"");
    }
}
