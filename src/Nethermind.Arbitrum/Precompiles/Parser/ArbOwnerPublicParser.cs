// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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

    private const uint _isChainOwnerId = Solgen.ArbOwnerPublic.Methods.IsChainOwner;
    private const uint _getAllChainOwnersId = Solgen.ArbOwnerPublic.Methods.GetAllChainOwners;
    private const uint _rectifyChainOwnerId = Solgen.ArbOwnerPublic.Methods.RectifyChainOwner;
    private const uint _isNativeTokenOwnerId = Solgen.ArbOwnerPublic.Methods.IsNativeTokenOwner;
    private const uint _getAllNativeTokenOwnersId = Solgen.ArbOwnerPublic.Methods.GetAllNativeTokenOwners;
    private const uint _getNetworkFeeAccountId = Solgen.ArbOwnerPublic.Methods.GetNetworkFeeAccount;
    private const uint _getInfraFeeAccountId = Solgen.ArbOwnerPublic.Methods.GetInfraFeeAccount;
    private const uint _getBrotliCompressionLevelId = Solgen.ArbOwnerPublic.Methods.GetBrotliCompressionLevel;
    private const uint _getParentGasFloorPerTokenId = Solgen.ArbOwnerPublic.Methods.GetParentGasFloorPerToken;
    private const uint _getNativeTokenManagementFromId = Solgen.ArbOwnerPublic.Methods.GetNativeTokenManagementFrom;
    private const uint _getScheduledUpgradeId = Solgen.ArbOwnerPublic.Methods.GetScheduledUpgrade;
    private const uint _isCalldataPriceIncreaseEnabledId = Solgen.ArbOwnerPublic.Methods.IsCalldataPriceIncreaseEnabled;

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
            { _getNativeTokenManagementFromId, GetNativeTokenManagementFrom },
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
        PrecompileFunctionDescription[_getNativeTokenManagementFromId].ArbOSVersion = ArbosVersion.Fifty;
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

    private static byte[] GetNativeTokenManagementFrom(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ulong timestamp = ArbOwnerPublic.GetNativeTokenManagementFrom(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getNativeTokenManagementFromId].AbiFunctionDescription.GetReturnInfo().Signature,
            timestamp
        );
    }
}
