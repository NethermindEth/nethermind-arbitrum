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
        = Solgen.ArbOwnerPublic.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint IsChainOwnerId = Solgen.ArbOwnerPublic.Methods.IsChainOwner;
    private const uint GetAllChainOwnersId = Solgen.ArbOwnerPublic.Methods.GetAllChainOwners;
    private const uint RectifyChainOwnerId = Solgen.ArbOwnerPublic.Methods.RectifyChainOwner;
    private const uint IsNativeTokenOwnerId = Solgen.ArbOwnerPublic.Methods.IsNativeTokenOwner;
    private const uint GetAllNativeTokenOwnersId = Solgen.ArbOwnerPublic.Methods.GetAllNativeTokenOwners;
    private const uint GetNetworkFeeAccountId = Solgen.ArbOwnerPublic.Methods.GetNetworkFeeAccount;
    private const uint GetInfraFeeAccountId = Solgen.ArbOwnerPublic.Methods.GetInfraFeeAccount;
    private const uint GetBrotliCompressionLevelId = Solgen.ArbOwnerPublic.Methods.GetBrotliCompressionLevel;
    private const uint GetParentGasFloorPerTokenId = Solgen.ArbOwnerPublic.Methods.GetParentGasFloorPerToken;
    private const uint GetNativeTokenManagementFromId = Solgen.ArbOwnerPublic.Methods.GetNativeTokenManagementFrom;
    private const uint GetScheduledUpgradeId = Solgen.ArbOwnerPublic.Methods.GetScheduledUpgrade;
    private const uint IsCalldataPriceIncreaseEnabledId = Solgen.ArbOwnerPublic.Methods.IsCalldataPriceIncreaseEnabled;

    static ArbOwnerPublicParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { IsChainOwnerId, IsChainOwner },
            { GetAllChainOwnersId, GetAllChainOwners },
            { RectifyChainOwnerId, RectifyChainOwner },
            { IsNativeTokenOwnerId, IsNativeTokenOwner },
            { GetAllNativeTokenOwnersId, GetAllNativeTokenOwners },
            { GetNetworkFeeAccountId, GetNetworkFeeAccount },
            { GetInfraFeeAccountId, GetInfraFeeAccount },
            { GetBrotliCompressionLevelId, GetBrotliCompressionLevel },
            { GetParentGasFloorPerTokenId, GetParentGasFloorPerToken },
            { GetNativeTokenManagementFromId, GetNativeTokenManagementFrom },
            { GetScheduledUpgradeId, GetScheduledUpgrade },
            { IsCalldataPriceIncreaseEnabledId, IsCalldataPriceIncreaseEnabled }
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[GetInfraFeeAccountId].ArbOSVersion = ArbosVersion.Five;
        PrecompileFunctionDescription[RectifyChainOwnerId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctionDescription[GetBrotliCompressionLevelId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[GetScheduledUpgradeId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[IsCalldataPriceIncreaseEnabledId].ArbOSVersion = ArbosVersion.Forty;
        PrecompileFunctionDescription[IsNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[GetAllNativeTokenOwnersId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[GetParentGasFloorPerTokenId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[GetNativeTokenManagementFromId].ArbOSVersion = ArbosVersion.Fifty;
    }

    private static byte[] IsChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[IsChainOwnerId].AbiFunctionDescription;

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
            PrecompileFunctionDescription[GetAllChainOwnersId].AbiFunctionDescription.GetReturnInfo().Signature,
            [owners]
        );
    }

    private static byte[] RectifyChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[RectifyChainOwnerId].AbiFunctionDescription;

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
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[IsNativeTokenOwnerId].AbiFunctionDescription;

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
            PrecompileFunctionDescription[GetAllNativeTokenOwnersId].AbiFunctionDescription.GetReturnInfo().Signature,
            [owners]
        );
    }

    private static byte[] GetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address account = ArbOwnerPublic.GetNetworkFeeAccount(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetNetworkFeeAccountId].AbiFunctionDescription.GetReturnInfo().Signature,
            account
        );
    }

    private static byte[] GetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address account = ArbOwnerPublic.GetInfraFeeAccount(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetInfraFeeAccountId].AbiFunctionDescription.GetReturnInfo().Signature,
            account
        );
    }

    private static byte[] GetBrotliCompressionLevel(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ulong level = ArbOwnerPublic.GetBrotliCompressionLevel(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetBrotliCompressionLevelId].AbiFunctionDescription.GetReturnInfo().Signature,
            level
        );
    }

    private static byte[] GetScheduledUpgrade(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        (ulong version, ulong timestamp) = ArbOwnerPublic.GetScheduledUpgrade(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetScheduledUpgradeId].AbiFunctionDescription.GetReturnInfo().Signature,
            version,
            timestamp
        );
    }

    private static byte[] GetParentGasFloorPerToken(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ulong result = ArbOwnerPublic.GetParentGasFloorPerToken(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetParentGasFloorPerTokenId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] IsCalldataPriceIncreaseEnabled(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        bool result = ArbOwnerPublic.IsCalldataPriceIncreaseEnabled(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[IsCalldataPriceIncreaseEnabledId].AbiFunctionDescription.GetReturnInfo().Signature,
            result
        );
    }

    private static byte[] GetNativeTokenManagementFrom(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ulong timestamp = ArbOwnerPublic.GetNativeTokenManagementFrom(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetNativeTokenManagementFromId].AbiFunctionDescription.GetReturnInfo().Signature,
            timestamp
        );
    }
}
