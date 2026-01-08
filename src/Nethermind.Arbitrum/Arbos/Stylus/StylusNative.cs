// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace Nethermind.Arbitrum.Arbos.Stylus;

public readonly record struct StylusNativeResult<T>(UserOutcomeKind Status, string Error, T? Value)
{
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => Status == UserOutcomeKind.Success;

    public void Deconstruct(out UserOutcomeKind status, out string error, out T? value)
    {
        status = Status;
        error = Error;
        value = Value;
    }

    public static StylusNativeResult<T> Success(T value)
    {
        return new StylusNativeResult<T>(UserOutcomeKind.Success, string.Empty, value);
    }

    public static StylusNativeResult<T> Failure(UserOutcomeKind status, string error)
    {
        return new StylusNativeResult<T>(status, error, default);
    }

    public static StylusNativeResult<T> Failure(UserOutcomeKind status, string error, T data)
    {
        return new StylusNativeResult<T>(status, error, data);
    }
}

public readonly record struct ActivateResult(Bytes32 ModuleHash, StylusData ActivationInfo, byte[] WavmModule);

public static unsafe partial class StylusNative
{
    public static StylusNativeResult<byte[]> Call(byte[] module, byte[] callData, StylusConfig config, IStylusEvmApi api, EvmData evmData, bool debug,
        uint arbOsTag, ref ulong gas)
    {
        using GoSliceHandle moduleSlice = GoSliceHandle.From(module);
        using GoSliceHandle callDataSlice = GoSliceHandle.From(callData);
        using StylusEnvApiRegistration registration = StylusEvmApiRegistry.Register(api);

        NativeRequestHandler handler = new()
        {
            HandleRequestFptr = &StylusEvmApiRegistry.HandleStylusEnvApiRequest,
            Id = registration.Id
        };

        RustBytes output = new();
        UserOutcomeKind status = stylus_call(
            moduleSlice.Data,
            callDataSlice.Data,
            config,
            handler,
            evmData,
            debug,
            ref output,
            ref gas,
            arbOsTag);

        byte[] resultBytes = ReadAndFreeRustBytes(output);

        return status switch
        {
            UserOutcomeKind.Success => StylusNativeResult<byte[]>.Success(resultBytes),
            UserOutcomeKind.Revert => StylusNativeResult<byte[]>.Failure(status, Encoding.UTF8.GetString(resultBytes), resultBytes),
            UserOutcomeKind.Failure => StylusNativeResult<byte[]>.Failure(status, Encoding.UTF8.GetString(resultBytes)),
            UserOutcomeKind.OutOfInk => StylusNativeResult<byte[]>.Failure(status, "out of gas"),
            UserOutcomeKind.OutOfStack => StylusNativeResult<byte[]>.Failure(status, "max call depth exceeded"),
            _ => StylusNativeResult<byte[]>.Failure(status, "Unknown error during Stylus call", resultBytes)
        };
    }

    public static StylusNativeResult<byte[]> Compile(byte[] wasm, ushort version, bool debug, string targetName, bool cranelift)
    {
        using GoSliceHandle wasmSlice = GoSliceHandle.From(wasm);
        using GoSliceHandle targetSlice = GoSliceHandle.From(targetName);

        RustBytes output = new();
        UserOutcomeKind status = stylus_compile(wasmSlice.Data, version, debug, targetSlice.Data, cranelift, ref output);
        byte[] resultBytes = ReadAndFreeRustBytes(output);

        return status != UserOutcomeKind.Success
            ? StylusNativeResult<byte[]>.Failure(status, Encoding.UTF8.GetString(resultBytes))
            : StylusNativeResult<byte[]>.Success(resultBytes);
    }

    public static StylusNativeResult<byte[]> SetTarget(string name, string descriptor, bool isNative)
    {
        using GoSliceHandle nameSlice = GoSliceHandle.From(name);
        using GoSliceHandle descriptorSlice = GoSliceHandle.From(descriptor);

        RustBytes output = new();
        UserOutcomeKind status = stylus_target_set(
            nameSlice.Data,
            descriptorSlice.Data,
            ref output,
            isNative);

        byte[] resultBytes = ReadAndFreeRustBytes(output);

        return status != UserOutcomeKind.Success
            ? StylusNativeResult<byte[]>.Failure(status, Encoding.UTF8.GetString(resultBytes))
            : StylusNativeResult<byte[]>.Success(resultBytes);
    }

