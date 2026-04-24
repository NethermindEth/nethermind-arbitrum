// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbOwnerParser : IArbitrumPrecompile<ArbOwnerParser>
{
    public static readonly ArbOwnerParser Instance = new();

    public bool IsOwner => true;

    public static Address Address { get; } = ArbOwner.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbOwner.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint AddChainOwnerId = Solgen.ArbOwner.Methods.AddChainOwner;
    private const uint RemoveChainOwnerId = Solgen.ArbOwner.Methods.RemoveChainOwner;
    private const uint IsChainOwnerId = Solgen.ArbOwner.Methods.IsChainOwner;
    private const uint GetAllChainOwnersId = Solgen.ArbOwner.Methods.GetAllChainOwners;
    private const uint SetNativeTokenManagementFromId = Solgen.ArbOwner.Methods.SetNativeTokenManagementFrom;
    private const uint AddNativeTokenOwnerId = Solgen.ArbOwner.Methods.AddNativeTokenOwner;
    private const uint RemoveNativeTokenOwnerId = Solgen.ArbOwner.Methods.RemoveNativeTokenOwner;
    private const uint IsNativeTokenOwnerId = Solgen.ArbOwner.Methods.IsNativeTokenOwner;
    private const uint GetAllNativeTokenOwnersId = Solgen.ArbOwner.Methods.GetAllNativeTokenOwners;
    private const uint SetL1BaseFeeEstimateInertiaId = Solgen.ArbOwner.Methods.SetL1BaseFeeEstimateInertia;
    private const uint SetL2BaseFeeId = Solgen.ArbOwner.Methods.SetL2BaseFee;
    private const uint SetMinimumL2BaseFeeId = Solgen.ArbOwner.Methods.SetMinimumL2BaseFee;
    private const uint SetSpeedLimitId = Solgen.ArbOwner.Methods.SetSpeedLimit;
    private const uint SetMaxTxGasLimitId = Solgen.ArbOwner.Methods.SetMaxTxGasLimit;
    private const uint SetMaxBlockGasLimitId = Solgen.ArbOwner.Methods.SetMaxBlockGasLimit;
    private const uint SetL2GasPricingInertiaId = Solgen.ArbOwner.Methods.SetL2GasPricingInertia;
    private const uint SetL2GasBacklogToleranceId = Solgen.ArbOwner.Methods.SetL2GasBacklogTolerance;
    private const uint GetNetworkFeeAccountId = Solgen.ArbOwner.Methods.GetNetworkFeeAccount;
    private const uint GetInfraFeeAccountId = Solgen.ArbOwner.Methods.GetInfraFeeAccount;
    private const uint SetNetworkFeeAccountId = Solgen.ArbOwner.Methods.SetNetworkFeeAccount;
    private const uint SetInfraFeeAccountId = Solgen.ArbOwner.Methods.SetInfraFeeAccount;
    private const uint ScheduleArbOSUpgradeId = Solgen.ArbOwner.Methods.ScheduleArbOSUpgrade;
    private const uint SetL1PricingEquilibrationUnitsId = Solgen.ArbOwner.Methods.SetL1PricingEquilibrationUnits;
    private const uint SetL1PricingInertiaId = Solgen.ArbOwner.Methods.SetL1PricingInertia;
    private const uint SetL1PricingRewardRecipientId = Solgen.ArbOwner.Methods.SetL1PricingRewardRecipient;
    private const uint SetL1PricingRewardRateId = Solgen.ArbOwner.Methods.SetL1PricingRewardRate;
    private const uint SetL1PricePerUnitId = Solgen.ArbOwner.Methods.SetL1PricePerUnit;
    private const uint SetPerBatchGasChargeId = Solgen.ArbOwner.Methods.SetPerBatchGasCharge;
    private const uint SetBrotliCompressionLevelId = Solgen.ArbOwner.Methods.SetBrotliCompressionLevel;
    private const uint SetAmortizedCostCapBipsId = Solgen.ArbOwner.Methods.SetAmortizedCostCapBips;
    private const uint ReleaseL1PricerSurplusFundsId = Solgen.ArbOwner.Methods.ReleaseL1PricerSurplusFunds;
    private const uint SetInkPriceId = Solgen.ArbOwner.Methods.SetInkPrice;
    private const uint SetWasmMaxStackDepthId = Solgen.ArbOwner.Methods.SetWasmMaxStackDepth;
    private const uint SetWasmFreePagesId = Solgen.ArbOwner.Methods.SetWasmFreePages;
    private const uint SetWasmPageGasId = Solgen.ArbOwner.Methods.SetWasmPageGas;
    private const uint SetWasmPageLimitId = Solgen.ArbOwner.Methods.SetWasmPageLimit;
    private const uint SetWasmMaxSizeId = Solgen.ArbOwner.Methods.SetWasmMaxSize;
    private const uint SetWasmMinInitGasId = Solgen.ArbOwner.Methods.SetWasmMinInitGas;
    private const uint SetWasmInitCostScalarId = Solgen.ArbOwner.Methods.SetWasmInitCostScalar;
    private const uint SetWasmExpiryDaysId = Solgen.ArbOwner.Methods.SetWasmExpiryDays;
    private const uint SetWasmKeepaliveDaysId = Solgen.ArbOwner.Methods.SetWasmKeepaliveDays;
    private const uint SetWasmBlockCacheSizeId = Solgen.ArbOwner.Methods.SetWasmBlockCacheSize;
    private const uint AddWasmCacheManagerId = Solgen.ArbOwner.Methods.AddWasmCacheManager;
    private const uint RemoveWasmCacheManagerId = Solgen.ArbOwner.Methods.RemoveWasmCacheManager;
    private const uint SetChainConfigId = Solgen.ArbOwner.Methods.SetChainConfig;
    private const uint SetCalldataPriceIncreaseId = Solgen.ArbOwner.Methods.SetCalldataPriceIncrease;
    private const uint SetParentGasFloorPerTokenId = Solgen.ArbOwner.Methods.SetParentGasFloorPerToken;
    private const uint SetGasBacklogId = Solgen.ArbOwner.Methods.SetGasBacklog;
    private const uint SetGasPricingConstraintsId = Solgen.ArbOwner.Methods.SetGasPricingConstraints;

    static ArbOwnerParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { AddChainOwnerId, AddChainOwner },
            { RemoveChainOwnerId, RemoveChainOwner },
            { IsChainOwnerId, IsChainOwner },
            { GetAllChainOwnersId, GetAllChainOwners },
            { SetNativeTokenManagementFromId, SetNativeTokenManagementFrom },
            { AddNativeTokenOwnerId, AddNativeTokenOwner },
            { RemoveNativeTokenOwnerId, RemoveNativeTokenOwner },
            { IsNativeTokenOwnerId, IsNativeTokenOwner },
            { GetAllNativeTokenOwnersId, GetAllNativeTokenOwners },
            { SetL1BaseFeeEstimateInertiaId, SetL1BaseFeeEstimateInertia },
            { SetL2BaseFeeId, SetL2BaseFee },
            { SetMinimumL2BaseFeeId, SetMinimumL2BaseFee },
            { SetSpeedLimitId, SetSpeedLimit },
            { SetMaxTxGasLimitId, SetMaxTxGasLimit },
            { SetMaxBlockGasLimitId, SetMaxBlockGasLimit },
            { SetL2GasPricingInertiaId, SetL2GasPricingInertia },
            { SetL2GasBacklogToleranceId, SetL2GasBacklogTolerance },
            { GetNetworkFeeAccountId, GetNetworkFeeAccount },
            { GetInfraFeeAccountId, GetInfraFeeAccount },
            { SetNetworkFeeAccountId, SetNetworkFeeAccount },
            { SetInfraFeeAccountId, SetInfraFeeAccount },
            { ScheduleArbOSUpgradeId, ScheduleArbOSUpgrade },
            { SetL1PricingEquilibrationUnitsId, SetL1PricingEquilibrationUnits },
            { SetL1PricingInertiaId, SetL1PricingInertia },
            { SetL1PricingRewardRecipientId, SetL1PricingRewardRecipient },
            { SetL1PricingRewardRateId, SetL1PricingRewardRate },
            { SetL1PricePerUnitId, SetL1PricePerUnit },
            { SetPerBatchGasChargeId, SetPerBatchGasCharge },
            { SetAmortizedCostCapBipsId, SetAmortizedCostCapBips },
            { SetBrotliCompressionLevelId, SetBrotliCompressionLevel },
            { ReleaseL1PricerSurplusFundsId, ReleaseL1PricerSurplusFunds },
            { SetInkPriceId, SetInkPrice },
            { SetWasmMaxStackDepthId, SetWasmMaxStackDepth },
            { SetWasmFreePagesId, SetWasmFreePages },
            { SetWasmPageGasId, SetWasmPageGas },
            { SetWasmPageLimitId, SetWasmPageLimit },
            { SetWasmMinInitGasId, SetWasmMinInitGas },
            { SetWasmInitCostScalarId, SetWasmInitCostScalar },
            { SetWasmExpiryDaysId, SetWasmExpiryDays },
            { SetWasmKeepaliveDaysId, SetWasmKeepaliveDays },
            { SetWasmBlockCacheSizeId, SetWasmBlockCacheSize },
            { SetWasmMaxSizeId, SetWasmMaxSize },
            { AddWasmCacheManagerId, AddWasmCacheManager },
            { RemoveWasmCacheManagerId, RemoveWasmCacheManager },
            { SetChainConfigId, SetChainConfig },
            { SetCalldataPriceIncreaseId, SetCalldataPriceIncrease },
            { SetParentGasFloorPerTokenId, SetParentGasFloorPerToken },
            { SetGasBacklogId, SetGasBacklog },
            { SetGasPricingConstraintsId, SetGasPricingConstraints }

        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[GetInfraFeeAccountId].ArbOSVersion = ArbosVersion.Five;
        PrecompileFunctionDescription[SetInfraFeeAccountId].ArbOSVersion = ArbosVersion.Five;
        PrecompileFunctionDescription[ReleaseL1PricerSurplusFundsId].ArbOSVersion = ArbosVersion.Ten;
        PrecompileFunctionDescription[SetChainConfigId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctionDescription[SetBrotliCompressionLevelId].ArbOSVersion = ArbosVersion.Twenty;

        // Stylus methods
        PrecompileFunctionDescription[SetInkPriceId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmMaxStackDepthId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmFreePagesId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmPageGasId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmPageLimitId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmMinInitGasId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmInitCostScalarId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmExpiryDaysId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmKeepaliveDaysId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[SetWasmBlockCacheSizeId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[AddWasmCacheManagerId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[RemoveWasmCacheManagerId].ArbOSVersion = ArbosVersion.Stylus;

        PrecompileFunctionDescription[SetCalldataPriceIncreaseId].ArbOSVersion = ArbosVersion.Forty;
        PrecompileFunctionDescription[SetWasmMaxSizeId].ArbOSVersion = ArbosVersion.Forty;
        PrecompileFunctionDescription[SetNativeTokenManagementFromId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[AddNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[RemoveNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[IsNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[GetAllNativeTokenOwnersId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[SetMaxBlockGasLimitId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[SetParentGasFloorPerTokenId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[SetGasBacklogId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[SetGasPricingConstraintsId].ArbOSVersion = ArbosVersion.Fifty;
    }

    private static byte[] AddChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[AddChainOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.AddChainOwner(context, account);
        return [];
    }

    private static byte[] RemoveChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[RemoveChainOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.RemoveChainOwner(context, account);
        return [];
    }

    private static byte[] IsChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[IsChainOwnerId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        bool isOwner = ArbOwner.IsChainOwner(context, account);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            isOwner
        );
    }

    private static byte[] GetAllChainOwners(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] allChainOwners = ArbOwner.GetAllChainOwners(context);

        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetAllChainOwnersId].AbiFunctionDescription;

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [allChainOwners]
        );
    }

    private static byte[] SetNativeTokenManagementFrom(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetNativeTokenManagementFromId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong timestamp = (ulong)decoded[0];
        ArbOwner.SetNativeTokenManagementFrom(context, timestamp);
        return [];
    }

    private static byte[] AddNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[AddNativeTokenOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.AddNativeTokenOwner(context, account);
        return [];
    }

    private static byte[] RemoveNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[RemoveNativeTokenOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.RemoveNativeTokenOwner(context, account);
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

        Address account = (Address)decoded[0];
        bool isOwner = ArbOwner.IsNativeTokenOwner(context, account);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            isOwner
        );
    }

    private static byte[] GetAllNativeTokenOwners(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] allNativeTokenOwners = ArbOwner.GetAllNativeTokenOwners(context);

        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetAllNativeTokenOwnersId].AbiFunctionDescription;

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [allNativeTokenOwners]
        );
    }

    private static byte[] SetL1BaseFeeEstimateInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL1BaseFeeEstimateInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong inertia = (ulong)decoded[0];
        ArbOwner.SetL1BaseFeeEstimateInertia(context, inertia);
        return [];
    }
    private static byte[] SetL2BaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL2BaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 l2BaseFee = (UInt256)decoded[0];
        ArbOwner.SetL2BaseFee(context, l2BaseFee);
        return [];
    }

    private static byte[] SetMinimumL2BaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetMinimumL2BaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 priceInWei = (UInt256)decoded[0];
        ArbOwner.SetMinimumL2BaseFee(context, priceInWei);
        return [];
    }

    private static byte[] SetSpeedLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetSpeedLimitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong limit = (ulong)decoded[0];
        ArbOwner.SetSpeedLimit(context, limit);
        return [];
    }

    private static byte[] SetMaxTxGasLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetMaxTxGasLimitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong limit = (ulong)decoded[0];
        ArbOwner.SetMaxTxGasLimit(context, limit);
        return [];
    }

    private static byte[] SetMaxBlockGasLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetMaxBlockGasLimitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong limit = (ulong)decoded[0];
        ArbOwner.SetMaxBlockGasLimit(context, limit);
        return [];
    }

    private static byte[] SetL2GasPricingInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL2GasPricingInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong inertia = (ulong)decoded[0];
        ArbOwner.SetL2GasPricingInertia(context, inertia);
        return [];
    }

    private static byte[] SetL2GasBacklogTolerance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL2GasBacklogToleranceId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong backlogTolerance = (ulong)decoded[0];
        ArbOwner.SetL2GasBacklogTolerance(context, backlogTolerance);
        return [];
    }

    private static byte[] GetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address networkFeeAccount = ArbOwner.GetNetworkFeeAccount(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetNetworkFeeAccountId].AbiFunctionDescription.GetReturnInfo().Signature,
            networkFeeAccount
        );
    }

    private static byte[] GetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address infraFeeAccount = ArbOwner.GetInfraFeeAccount(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetInfraFeeAccountId].AbiFunctionDescription.GetReturnInfo().Signature,
            infraFeeAccount
        );
    }

    private static byte[] SetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetNetworkFeeAccountId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.SetNetworkFeeAccount(context, account);
        return [];
    }

    private static byte[] SetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetInfraFeeAccountId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.SetInfraFeeAccount(context, account);
        return [];
    }

    private static byte[] ScheduleArbOSUpgrade(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[ScheduleArbOSUpgradeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong version = (ulong)decoded[0];
        ulong timestamp = (ulong)decoded[1];
        ArbOwner.ScheduleArbOSUpgrade(context, version, timestamp);
        return [];
    }

    private static byte[] SetL1PricingEquilibrationUnits(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL1PricingEquilibrationUnitsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 units = (UInt256)decoded[0];
        ArbOwner.SetL1PricingEquilibrationUnits(context, units);
        return [];
    }

    private static byte[] SetL1PricingInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL1PricingInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong inertia = (ulong)decoded[0];
        ArbOwner.SetL1PricingInertia(context, inertia);
        return [];
    }

    private static byte[] SetL1PricingRewardRecipient(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL1PricingRewardRecipientId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address recipient = (Address)decoded[0];
        ArbOwner.SetL1PricingRewardRecipient(context, recipient);
        return [];
    }

    private static byte[] SetL1PricingRewardRate(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL1PricingRewardRateId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong weiPerUnit = (ulong)decoded[0];
        ArbOwner.SetL1PricingRewardRate(context, weiPerUnit);
        return [];
    }

    private static byte[] SetL1PricePerUnit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetL1PricePerUnitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 pricePerUnit = (UInt256)decoded[0];
        ArbOwner.SetL1PricePerUnit(context, pricePerUnit);
        return [];
    }

    private static byte[] SetPerBatchGasCharge(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetPerBatchGasChargeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        long baseCharge = (long)decoded[0];
        ArbOwner.SetPerBatchGasCharge(context, (ulong)baseCharge);
        return [];
    }

    private static byte[] SetAmortizedCostCapBips(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetAmortizedCostCapBipsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong cap = (ulong)decoded[0];
        ArbOwner.SetAmortizedCostCapBips(context, cap);
        return [];
    }

    private static byte[] SetBrotliCompressionLevel(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetBrotliCompressionLevelId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong level = (ulong)decoded[0];
        ArbOwner.SetBrotliCompressionLevel(context, level);
        return [];
    }

    private static byte[] ReleaseL1PricerSurplusFunds(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[ReleaseL1PricerSurplusFundsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 maxWeiToRelease = (UInt256)decoded[0];
        UInt256 weiToRelease = ArbOwner.ReleaseL1PricerSurplusFunds(context, maxWeiToRelease);
        return weiToRelease.ToBigEndian();
    }

    private static byte[] SetInkPrice(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetInkPriceId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        uint inkPrice = (uint)decoded[0];
        ArbOwner.SetInkPrice(context, inkPrice);
        return [];
    }

    private static byte[] SetWasmMaxStackDepth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmMaxStackDepthId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        uint maxStackDepth = (uint)decoded[0];
        ArbOwner.SetWasmMaxStackDepth(context, maxStackDepth);
        return [];
    }

    private static byte[] SetWasmFreePages(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmFreePagesId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort freePages = (ushort)decoded[0];
        ArbOwner.SetWasmFreePages(context, freePages);
        return [];
    }

    private static byte[] SetWasmPageGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmPageGasId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort pageGas = (ushort)decoded[0];
        ArbOwner.SetWasmPageGas(context, pageGas);
        return [];
    }


    private static byte[] SetWasmPageLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmPageLimitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort pageLimit = (ushort)decoded[0];
        ArbOwner.SetWasmPageLimit(context, pageLimit);
        return [];
    }

    private static byte[] SetWasmMinInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmMinInitGasId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );
        ulong gas = (byte)decoded[0];
        ulong cached = (ushort)decoded[1];
        ArbOwner.SetWasmMinInitGas(context, gas, cached);
        return [];
    }

    private static byte[] SetWasmInitCostScalar(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmInitCostScalarId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong percent = (ulong)decoded[0];
        ArbOwner.SetWasmInitCostScalar(context, percent);
        return [];
    }

    private static byte[] SetWasmExpiryDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmExpiryDaysId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort expiryDays = (ushort)decoded[0];
        ArbOwner.SetWasmExpiryDays(context, expiryDays);
        return [];
    }

    private static byte[] SetWasmKeepaliveDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmKeepaliveDaysId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort keepaliveDays = (ushort)decoded[0];
        ArbOwner.SetWasmKeepaliveDays(context, keepaliveDays);
        return [];
    }

    private static byte[] SetWasmBlockCacheSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmBlockCacheSizeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort blockCacheSize = (ushort)decoded[0];
        ArbOwner.SetWasmBlockCacheSize(context, blockCacheSize);
        return [];
    }

    private static byte[] SetWasmMaxSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetWasmMaxSizeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        uint maxWasmSize = (uint)decoded[0];
        ArbOwner.SetWasmMaxSize(context, maxWasmSize);
        return [];
    }

    private static byte[] AddWasmCacheManager(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[AddWasmCacheManagerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address manager = (Address)decoded[0];
        ArbOwner.AddWasmCacheManager(context, manager);
        return [];
    }

    private static byte[] RemoveWasmCacheManager(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[RemoveWasmCacheManagerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address manager = (Address)decoded[0];
        ArbOwner.RemoveWasmCacheManager(context, manager);
        return [];
    }

    private static byte[] SetChainConfig(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetChainConfigId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        string chainConfig = (string)decoded[0];
        ArbOwner.SetChainConfig(context, System.Text.Encoding.UTF8.GetBytes(chainConfig));
        return [];
    }

    private static byte[] SetCalldataPriceIncrease(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetCalldataPriceIncreaseId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        bool enabled = (bool)decoded[0];
        ArbOwner.SetCalldataPriceIncrease(context, enabled);
        return [];
    }

    private static byte[] SetParentGasFloorPerToken(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetParentGasFloorPerTokenId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong floorPerToken = (ulong)decoded[0];
        ArbOwner.SetParentGasFloorPerToken(context, floorPerToken);
        return [];
    }

    private static byte[] SetGasBacklog(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetGasBacklogId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong backlog = (ulong)decoded[0];
        ArbOwner.SetGasBacklog(context, backlog);
        return [];
    }

    private static byte[] SetGasPricingConstraints(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[SetGasPricingConstraintsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong[][] constraintsRaw = (ulong[][])decoded[0];
        ArbOwner.SetGasPricingConstraints(context, constraintsRaw);
        return [];
    }
}
