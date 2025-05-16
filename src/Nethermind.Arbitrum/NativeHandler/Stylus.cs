using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Nethermind.Arbitrum.Exceptions;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.NativeHandler;

[StructLayout(LayoutKind.Sequential)]
public struct GoSliceData
{
    public IntPtr ptr; // pointer to data
    public UIntPtr len; // length of data
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
    public IntPtr ptr;
    public UIntPtr len;
    public UIntPtr cap;
}

[StructLayout(LayoutKind.Sequential)]
public struct PricingParams
{
    public uint ink_price;
}

[StructLayout(LayoutKind.Sequential)]
public struct StylusConfig
{
    public ushort version;
    public uint max_depth;
    public PricingParams pricing;
}

[StructLayout(LayoutKind.Sequential)]
public record struct Bytes32
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] bytes;
}

[StructLayout(LayoutKind.Sequential)]
public struct Bytes20 : IEquatable<Bytes20>
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
    public byte[] bytes;

    public bool Equals(Bytes20 other)
    {
        return bytes.Equals(other.bytes);
    }

    public override bool Equals(object? obj)
    {
        return obj is Bytes20 other && Equals(other);
    }

    public override int GetHashCode()
    {
        return bytes.GetHashCode();
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct EvmData
{
    public ulong arbos_version;
    public Bytes32 block_basefee;
    public ulong chainid;
    public Bytes20 block_coinbase;
    public ulong block_gas_limit;
    public ulong block_number;
    public ulong block_timestamp;
    public Bytes20 contract_address;
    public Bytes32 module_hash;
    public Bytes20 msg_sender;
    public Bytes32 msg_value;
    public Bytes32 tx_gas_price;
    public Bytes20 tx_origin;
    public uint reentrant;
    [MarshalAs(UnmanagedType.I1)] public bool cached;
    [MarshalAs(UnmanagedType.I1)] public bool tracing;
}

[StructLayout(LayoutKind.Sequential)]
public struct StylusData
{
    public ushort init_cost;
    public ushort cached_init_cost;
    public ushort footprint;
    public uint asm_estimate;
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
        var target = targetName;
        using var wasmSlice = CreateSlice(wasm);
        using var targetSlice = CreateSlice(targetName);
        var status = stylus_compile(wasmSlice.Data, version, debug, targetSlice.Data, ref output);
        

        var resultBytes = ReadBytes(output);
        switch (status)
        {
            case 0: return resultBytes;
            case 2: throw new StylusCompilationFailedException(target, resultBytes);
            default: throw new Exception($"Compile failed. Status: {status}");;
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
            return new GoSlice(new GoSliceData { ptr = IntPtr.Zero, len = UIntPtr.Zero });
        }

        // Pin the managed array in memory
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        return new GoSlice(new GoSliceData { ptr = handle.AddrOfPinnedObject(), len = (UIntPtr)bytes.Length }, handle); // Store handle to free later
    }

    private static byte[] ReadBytes(RustBytes output)
    {
        if (output.len == 0)
        {
            free_rust_bytes(output);
            return [];
        }
        byte[] buffer = new byte[(int)output.len];
        Marshal.Copy(output.ptr, buffer, 0, buffer.Length);
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
        ushort page_limit,
        ushort stylus_version,
        ulong arbos_version,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        ref RustBytes output,
        ref Bytes32 codehash,
        out Bytes32 module_hash,
        out StylusData stylus_data,
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
        Bytes32 module_hash,
        ushort version,
        uint arbos_tag,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [DllImport("libstylus", CallingConvention = CallingConvention.Cdecl)]
    private static extern void stylus_evict_module(
        Bytes32 module_hash,
        ushort version,
        uint arbos_tag,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void stylus_reorg_vm(
        ulong block,
        uint arbos_tag);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void stylus_get_cache_metrics(IntPtr output);

    [LibraryImport("libstylus")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial void stylus_set_cache_lru_capacity(ulong capacity_bytes);

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