using System.Runtime.InteropServices;

namespace Nethermind.Arbitrum.NativeHandler;

public static class Rust
{
    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern int stylus_call(
        GoSliceData module,
        GoSliceData calldata,
        StylusConfig config,
        NativeRequestHandler handler,
        EvmData evmData,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        ref RustBytes output,
        ref ulong gas,
        uint arbosTag);
    
    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern int stylus_activate(
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

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern int stylus_compile(
        GoSliceData wasm,
        ushort version,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        GoSliceData targetName,
        ref RustBytes output);

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern int stylus_target_set(
        GoSliceData name,
        GoSliceData description,
        ref RustBytes output,
        [MarshalAs(UnmanagedType.I1)] bool native);

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern void stylus_cache_module(
        GoSliceData module,
        Bytes32 module_hash,
        ushort version,
        uint arbos_tag,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern void stylus_evict_module(
        Bytes32 module_hash,
        ushort version,
        uint arbos_tag,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern void stylus_reorg_vm(
        ulong block,
        uint arbos_tag);

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern void stylus_get_cache_metrics(IntPtr output);

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern void stylus_set_cache_lru_capacity(ulong capacity_bytes);

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern void stylus_clear_lru_cache();

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern void stylus_clear_long_term_cache();

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong stylus_get_entry_size_estimate_bytes(
        GoSliceData module,
        ushort version,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [DllImport("/Users/tanishqjasoria/RiderProjects/nethermind-arbitrum/src/Nethermind.Arbitrum/NativeHandler/C/libstylus.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern int wat_to_wasm(
        GoSliceData wat,
        ref RustBytes output);
}