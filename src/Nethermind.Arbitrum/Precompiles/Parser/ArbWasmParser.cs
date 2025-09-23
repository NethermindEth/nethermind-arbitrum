// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public sealed class ArbWasmParser : IArbitrumPrecompile<ArbWasmParser>
{
    public static readonly ArbWasmParser Instance = new();
    public static Address Address => ArbWasm.Address;

    private static readonly Dictionary<string, AbiFunctionDescription> precompileFunctions;

    private static readonly uint ActivateProgramId = MethodIdHelper.GetMethodId("activateProgram(address)");
    private static readonly uint CodeHashKeepaliveId = MethodIdHelper.GetMethodId("codehashKeepalive(bytes32)");
    private static readonly uint StylusVersionId = MethodIdHelper.GetMethodId("stylusVersion()");
    private static readonly uint InkPriceId = MethodIdHelper.GetMethodId("inkPrice()");
    private static readonly uint MaxStackDepthId = MethodIdHelper.GetMethodId("maxStackDepth()");
    private static readonly uint FreePagesId = MethodIdHelper.GetMethodId("freePages()");
    private static readonly uint PageGasId = MethodIdHelper.GetMethodId("pageGas()");
    private static readonly uint PageRampId = MethodIdHelper.GetMethodId("pageRamp()");
    private static readonly uint PageLimitId = MethodIdHelper.GetMethodId("pageLimit()");
    private static readonly uint MinInitGasId = MethodIdHelper.GetMethodId("minInitGas()");
    private static readonly uint InitCostScalarId = MethodIdHelper.GetMethodId("initCostScalar()");
    private static readonly uint ExpiryDaysId = MethodIdHelper.GetMethodId("expiryDays()");
    private static readonly uint KeepaliveDaysId = MethodIdHelper.GetMethodId("keepaliveDays()");
    private static readonly uint BlockCacheSizeId = MethodIdHelper.GetMethodId("blockCacheSize()");
    private static readonly uint CodeHashVersionId = MethodIdHelper.GetMethodId("codehashVersion(bytes32)");
    private static readonly uint CodeHashAsmSizeId = MethodIdHelper.GetMethodId("codehashAsmSize(bytes32)");
    private static readonly uint ProgramVersionId = MethodIdHelper.GetMethodId("programVersion(address)");
    private static readonly uint ProgramInitGasId = MethodIdHelper.GetMethodId("programInitGas(address)");
    private static readonly uint ProgramMemoryFootprintId = MethodIdHelper.GetMethodId("programMemoryFootprint(address)");
    private static readonly uint ProgramTimeLeftId = MethodIdHelper.GetMethodId("programTimeLeft(address)");

    private static readonly AbiEncodingInfo ActivateProgramOutput;
    private static readonly AbiEncodingInfo MinInitGasOutput;
    private static readonly AbiEncodingInfo ProgramInitGasOutput;

    static ArbWasmParser()
    {
        precompileFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbWasm.Abi);

        ActivateProgramOutput = precompileFunctions["activateProgram"].GetReturnInfo();
        MinInitGasOutput = precompileFunctions["minInitGas"].GetReturnInfo();
        ProgramInitGasOutput = precompileFunctions["programInitGas"].GetReturnInfo();
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        return methodId switch
        {
            _ when methodId == ActivateProgramId => ActivateProgram(context, inputDataSpan),
            _ when methodId == CodeHashKeepaliveId => CodeHashKeepalive(context, inputDataSpan),
            _ when methodId == StylusVersionId => StylusVersion(context, inputDataSpan),
            _ when methodId == InkPriceId => InkPrice(context, inputDataSpan),
            _ when methodId == MaxStackDepthId => MaxStackDepth(context, inputDataSpan),
            _ when methodId == FreePagesId => FreePages(context, inputDataSpan),
            _ when methodId == PageGasId => PageGas(context, inputDataSpan),
            _ when methodId == PageRampId => PageRamp(context, inputDataSpan),
            _ when methodId == PageLimitId => PageLimit(context, inputDataSpan),
            _ when methodId == MinInitGasId => MinInitGas(context, inputDataSpan),
            _ when methodId == InitCostScalarId => InitCostScalar(context, inputDataSpan),
            _ when methodId == ExpiryDaysId => ExpiryDays(context, inputDataSpan),
            _ when methodId == KeepaliveDaysId => KeepaliveDays(context, inputDataSpan),
            _ when methodId == BlockCacheSizeId => BlockCacheSize(context, inputDataSpan),
            _ when methodId == CodeHashVersionId => CodeHashVersion(context, inputDataSpan),
            _ when methodId == CodeHashAsmSizeId => CodeHashAsmSize(context, inputDataSpan),
            _ when methodId == ProgramVersionId => ProgramVersion(context, inputDataSpan),
            _ when methodId == ProgramInitGasId => ProgramInitGas(context, inputDataSpan),
            _ when methodId == ProgramMemoryFootprintId => ProgramMemoryFootprint(context, inputDataSpan),
            _ when methodId == ProgramTimeLeftId => ProgramTimeLeft(context, inputDataSpan),
            _ => throw new ArgumentException($"Unknown precompile method ID: {methodId}")
        };
    }

    private static byte[] ActivateProgram(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            precompileFunctions["activateProgram"].GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ArbWasmActivateProgramResult result = ArbWasm.ActivateProgram(context, program);
        return AbiEncoder.Instance.Encode(ActivateProgramOutput, result.Version, result.DataFee);
    }

    private static byte[] CodeHashKeepalive(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            precompileFunctions["codehashKeepalive"].GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] codeHashBytes = (byte[])decoded[0];
        Hash256 codeHash = new(codeHashBytes);

        ArbWasm.CodeHashKeepAlive(context, codeHash);
        return [];
    }

    private static byte[] StylusVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.StylusVersion(context)).ToBigEndian();

    private static byte[] InkPrice(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.InkPrice(context)).ToBigEndian();

    private static byte[] MaxStackDepth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.MaxStackDepth(context)).ToBigEndian();

    private static byte[] FreePages(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.FreePages(context)).ToBigEndian();

    private static byte[] PageGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.PageGas(context)).ToBigEndian();

    private static byte[] PageRamp(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.PageRamp(context)).ToBigEndian();

    private static byte[] PageLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.PageLimit(context)).ToBigEndian();

    private static byte[] MinInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        (ulong gas, ulong cached) = ArbWasm.MinInitGas(context);
        return AbiEncoder.Instance.Encode(MinInitGasOutput, gas, cached);
    }

    private static byte[] InitCostScalar(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.InitCostScalar(context)).ToBigEndian();

    private static byte[] ExpiryDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.ExpiryDays(context)).ToBigEndian();

    private static byte[] KeepaliveDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.KeepaliveDays(context)).ToBigEndian();

    private static byte[] BlockCacheSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbWasm.BlockCacheSize(context)).ToBigEndian();

    private static byte[] CodeHashVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            precompileFunctions["codehashVersion"].GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] codeHashBytes = (byte[])decoded[0];
        Hash256 codeHash = new(codeHashBytes);

        ushort version = ArbWasm.CodeHashVersion(context, codeHash);
        return new UInt256(version).ToBigEndian();
    }

    private static byte[] CodeHashAsmSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            precompileFunctions["codehashAsmSize"].GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] codeHashBytes = (byte[])decoded[0];
        Hash256 codeHash = new(codeHashBytes);

        uint size = ArbWasm.CodeHashAsmSize(context, codeHash);
        return new UInt256(size).ToBigEndian();
    }

    private static byte[] ProgramVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            precompileFunctions["programVersion"].GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ushort version = ArbWasm.ProgramVersion(context, program);
        return new UInt256(version).ToBigEndian();
    }

    private static byte[] ProgramInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            precompileFunctions["programInitGas"].GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        (ulong gas, ulong gasWhenCached) = ArbWasm.ProgramInitGas(context, program);
        return AbiEncoder.Instance.Encode(ProgramInitGasOutput, gas, gasWhenCached);
    }

    private static byte[] ProgramMemoryFootprint(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            precompileFunctions["programMemoryFootprint"].GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ushort footprint = ArbWasm.ProgramMemoryFootprint(context, program);
        return new UInt256(footprint).ToBigEndian();
    }

    private static byte[] ProgramTimeLeft(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            precompileFunctions["programTimeLeft"].GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ulong secs = ArbWasm.ProgramTimeLeft(context, program);
        return new UInt256(secs).ToBigEndian();
    }
}
