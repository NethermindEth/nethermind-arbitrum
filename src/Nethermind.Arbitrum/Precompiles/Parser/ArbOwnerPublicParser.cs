using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbOwnerPublicParser : IArbitrumPrecompile<ArbOwnerPublicParser>
{
    public static readonly ArbOwnerPublicParser Instance = new();

    public static Address Address { get; } = ArbOwnerPublic.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbOwnerPublic.Abi);

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private static readonly uint _isChainOwnerId = PrecompileHelper.GetMethodId("isChainOwner(address)");
    private static readonly uint _getAllChainOwnersId = PrecompileHelper.GetMethodId("getAllChainOwners()");
    private static readonly uint _rectifyChainOwnerId = PrecompileHelper.GetMethodId("rectifyChainOwner(address)");
    private static readonly uint _isNativeTokenOwnerId = PrecompileHelper.GetMethodId("isNativeTokenOwner(address)");
    private static readonly uint _getAllNativeTokenOwnersId = PrecompileHelper.GetMethodId("getAllNativeTokenOwners()");
    private static readonly uint _getNetworkFeeAccountId = PrecompileHelper.GetMethodId("getNetworkFeeAccount()");
    private static readonly uint _getInfraFeeAccountId = PrecompileHelper.GetMethodId("getInfraFeeAccount()");
    private static readonly uint _getBrotliCompressionLevelId = PrecompileHelper.GetMethodId("getBrotliCompressionLevel()");
    private static readonly uint _getParentGasFloorPerTokenId = PrecompileHelper.GetMethodId("getParentGasFloorPerToken()");
    private static readonly uint _getScheduledUpgradeId = PrecompileHelper.GetMethodId("getScheduledUpgrade()");
    private static readonly uint _isCalldataPriceIncreaseEnabledId = PrecompileHelper.GetMethodId("isCalldataPriceIncreaseEnabled()");

    static ArbOwnerPublicParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _isChainOwnerId, IsChainOwner },
            { _getAllChainOwnersId, GetAllChainOwners },
            { _rectifyChainOwnerId, RectifyChainOwner },
            { _isNativeTokenOwnerId, IsNativeTokenOwner },
            { _getAllNativeTokenOwnersId, GetAllNativeTokenOwners },
            { _getNetworkFeeAccountId, GetNetworkFeeAccount },
            { _getInfraFeeAccountId, GetInfraFeeAccount },
            { _getBrotliCompressionLevelId, GetBrotliCompressionLevel },
            { _getParentGasFloorPerTokenId, GetParentGasFloorPerToken },
            { _getScheduledUpgradeId, GetScheduledUpgrade },
            { _isCalldataPriceIncreaseEnabledId, IsCalldataPriceIncreaseEnabled }
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[_getInfraFeeAccountId].ArbOSVersion = ArbosVersion.Five;
        PrecompileFunctionDescription[_rectifyChainOwnerId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctionDescription[_getBrotliCompressionLevelId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[_getScheduledUpgradeId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[_isCalldataPriceIncreaseEnabledId].ArbOSVersion = ArbosVersion.Forty;
        PrecompileFunctionDescription[_isNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[_getAllNativeTokenOwnersId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[_getParentGasFloorPerTokenId].ArbOSVersion = ArbosVersion.Fifty;
    }

    private static byte[] IsChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_isChainOwnerId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address addr = (Address)decoded[0];
        bool result = ArbOwnerPublic.IsChainOwner(context, addr);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] GetAllChainOwners(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] owners = ArbOwnerPublic.GetAllChainOwners(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getAllChainOwnersId].AbiFunctionDescription.GetReturnInfo().Signature,
            [owners]
        );
    }

    private static byte[] RectifyChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_rectifyChainOwnerId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address ownerToRectify = (Address)decoded[0];
        ArbOwnerPublic.RectifyChainOwner(context, ownerToRectify);

        return [];
    }

    private static byte[] IsNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_isNativeTokenOwnerId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address addr = (Address)decoded[0];
        bool result = ArbOwnerPublic.IsNativeTokenOwner(context, addr);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] GetAllNativeTokenOwners(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] owners = ArbOwnerPublic.GetAllNativeTokenOwners(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getAllNativeTokenOwnersId].AbiFunctionDescription.GetReturnInfo().Signature,
            [owners]
        );
    }

    private static byte[] GetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address account = ArbOwnerPublic.GetNetworkFeeAccount(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getNetworkFeeAccountId].AbiFunctionDescription.GetReturnInfo().Signature,
            account
        );
    }

    private static byte[] GetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address account = ArbOwnerPublic.GetInfraFeeAccount(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getInfraFeeAccountId].AbiFunctionDescription.GetReturnInfo().Signature,
            account
        );
    }

    private static byte[] GetBrotliCompressionLevel(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ulong level = ArbOwnerPublic.GetBrotliCompressionLevel(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getBrotliCompressionLevelId].AbiFunctionDescription.GetReturnInfo().Signature,
            level
        );
    }

    private static byte[] GetScheduledUpgrade(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        (ulong version, ulong timestamp) = ArbOwnerPublic.GetScheduledUpgrade(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getScheduledUpgradeId].AbiFunctionDescription.GetReturnInfo().Signature,
            version,
            timestamp
        );
    }

    private static byte[] GetParentGasFloorPerToken(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ulong result = ArbOwnerPublic.GetParentGasFloorPerToken(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getParentGasFloorPerTokenId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] IsCalldataPriceIncreaseEnabled(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        bool result = ArbOwnerPublic.IsCalldataPriceIncreaseEnabled(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_isCalldataPriceIncreaseEnabledId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }
}
