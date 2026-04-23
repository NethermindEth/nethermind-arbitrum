// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbWasmCacheParser : IArbitrumPrecompile<ArbWasmCacheParser>
{
    public static readonly ArbWasmCacheParser Instance = new();

    public static Address Address { get; } = ArbWasmCache.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbWasmCache.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint _isCacheManagerId = Solgen.ArbWasmCache.Methods.IsCacheManager;
    private const uint _allCacheManagersId = Solgen.ArbWasmCache.Methods.AllCacheManagers;
    private const uint _cacheCodehashId = Solgen.ArbWasmCache.Methods.CacheCodehash;
    private const uint _cacheProgramId = Solgen.ArbWasmCache.Methods.CacheProgram;
    private const uint _evictProgramId = Solgen.ArbWasmCache.Methods.EvictCodehash;
    private const uint _codehashIsCachedId = Solgen.ArbWasmCache.Methods.CodehashIsCached;

    static ArbWasmCacheParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _isCacheManagerId, IsCacheManager },
            { _allCacheManagersId, AllCacheManagers },
            { _cacheCodehashId, CacheCodehash },
            { _cacheProgramId, CacheProgram },
            { _evictProgramId, EvictProgram },
            { _codehashIsCachedId, CodehashIsCached },
        }.ToFrozenDictionary();
    }

    private static byte[] IsCacheManager(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_isCacheManagerId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        bool result = ArbWasmCache.IsCacheManager(context, account);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] AllCacheManagers(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] result = ArbWasmCache.AllCacheManagers(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_allCacheManagersId].AbiFunctionDescription.GetReturnInfo().Signature,
            [result]
        );
    }

    private static byte[] CacheCodehash(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_cacheCodehashId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ValueHash256 codeHash = new((byte[])decoded[0]);
        ArbWasmCache.CacheCodehash(context, codeHash);

        return [];
    }

    private static byte[] CacheProgram(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_cacheProgramId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address address = (Address)decoded[0];
        ArbWasmCache.CacheProgram(context, address);

        return [];
    }

    private static byte[] EvictProgram(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_evictProgramId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ValueHash256 codeHash = new((byte[])decoded[0]);
        ArbWasmCache.EvictProgram(context, codeHash);

        return [];
    }

    private static byte[] CodehashIsCached(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_codehashIsCachedId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ValueHash256 codeHash = new((byte[])decoded[0]);
        bool result = ArbWasmCache.CodehashIsCached(context, codeHash);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            result
        );
    }
}
