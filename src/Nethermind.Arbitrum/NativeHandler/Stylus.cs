using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Nethermind.Arbitrum.Exceptions;
using Nethermind.Core.Extensions;

namespace Nethermind.Arbitrum.NativeHandler;

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
        var output = new RustBytes();
        var status = stylus_call(
            Utils.CreateSlice(module),
            Utils.CreateSlice(callData),
            config,
            handler,
            evmData,
            debug,
            ref output,
            ref gas,
            arbOsTag);

        if (status != 0) throw new StylusCallFailedException(Utils.ReadBytes(output));
        return Utils.ReadBytes(output);
    }
    
    
    public static byte[] Compile(byte[] wasm, ushort version, bool debug, string targetName)
    {
        var output = new RustBytes();
        string target = targetName;
        var status = stylus_compile(Utils.CreateSlice(wasm), version, debug, Utils.CreateSlice(targetName), ref output);

        switch (status)
        {
            case 0: return Utils.ReadBytes(output);
            case 2: throw new StylusCompilationFailedException(target, Utils.ReadBytes(output));
            default: throw new Exception($"Compile failed. Status: {status}");;
        }
    }
    
    public static void SetCompilationTarget(string name, string description, bool isNative)
    {
        RustBytes output = new RustBytes();
        int status = stylus_target_set(
            Utils.CreateSlice(name),
            Utils.CreateSlice(description),
            ref output,
            isNative);
        if (status != 0) throw new StylusTargetSetFailedException(Utils.TargetArm64, Utils.ReadBytes(output));
    }
    
    public static byte[] CompileWatToWasm(byte[] watBytes)
    {
        RustBytes wasmOutput = new RustBytes();
        int watStatus = wat_to_wasm(Utils.CreateSlice(watBytes), ref wasmOutput);
        if (watStatus != 0) throw new StylusWat2WasmFailedException(Utils.ReadBytes(wasmOutput));
        return Utils.ReadBytes(wasmOutput);
    }

    #region private methods

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