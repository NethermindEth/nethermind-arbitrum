using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbInfoParser : IArbitrumPrecompile<ArbInfoParser>
{
    public static readonly ArbInfoParser Instance = new();

    public static Address Address { get; } = ArbInfo.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbInfo.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _getBalanceId = PrecompileHelper.GetMethodId("getBalance(address)");
    private static readonly uint _getCodeId = PrecompileHelper.GetMethodId("getCode(address)");

    static ArbInfoParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _getBalanceId, GetBalance },
            { _getCodeId, GetCode },
        }.ToFrozenDictionary();
    }

    private static byte[] GetBalance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getBalanceId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        return ArbInfo.GetBalance(context, account).ToBigEndian();
    }

    private static byte[] GetCode(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_getCodeId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        byte[] code = ArbInfo.GetCode(context, account);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            code
        );
    }
}
