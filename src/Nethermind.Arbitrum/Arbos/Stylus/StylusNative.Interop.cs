// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nethermind.Arbitrum.Arbos.Stylus;

public static partial class StylusNative
{
    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial UserOutcomeKind stylus_activate(
        GoSliceData wasm,
        ushort pageLimit,
        ushort stylusVersion,
        ulong arbosVersionForGas,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        ref RustBytes output,
        ref Bytes32 codeHash,
        out Bytes32 moduleHash,
        out StylusData stylusData,
        ref ulong gas);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial UserOutcomeKind stylus_compile(
        GoSliceData wasm,
        ushort version,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        GoSliceData targetName,
        // true → Uses the Cranelift compiler - produces more optimized code but slower to compile
        // false → Uses the Singlepass compiler - faster compilation but less optimized output
        [MarshalAs(UnmanagedType.I1)] bool cranelift,
        ref RustBytes output);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial UserOutcomeKind wat_to_wasm(
        GoSliceData wat,
        ref RustBytes output);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial UserOutcomeKind stylus_target_set(
        GoSliceData name,
        GoSliceData description,
        ref RustBytes output,
        [MarshalAs(UnmanagedType.I1)] bool native);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial UserOutcomeKind stylus_call(
        GoSliceData module,
        GoSliceData calldata,
        StylusConfig config,
        NativeRequestHandler handler,
        EvmData evmData,
        [MarshalAs(UnmanagedType.I1)] bool debug,
        ref RustBytes output,
        ref ulong gas,
        uint arbosTag);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void stylus_set_cache_lru_capacity(ulong capacityBytes);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void stylus_cache_module(
        GoSliceData module,
        Bytes32 moduleHash,
        ushort version,
        uint arbosTag,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void stylus_evict_module(
        Bytes32 moduleHash,
        ushort version,
        uint arbosTag,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void stylus_reorg_vm(
        ulong block,
        uint arbosTag);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void stylus_get_cache_metrics(IntPtr output);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void stylus_clear_lru_cache();

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void stylus_clear_long_term_cache();

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial ulong stylus_get_entry_size_estimate_bytes(
        GoSliceData module,
        ushort version,
        [MarshalAs(UnmanagedType.I1)] bool debug);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void free_rust_bytes(RustBytes bytes); // From from arbitrator/prover/src/lib.rs

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial BrotliStatus brotli_compress(
        BrotliBuffer input,
        BrotliBuffer output,
        BrotliDictionary dictionary,
        uint level);

    [LibraryImport(LibraryName)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe partial BrotliStatus brotli_decompress(
        BrotliBuffer input,
        BrotliBuffer output,
        BrotliDictionary dictionary);
}
