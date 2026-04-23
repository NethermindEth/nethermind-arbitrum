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

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; } = AbiMetadata.GetAllFunctionDescriptions(ArbWasm.Abi);
    public static ArbitrumFunctionDescription ActivateProgramDescription => PrecompileFunctionDescription[_activateProgramId];

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint _activateProgramId = Solgen.ArbWasm.Methods.ActivateProgram;
    private const uint _codeHashKeepaliveId = Solgen.ArbWasm.Methods.CodehashKeepalive;
    private const uint _stylusVersionId = Solgen.ArbWasm.Methods.StylusVersion;
    private const uint _inkPriceId = Solgen.ArbWasm.Methods.InkPrice;
    private const uint _maxStackDepthId = Solgen.ArbWasm.Methods.MaxStackDepth;
    private const uint _freePagesId = Solgen.ArbWasm.Methods.FreePages;
    private const uint _pageGasId = Solgen.ArbWasm.Methods.PageGas;
    private const uint _pageRampId = Solgen.ArbWasm.Methods.PageRamp;
    private const uint _pageLimitId = Solgen.ArbWasm.Methods.PageLimit;
    private const uint _minInitGasId = Solgen.ArbWasm.Methods.MinInitGas;
    private const uint _initCostScalarId = Solgen.ArbWasm.Methods.InitCostScalar;
    private const uint _expiryDaysId = Solgen.ArbWasm.Methods.ExpiryDays;
    private const uint _keepaliveDaysId = Solgen.ArbWasm.Methods.KeepaliveDays;
    private const uint _blockCacheSizeId = Solgen.ArbWasm.Methods.BlockCacheSize;
    private const uint _codeHashVersionId = Solgen.ArbWasm.Methods.CodehashVersion;
    private const uint _codeHashAsmSizeId = Solgen.ArbWasm.Methods.CodehashAsmSize;
    private const uint _programVersionId = Solgen.ArbWasm.Methods.ProgramVersion;
    private const uint _programInitGasId = Solgen.ArbWasm.Methods.ProgramInitGas;
    private const uint _programMemoryFootprintId = Solgen.ArbWasm.Methods.ProgramMemoryFootprint;
    private const uint _programTimeLeftId = Solgen.ArbWasm.Methods.ProgramTimeLeft;

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

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        foreach (ArbitrumFunctionDescription functionDescription in PrecompileFunctionDescription.Values)
            functionDescription.ArbOSVersion = AvailableFromArbosVersion;
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
            PrecompileFunctionDescription[_minInitGasId].AbiFunctionDescription.GetReturnInfo().Signature,
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
            PrecompileFunctionDescription[_codeHashVersionId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_codeHashAsmSizeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_programVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address program = (Address)decoded[0];
        ushort version = ArbWasm.ProgramVersion(context, program);
        return new UInt256(version).ToBigEndian();
    }

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
}
