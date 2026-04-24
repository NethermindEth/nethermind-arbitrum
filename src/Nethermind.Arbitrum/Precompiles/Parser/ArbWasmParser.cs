// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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

    public static Address Address => ArbWasm.Address;

    public static ulong AvailableFromArbosVersion => ArbosVersion.Stylus;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbWasm.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());
    public static ArbitrumFunctionDescription ActivateProgramDescription => PrecompileFunctionDescription[ActivateProgramId];

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint ActivateProgramId = Solgen.ArbWasm.Methods.ActivateProgram;
    private const uint CodeHashKeepaliveId = Solgen.ArbWasm.Methods.CodehashKeepalive;
    private const uint StylusVersionId = Solgen.ArbWasm.Methods.StylusVersion;
    private const uint InkPriceId = Solgen.ArbWasm.Methods.InkPrice;
    private const uint MaxStackDepthId = Solgen.ArbWasm.Methods.MaxStackDepth;
    private const uint FreePagesId = Solgen.ArbWasm.Methods.FreePages;
    private const uint PageGasId = Solgen.ArbWasm.Methods.PageGas;
    private const uint PageRampId = Solgen.ArbWasm.Methods.PageRamp;
    private const uint PageLimitId = Solgen.ArbWasm.Methods.PageLimit;
    private const uint MinInitGasId = Solgen.ArbWasm.Methods.MinInitGas;
    private const uint InitCostScalarId = Solgen.ArbWasm.Methods.InitCostScalar;
    private const uint ExpiryDaysId = Solgen.ArbWasm.Methods.ExpiryDays;
    private const uint KeepaliveDaysId = Solgen.ArbWasm.Methods.KeepaliveDays;
    private const uint BlockCacheSizeId = Solgen.ArbWasm.Methods.BlockCacheSize;
    private const uint CodeHashVersionId = Solgen.ArbWasm.Methods.CodehashVersion;
    private const uint CodeHashAsmSizeId = Solgen.ArbWasm.Methods.CodehashAsmSize;
    private const uint ProgramVersionId = Solgen.ArbWasm.Methods.ProgramVersion;
    private const uint ProgramInitGasId = Solgen.ArbWasm.Methods.ProgramInitGas;
    private const uint ProgramMemoryFootprintId = Solgen.ArbWasm.Methods.ProgramMemoryFootprint;
    private const uint ProgramTimeLeftId = Solgen.ArbWasm.Methods.ProgramTimeLeft;

    static ArbWasmParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
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
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        foreach (ArbitrumFunctionDescription functionDescription in PrecompileFunctionDescription.Values)
            functionDescription.ArbOSVersion = AvailableFromArbosVersion;
    }

    private static byte[] ActivateProgram(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[ActivateProgramId].AbiFunctionDescription;

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
            PrecompileFunctionDescription[CodeHashKeepaliveId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[MinInitGasId].AbiFunctionDescription.GetReturnInfo().Signature,
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
            PrecompileFunctionDescription[CodeHashVersionId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[CodeHashAsmSizeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[ProgramVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ushort version = ArbWasm.ProgramVersion(context, program);
        return new UInt256(version).ToBigEndian();
    }

    private static byte[] ProgramInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[ProgramInitGasId].AbiFunctionDescription;

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
            PrecompileFunctionDescription[ProgramMemoryFootprintId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[ProgramTimeLeftId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ulong secs = ArbWasm.ProgramTimeLeft(context, program);
        return new UInt256(secs).ToBigEndian();
    }
}
