// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public sealed class ArbWasmParser : IArbitrumPrecompile<ArbWasmParser>
{
    public static readonly ArbWasmParser Instance = new();

    public static Address Address => ArbWasm.Address;

    public static ulong AvailableFromArbosVersion => ArbosVersion.Stylus;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbWasm.Abi);

    private static readonly uint ActivateProgramId = PrecompileHelper.GetMethodId("activateProgram(address)");
    private static readonly uint CodeHashKeepaliveId = PrecompileHelper.GetMethodId("codehashKeepalive(bytes32)");
    private static readonly uint StylusVersionId = PrecompileHelper.GetMethodId("stylusVersion()");
    private static readonly uint InkPriceId = PrecompileHelper.GetMethodId("inkPrice()");
    private static readonly uint MaxStackDepthId = PrecompileHelper.GetMethodId("maxStackDepth()");
    private static readonly uint FreePagesId = PrecompileHelper.GetMethodId("freePages()");
    private static readonly uint PageGasId = PrecompileHelper.GetMethodId("pageGas()");
    private static readonly uint PageRampId = PrecompileHelper.GetMethodId("pageRamp()");
    private static readonly uint PageLimitId = PrecompileHelper.GetMethodId("pageLimit()");
    private static readonly uint MinInitGasId = PrecompileHelper.GetMethodId("minInitGas()");
    private static readonly uint InitCostScalarId = PrecompileHelper.GetMethodId("initCostScalar()");
    private static readonly uint ExpiryDaysId = PrecompileHelper.GetMethodId("expiryDays()");
    private static readonly uint KeepaliveDaysId = PrecompileHelper.GetMethodId("keepaliveDays()");
    private static readonly uint BlockCacheSizeId = PrecompileHelper.GetMethodId("blockCacheSize()");
    private static readonly uint CodeHashVersionId = PrecompileHelper.GetMethodId("codehashVersion(bytes32)");
    private static readonly uint CodeHashAsmSizeId = PrecompileHelper.GetMethodId("codehashAsmSize(bytes32)");
    private static readonly uint ProgramVersionId = PrecompileHelper.GetMethodId("programVersion(address)");
    private static readonly uint ProgramInitGasId = PrecompileHelper.GetMethodId("programInitGas(address)");
    private static readonly uint ProgramMemoryFootprintId = PrecompileHelper.GetMethodId("programMemoryFootprint(address)");
    private static readonly uint ProgramTimeLeftId = PrecompileHelper.GetMethodId("programTimeLeft(address)");

    private static readonly Dictionary<uint, Func<ArbitrumPrecompileExecutionContext, ReadOnlySpan<byte>, byte[]>> _methodIdToParsingFunction
        = new()
    {
        { ActivateProgramId, ActivateProgram },
        { CodeHashKeepaliveId, CodeHashKeepalive },
        { StylusVersionId, StylusVersion },
        { InkPriceId, InkPrice },
        { MaxStackDepthId, MaxStackDepth },
        { FreePagesId, FreePages },
        { PageGasId, PageGas },
        { PageRampId, PageRamp },
        { PageLimitId, PageLimit },
        { MinInitGasId, MinInitGas },
        { InitCostScalarId, InitCostScalar },
        { ExpiryDaysId, ExpiryDays },
        { KeepaliveDaysId, KeepaliveDays },
        { BlockCacheSizeId, BlockCacheSize },
        { CodeHashVersionId, CodeHashVersion },
        { CodeHashAsmSizeId, CodeHashAsmSize },
        { ProgramVersionId, ProgramVersion },
        { ProgramInitGasId, ProgramInitGas },
        { ProgramMemoryFootprintId, ProgramMemoryFootprint },
        { ProgramTimeLeftId, ProgramTimeLeft },
    };

    static ArbWasmParser()
    {
        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (_methodIdToParsingFunction.TryGetValue(methodId, out Func<ArbitrumPrecompileExecutionContext, ReadOnlySpan<byte>, byte[]>? function))
            return function(context, inputDataSpan);

        throw new ArgumentException($"Invalid precompile method ID: {methodId} for ArbWasm precompile");
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        foreach (ArbitrumFunctionDescription functionDescription in PrecompileFunctions.Values)
            functionDescription.ArbOSVersion = AvailableFromArbosVersion;
    }

    private static byte[] ActivateProgram(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctions[ActivateProgramId].AbiFunctionDescription;

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

    private static byte[] CodeHashKeepalive(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[CodeHashKeepaliveId].AbiFunctionDescription.GetCallInfo().Signature,
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

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctions[MinInitGasId].AbiFunctionDescription.GetReturnInfo().Signature,
            gas,
            cached
        );
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
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[CodeHashVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] codeHashBytes = (byte[])decoded[0];
        Hash256 codeHash = new(codeHashBytes);

        ushort version = ArbWasm.CodeHashVersion(context, codeHash);
        return new UInt256(version).ToBigEndian();
    }

    private static byte[] CodeHashAsmSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[CodeHashAsmSizeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        byte[] codeHashBytes = (byte[])decoded[0];
        Hash256 codeHash = new(codeHashBytes);

        uint size = ArbWasm.CodeHashAsmSize(context, codeHash);
        return new UInt256(size).ToBigEndian();
    }

    private static byte[] ProgramVersion(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[ProgramVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ushort version = ArbWasm.ProgramVersion(context, program);
        return new UInt256(version).ToBigEndian();
    }

    private static byte[] ProgramInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctions[ProgramInitGasId].AbiFunctionDescription;

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
            PrecompileFunctions[ProgramMemoryFootprintId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctions[ProgramTimeLeftId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ulong secs = ArbWasm.ProgramTimeLeft(context, program);
        return new UInt256(secs).ToBigEndian();
    }
}
