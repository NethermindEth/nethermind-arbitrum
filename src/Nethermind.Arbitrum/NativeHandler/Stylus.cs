using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Nethermind.Arbitrum.Exceptions;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.NativeHandler;


[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void HandleRequestDelegate(
    UIntPtr apiId,
    uint reqType,
    ref RustBytes data,
    ref ulong outCost,
    out GoSliceData outResult,
    out GoSliceData outRawData);

[StructLayout(LayoutKind.Sequential)]
public struct NativeRequestHandler
{
    public IntPtr HandleRequestFptr; // function pointer
    public UIntPtr Id;
}

[StructLayout(LayoutKind.Sequential)]
public struct GoSliceData
{
    public IntPtr Ptr; // pointer to data
    public UIntPtr Len; // length of data
}

// And GoSlice would need to implement IDisposable to free the GCHandle
public readonly struct GoSlice(GoSliceData data, GCHandle? gcHandle = null) : IDisposable
{
    public GoSliceData Data { get; } = data;
    public void Dispose()
    {
        if (gcHandle.HasValue && gcHandle.Value.IsAllocated)
        {
            gcHandle.Value.Free();
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct RustBytes
{
    public IntPtr Ptr;
    public UIntPtr Len;
    public UIntPtr Cap;
}

[StructLayout(LayoutKind.Sequential)]
public struct PricingParams
{
    public uint InkPrice;
}

[StructLayout(LayoutKind.Sequential)]
public struct StylusConfig
{
    public ushort Version;
    public uint MaxDepth;
    public PricingParams Pricing;
}

[StructLayout(LayoutKind.Sequential)]
public record struct Bytes32
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] Bytes;
}

[StructLayout(LayoutKind.Sequential)]
public struct Bytes20 : IEquatable<Bytes20>
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public byte[] Bytes;

    public bool Equals(Bytes20 other)
    {
        return Bytes.Equals(other.Bytes);
    }

    public override bool Equals(object? obj)
    {
        return obj is Bytes20 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Bytes.GetHashCode();
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct EvmData
{
    public ulong ArbosVersion;
    public Bytes32 BlockBasefee;
    public ulong Chainid;
    public Bytes20 BlockCoinbase;
    public ulong BlockGasLimit;
    public ulong BlockNumber;
    public ulong BlockTimestamp;
    public Bytes20 ContractAddress;
    public Bytes32 ModuleHash;
    public Bytes20 MsgSender;
    public Bytes32 MsgValue;
    public Bytes32 TxGasPrice;
    public Bytes20 TxOrigin;
    public uint Reentrant;
    [MarshalAs(UnmanagedType.I1)] public bool Cached;
    [MarshalAs(UnmanagedType.I1)] public bool Tracing;
}

[StructLayout(LayoutKind.Sequential)]
public struct StylusData
{
    public ushort InitCost;
    public ushort CachedInitCost;
    public ushort Footprint;
    public uint AsmEstimate;
}

public static partial class Stylus
{
    private const string LibraryName = "libstylus";
    private static readonly int Initialized;
    private static string? _libraryFallbackPath;
    static Stylus()
    {
        if (Interlocked.Exchange(ref Initialized, 1) == 0)
            NativeLibrary.SetDllImportResolver(typeof(Stylus).Assembly, ResolveNativeLibrary);
    }
    
    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        _libraryFallbackPath ??= Path.Combine("NativeHandler/C", LibraryName);

        NativeLibrary.TryLoad(_libraryFallbackPath, assembly, searchPath, out var handle);
        return handle;
    }
    public static byte[] Call(byte[] module, byte[] callData, StylusConfig config, NativeRequestHandler handler, EvmData evmData, bool debug, uint arbOsTag, ref ulong gas)
    {
        using var moduleSlice = CreateSlice(module);
        using var callDataSlice = CreateSlice(callData);
        var output = new RustBytes();
        var status = stylus_call(
            moduleSlice.Data,
            callDataSlice.Data,
            config,
            handler,
            evmData,
            debug,
            ref output,
            ref gas,
            arbOsTag);

        var resultBytes = ReadBytes(output);
        if (status != 0) throw new StylusCallFailedException(resultBytes);
        return resultBytes;
    }
    
    
    public static byte[] Compile(byte[] wasm, ushort version, bool debug, string targetName)
    {
        var output = new RustBytes();
        using var wasmSlice = CreateSlice(wasm);
        using var targetSlice = CreateSlice(targetName);
        var status = stylus_compile(wasmSlice.Data, version, debug, targetSlice.Data, ref output);
        

        var resultBytes = ReadBytes(output);
        switch (status)
        {
            case 0: return resultBytes;
            case 2: throw new StylusCompilationFailedException(targetName, resultBytes);
            default:
                throw new Exception($"Compile failed. Status: {status}. Error: {Encoding.UTF8.GetString(resultBytes)}");
        }
    }
    
    public static void SetCompilationTarget(string name, string description, bool isNative)
    {
        using var nameSlice = CreateSlice(name);
        using var descriptionSlice = CreateSlice(description);
        var output = new RustBytes();
        int status = stylus_target_set(
            nameSlice.Data,
            descriptionSlice.Data,
            ref output,
            isNative);
        var resultBytes = ReadBytes(output);
        if (status != 0) throw new StylusTargetSetFailedException(Utils.TargetArm64, resultBytes);
    }
    
    public static byte[] CompileWatToWasm(byte[] watBytes)
    {
        using var wasmSlice = CreateSlice(watBytes);
        Unsafe.SkipInit(out RustBytes wasmOutput);
        int watStatus = wat_to_wasm(wasmSlice.Data, ref wasmOutput);
        var resultBytes = ReadBytes(wasmOutput);
        if (watStatus != 0) throw new StylusWat2WasmFailedException(resultBytes);
        return resultBytes;
    }

    #region private methods

    private static GoSlice CreateSlice(string s) => CreateSlice(Encoding.UTF8.GetBytes(s));

    private static GoSlice CreateSlice(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            return new GoSlice(new GoSliceData { Ptr = IntPtr.Zero, Len = UIntPtr.Zero });
        }

        // Pin the managed array in memory
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        return new GoSlice(new GoSliceData { Ptr = handle.AddrOfPinnedObject(), Len = (UIntPtr)bytes.Length }, handle); // Store handle to free later
    }

    private static byte[] ReadBytes(RustBytes output)
    {
        if (output.Len == 0)
        {
            free_rust_bytes(output);
            return [];
        }
        byte[] buffer = new byte[(int)output.Len];
        Marshal.Copy(output.Ptr, buffer, 0, buffer.Length);
        free_rust_bytes(output);
        return buffer;
    }

    [DllImport("libstylus", CallingConvention = CallingConvention.Cdecl)]
    private static extern int stylus_call(
        GoSliceData module,
        GoSliceData calldata,
        StylusConfig config,
        NativeRequestHandler handler,
        EvmData evmData,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        ref RustBytes output,
        ref ulong gas,
        uint arbosTag);
    
    [DllImport("libstylus", CallingConvention = CallingConvention.Cdecl)]
    private static extern int stylus_activate(
        GoSliceData wasm,
        ushort pageLimit,
        ushort stylusVersion,
        ulong arbosVersion,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        ref RustBytes output,
        ref Bytes32 codehash,
        out Bytes32 moduleHash,
        out StylusData stylusData,
        ref ulong gas);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int free_rust_bytes(RustBytes bytes);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial int stylus_compile(
        GoSliceData wasm,
        ushort version,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        GoSliceData targetName,
        ref RustBytes output);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial int stylus_target_set(
        GoSliceData name,
        GoSliceData description,
        ref RustBytes output,
        [MarshalAs(UnmanagedType.I1)] bool native);
    
    [DllImport("libstylus", CallingConvention = CallingConvention.Cdecl)]
    private static extern void stylus_cache_module(
        GoSliceData module,
        Bytes32 moduleHash,
        ushort version,
        uint arbosTag,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [DllImport("libstylus", CallingConvention = CallingConvention.Cdecl)]
    private static extern void stylus_evict_module(
        Bytes32 moduleHash,
        ushort version,
        uint arbosTag,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void stylus_reorg_vm(
        ulong block,
        uint arbosTag);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void stylus_get_cache_metrics(IntPtr output);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void stylus_set_cache_lru_capacity(ulong capacityBytes);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void stylus_clear_lru_cache();

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void stylus_clear_long_term_cache();

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial ulong stylus_get_entry_size_estimate_bytes(
        GoSliceData module,
        ushort version,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial int wat_to_wasm(
        GoSliceData wat,
        ref RustBytes output);
    
    #endregion
}