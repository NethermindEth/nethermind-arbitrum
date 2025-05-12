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

public static class RegisterHandler
{
    public static HandleRequestDelegate? handlerDelegate;

    public static NativeRequestHandler CreateHandler(UIntPtr id)
    {
        handlerDelegate = new HandleRequestDelegate(HandleRequest);
        IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(handlerDelegate);

        return new NativeRequestHandler
        {
            handle_request_fptr = fnPtr,
            id = id
        };
    }
    
    public static void HandleRequest(
        UIntPtr apiId,
        uint reqType,
        ref RustSlice data,
        ref ulong outCost,
        out GoSliceData outResult,
        out GoSliceData outRawData)
    {
        Console.WriteLine($"Handler: {(int)apiId} {reqType}");
        byte[] input = new byte[(int)data.len];
        Marshal.Copy(data.ptr, input, 0, input.Length);

        // TEMP: simulate response
        byte[] result = System.Text.Encoding.UTF8.GetBytes("OK");
        byte[] rawData = new byte[0];

        IntPtr resultPtr = Marshal.AllocHGlobal(result.Length);
        Marshal.Copy(result, 0, resultPtr, result.Length);

        outResult = new GoSliceData
        {
            ptr = resultPtr,
            len = (UIntPtr)result.Length
        };

        outRawData = new GoSliceData
        {
            ptr = IntPtr.Zero,
            len = UIntPtr.Zero
        };

        outCost = 123; // simulated gas cost
        Console.WriteLine($"");
        
    }


}
