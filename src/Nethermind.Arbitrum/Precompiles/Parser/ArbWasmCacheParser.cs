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
        = AbiMetadata.GetAllFunctionDescriptions(ArbWasmCache.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _isCacheManagerId = PrecompileHelper.GetMethodId("isCacheManager(address)");
    private static readonly uint _allCacheManagersId = PrecompileHelper.GetMethodId("allCacheManagers()");
    private static readonly uint _cacheCodehashId = PrecompileHelper.GetMethodId("cacheCodehash(bytes32)");
    private static readonly uint _cacheProgramId = PrecompileHelper.GetMethodId("cacheProgram(address)");
    private static readonly uint _evictProgramId = PrecompileHelper.GetMethodId("evictCodehash(bytes32)");
    private static readonly uint _codehashIsCachedId = PrecompileHelper.GetMethodId("codehashIsCached(bytes32)");

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
