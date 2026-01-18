// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public sealed class ArbWasmParser : IArbitrumPrecompile<ArbWasmParser>
{
    public static readonly ArbWasmParser Instance = new();

    private static readonly uint _activateProgramId = PrecompileHelper.GetMethodId("activateProgram(address)");
    private static readonly uint _blockCacheSizeId = PrecompileHelper.GetMethodId("blockCacheSize()");
    private static readonly uint _codeHashAsmSizeId = PrecompileHelper.GetMethodId("codehashAsmSize(bytes32)");
    private static readonly uint _codeHashKeepaliveId = PrecompileHelper.GetMethodId("codehashKeepalive(bytes32)");
    private static readonly uint _codeHashVersionId = PrecompileHelper.GetMethodId("codehashVersion(bytes32)");
    private static readonly uint _expiryDaysId = PrecompileHelper.GetMethodId("expiryDays()");
    private static readonly uint _freePagesId = PrecompileHelper.GetMethodId("freePages()");
    private static readonly uint _initCostScalarId = PrecompileHelper.GetMethodId("initCostScalar()");
    private static readonly uint _inkPriceId = PrecompileHelper.GetMethodId("inkPrice()");
    private static readonly uint _keepaliveDaysId = PrecompileHelper.GetMethodId("keepaliveDays()");
    private static readonly uint _maxStackDepthId = PrecompileHelper.GetMethodId("maxStackDepth()");
    private static readonly uint _minInitGasId = PrecompileHelper.GetMethodId("minInitGas()");
    private static readonly uint _pageGasId = PrecompileHelper.GetMethodId("pageGas()");
    private static readonly uint _pageLimitId = PrecompileHelper.GetMethodId("pageLimit()");
    private static readonly uint _pageRampId = PrecompileHelper.GetMethodId("pageRamp()");
    private static readonly uint _programInitGasId = PrecompileHelper.GetMethodId("programInitGas(address)");
    private static readonly uint _programMemoryFootprintId = PrecompileHelper.GetMethodId("programMemoryFootprint(address)");
    private static readonly uint _programTimeLeftId = PrecompileHelper.GetMethodId("programTimeLeft(address)");
    private static readonly uint _programVersionId = PrecompileHelper.GetMethodId("programVersion(address)");
    private static readonly uint _stylusVersionId = PrecompileHelper.GetMethodId("stylusVersion()");
    public static ArbitrumFunctionDescription ActivateProgramDescription => PrecompileFunctionDescription[_activateProgramId];

    public static Address Address => ArbWasm.Address;

    public static ulong AvailableFromArbosVersion => ArbosVersion.Stylus;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; } = AbiMetadata.GetAllFunctionDescriptions(ArbWasm.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    static ArbWasmParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _activateProgramId, ActivateProgram },
            { _codeHashKeepaliveId, CodeHashKeepalive },
            { _stylusVersionId, StylusVersion },
            { _inkPriceId, InkPrice },
            { _maxStackDepthId, MaxStackDepth },
            { _freePagesId, FreePages },
            { _pageGasId, PageGas },
            { _pageRampId, PageRamp },
            { _pageLimitId, PageLimit },
            { _minInitGasId, MinInitGas },
            { _initCostScalarId, InitCostScalar },
            { _expiryDaysId, ExpiryDays },
            { _keepaliveDaysId, KeepaliveDays },
            { _blockCacheSizeId, BlockCacheSize },
            { _codeHashVersionId, CodeHashVersion },
            { _codeHashAsmSizeId, CodeHashAsmSize },
            { _programVersionId, ProgramVersion },
            { _programInitGasId, ProgramInitGas },
            { _programMemoryFootprintId, ProgramMemoryFootprint },
            { _programTimeLeftId, ProgramTimeLeft },
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static byte[] ActivateProgram(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_activateProgramId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ArbWasmActivateProgramResult result = ArbWasm.ActivateProgram(context, program);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            result.Version,
            result.DataFee
        );
    }

    private static byte[] BlockCacheSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.BlockCacheSize(context)).ToBigEndian();

    private static byte[] CodeHashAsmSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_codeHashAsmSizeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] codeHashBytes = (byte[])decoded[0];
        Hash256 codeHash = new(codeHashBytes);

        uint size = ArbWasm.CodeHashAsmSize(context, codeHash);
        return new UInt256(size).ToBigEndian();
    }

    private static byte[] CodeHashKeepalive(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_codeHashKeepaliveId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] codeHashBytes = (byte[])decoded[0];
        Hash256 codeHash = new(codeHashBytes);

        ArbWasm.CodeHashKeepAlive(context, codeHash);
        return [];
    }

    private static byte[] CodeHashVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_codeHashVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] codeHashBytes = (byte[])decoded[0];
        Hash256 codeHash = new(codeHashBytes);

        ushort version = ArbWasm.CodeHashVersion(context, codeHash);
        return new UInt256(version).ToBigEndian();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        foreach (ArbitrumFunctionDescription functionDescription in PrecompileFunctionDescription.Values)
            functionDescription.ArbOSVersion = AvailableFromArbosVersion;
    }

    private static byte[] ExpiryDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.ExpiryDays(context)).ToBigEndian();

    private static byte[] FreePages(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.FreePages(context)).ToBigEndian();

    private static byte[] InitCostScalar(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.InitCostScalar(context)).ToBigEndian();

    private static byte[] InkPrice(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.InkPrice(context)).ToBigEndian();

    private static byte[] KeepaliveDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.KeepaliveDays(context)).ToBigEndian();

    private static byte[] MaxStackDepth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.MaxStackDepth(context)).ToBigEndian();

    private static byte[] MinInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        (ulong gas, ulong cached) = ArbWasm.MinInitGas(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_minInitGasId].AbiFunctionDescription.GetReturnInfo().Signature,
            gas,
            cached
        );
    }

    private static byte[] PageGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.PageGas(context)).ToBigEndian();

    private static byte[] PageLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.PageLimit(context)).ToBigEndian();

    private static byte[] PageRamp(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.PageRamp(context)).ToBigEndian();

    private static byte[] ProgramInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_programInitGasId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        (ulong gas, ulong gasWhenCached) = ArbWasm.ProgramInitGas(context, program);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            gas,
            gasWhenCached
        );
    }

    private static byte[] ProgramMemoryFootprint(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_programMemoryFootprintId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ushort footprint = ArbWasm.ProgramMemoryFootprint(context, program);
        return new UInt256(footprint).ToBigEndian();
    }

    private static byte[] ProgramTimeLeft(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_programTimeLeftId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ulong secs = ArbWasm.ProgramTimeLeft(context, program);
        return new UInt256(secs).ToBigEndian();
    }

    private static byte[] ProgramVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_programVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ushort version = ArbWasm.ProgramVersion(context, program);
        return new UInt256(version).ToBigEndian();
    }

    private static byte[] StylusVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.StylusVersion(context)).ToBigEndian();
}
