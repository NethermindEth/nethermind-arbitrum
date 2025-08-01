// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;
using System.Text;

namespace Nethermind.Arbitrum.Arbos.Stylus;

public readonly record struct StylusResult<T>(UserOutcomeKind Status, string Error, T? Value)
{
    public bool IsSuccess => Status == UserOutcomeKind.Success;

    public void Deconstruct(out UserOutcomeKind status, out string error, out T? value)
    {
        status = Status;
        error = Error;
        value = Value;
    }

    public static StylusResult<T> Success(T value)
    {
        return new StylusResult<T>(UserOutcomeKind.Success, string.Empty, value);
    }

    public static StylusResult<T> Failure(UserOutcomeKind status, string error)
    {
        return new StylusResult<T>(status, error, default);
    }
}

public readonly record struct ActivateResult(Bytes32 ModuleHash, StylusData ActivationInfo, byte[] WavmModule);

public static unsafe partial class StylusNative
{
    public static StylusResult<byte[]> Call(byte[] module, byte[] callData, StylusConfig config, IStylusEvmApi api, EvmData evmData, bool debug,
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

        return status != UserOutcomeKind.Success
            ? StylusResult<byte[]>.Failure(status, Encoding.UTF8.GetString(resultBytes))
            : StylusResult<byte[]>.Success(resultBytes);
    }

    public static StylusResult<byte[]> Compile(byte[] wasm, ushort version, bool debug, string targetName)
    {
        using GoSliceHandle wasmSlice = GoSliceHandle.From(wasm);
        using GoSliceHandle targetSlice = GoSliceHandle.From(targetName);

        RustBytes output = new();
        UserOutcomeKind status = stylus_compile(wasmSlice.Data, version, debug, targetSlice.Data, ref output);
        byte[] resultBytes = ReadAndFreeRustBytes(output);

        return status != UserOutcomeKind.Success
            ? StylusResult<byte[]>.Failure(status, Encoding.UTF8.GetString(resultBytes))
            : StylusResult<byte[]>.Success(resultBytes);
    }

    public static StylusResult<byte[]> SetTarget(string name, string descriptor, bool isNative)
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
            ? StylusResult<byte[]>.Failure(status, Encoding.UTF8.GetString(resultBytes))
            : StylusResult<byte[]>.Success(resultBytes);
    }

    public static StylusResult<byte[]> WatToWasm(byte[] wat)
    {
        using GoSliceHandle watSlice = GoSliceHandle.From(wat);

        RustBytes output = new();
        UserOutcomeKind watStatus = wat_to_wasm(watSlice.Data, ref output);

        byte[] resultBytes = ReadAndFreeRustBytes(output);

        return watStatus == UserOutcomeKind.Success
            ? StylusResult<byte[]>.Success(resultBytes)
            : StylusResult<byte[]>.Failure(watStatus, Encoding.UTF8.GetString(resultBytes));
    }

    public static StylusResult<ActivateResult> Activate(byte[] wasm, ushort pageLimit, ushort stylusVersion, ulong arbosVersionForGas, bool debug,
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
            ? StylusResult<ActivateResult>.Failure(status, Encoding.UTF8.GetString(resultBytes))
            : StylusResult<ActivateResult>.Success(new ActivateResult(moduleHash, stylusData, resultBytes));
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
}