    public static StylusNativeResult<byte[]> WatToWasm(byte[] wat)
    {
        using GoSliceHandle watSlice = GoSliceHandle.From(wat);

        RustBytes output = new();
        UserOutcomeKind watStatus = wat_to_wasm(watSlice.Data, ref output);

        byte[] resultBytes = ReadAndFreeRustBytes(output);

        return watStatus == UserOutcomeKind.Success
            ? StylusNativeResult<byte[]>.Success(resultBytes)
            : StylusNativeResult<byte[]>.Failure(watStatus, Encoding.UTF8.GetString(resultBytes));
    }

    public static StylusNativeResult<ActivateResult> Activate(byte[] wasm, ushort pageLimit, ushort stylusVersion, ulong arbosVersionForGas, bool debug,
        Bytes32 codeHash, ref ulong gas)
    {
        using GoSliceHandle wasmSlice = GoSliceHandle.From(wasm);

        RustBytes output = new();
        UserOutcomeKind status = stylus_activate(
            wasmSlice.Data,
            pageLimit,
            stylusVersion,
            arbosVersionForGas,
            debug,
            ref output,
            ref codeHash,
            out Bytes32 moduleHash,
            out StylusData stylusData,
            ref gas);

        byte[] resultBytes = ReadAndFreeRustBytes(output);

        return status != UserOutcomeKind.Success
            ? StylusNativeResult<ActivateResult>.Failure(status, Encoding.UTF8.GetString(resultBytes))
            : StylusNativeResult<ActivateResult>.Success(new ActivateResult(moduleHash, stylusData, resultBytes));
    }

    public static BrotliStatus BrotliCompress(ReadOnlySpan<byte> input, Span<byte> output, uint level, BrotliDictionary dictionary, out int bytesWritten)
    {
        ReadOnlySpan<byte> nonEmptyInput = EnsureBrotliNonEmpty(input);

        fixed (byte* inputPtr = nonEmptyInput)
        fixed (byte* outputPtr = output)
        {
            nuint inputLen = (nuint)nonEmptyInput.Length;
            nuint outputLen = (nuint)output.Length;

            BrotliBuffer inputBuffer = new() { Ptr = inputPtr, Len = &inputLen };
            BrotliBuffer outputBuffer = new() { Ptr = outputPtr, Len = &outputLen };

            BrotliStatus status = brotli_compress(inputBuffer, outputBuffer, dictionary, level);
            bytesWritten = (int)outputLen;

            return status;
        }
    }

    public static BrotliStatus BrotliDecompress(ReadOnlySpan<byte> input, Span<byte> output, BrotliDictionary dictionary, out int bytesWritten)
    {
        ReadOnlySpan<byte> nonEmptyInput = EnsureBrotliNonEmpty(input);

        fixed (byte* inputPtr = nonEmptyInput)
        fixed (byte* outputPtr = output)
        {
            nuint inputLen = (nuint)nonEmptyInput.Length;
            nuint outputLen = (nuint)output.Length;

            BrotliBuffer inputBuffer = new() { Ptr = inputPtr, Len = &inputLen };
            BrotliBuffer outputBuffer = new() { Ptr = outputPtr, Len = &outputLen };

            BrotliStatus status = brotli_decompress(inputBuffer, outputBuffer, dictionary);
            bytesWritten = (int)outputLen;

            return status;
        }
    }

    public static void SetWasmLruCacheCapacity(ulong capacity)
    {
        stylus_set_cache_lru_capacity(capacity);
    }

    public static int GetCompressedBufferSize(int inputSize)
    {
        // This matches the typical brotli worst-case compression bound
        return inputSize + (inputSize >> 10) * 8 + 64;
    }

    private static byte[] ReadAndFreeRustBytes(RustBytes output)
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

    private static ReadOnlySpan<byte> EnsureBrotliNonEmpty(ReadOnlySpan<byte> input)
    {
        // Nitro: Ensures pointer is not null (shouldn't be necessary, but brotli docs are picky about NULL)
        return input.Length > 0 ? input : [0x00];
    }
}
