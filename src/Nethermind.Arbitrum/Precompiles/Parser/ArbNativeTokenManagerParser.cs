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
    private static readonly uint BurnNativeTokenId = PrecompileHelper.GetMethodId("burnNativeToken(uint256)");

    private static readonly uint MintNativeTokenId = PrecompileHelper.GetMethodId("mintNativeToken(uint256)");

    public static Address Address { get; } = ArbNativeTokenManager.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbNativeTokenManager.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    static ArbNativeTokenManagerParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { MintNativeTokenId, MintNativeToken },
            { BurnNativeTokenId, BurnNativeToken }
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static byte[] BurnNativeToken(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[BurnNativeTokenId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 amount = (UInt256)decoded[0];
        ArbNativeTokenManager.BurnNativeToken(context, amount);

        return [];
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[MintNativeTokenId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[BurnNativeTokenId].ArbOSVersion = ArbosVersion.FortyOne;
    }

    private static byte[] MintNativeToken(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[MintNativeTokenId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 amount = (UInt256)decoded[0];
        ArbNativeTokenManager.MintNativeToken(context, amount);

        return [];
    }
}
