using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbNativeTokenManagerParser : IArbitrumPrecompile<ArbNativeTokenManagerParser>
{
    public static readonly ArbNativeTokenManagerParser Instance = new();

    public static Address Address { get; } = ArbNativeTokenManager.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbNativeTokenManager.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _mintNativeTokenId = PrecompileHelper.GetMethodId("mintNativeToken(uint256)");
    private static readonly uint _burnNativeTokenId = PrecompileHelper.GetMethodId("burnNativeToken(uint256)");

    static ArbNativeTokenManagerParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _mintNativeTokenId, MintNativeToken },
            { _burnNativeTokenId, BurnNativeToken }
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[_mintNativeTokenId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[_burnNativeTokenId].ArbOSVersion = ArbosVersion.FortyOne;
    }

    private static byte[] MintNativeToken(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_mintNativeTokenId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 amount = (UInt256)decoded[0];
        ArbNativeTokenManager.MintNativeToken(context, amount);

        return [];
    }

    private static byte[] BurnNativeToken(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_burnNativeTokenId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 amount = (UInt256)decoded[0];
        ArbNativeTokenManager.BurnNativeToken(context, amount);

        return [];
    }
}
