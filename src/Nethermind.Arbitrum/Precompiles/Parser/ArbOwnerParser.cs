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

    private const uint _addChainOwnerId = Solgen.ArbOwner.Methods.AddChainOwner;
    private const uint _removeChainOwnerId = Solgen.ArbOwner.Methods.RemoveChainOwner;
    private const uint _isChainOwnerId = Solgen.ArbOwner.Methods.IsChainOwner;
    private const uint _getAllChainOwnersId = Solgen.ArbOwner.Methods.GetAllChainOwners;
    private const uint _setNativeTokenManagementFromId = Solgen.ArbOwner.Methods.SetNativeTokenManagementFrom;
    private const uint _addNativeTokenOwnerId = Solgen.ArbOwner.Methods.AddNativeTokenOwner;
    private const uint _removeNativeTokenOwnerId = Solgen.ArbOwner.Methods.RemoveNativeTokenOwner;
    private const uint _isNativeTokenOwnerId = Solgen.ArbOwner.Methods.IsNativeTokenOwner;
    private const uint _getAllNativeTokenOwnersId = Solgen.ArbOwner.Methods.GetAllNativeTokenOwners;
    private const uint _setL1BaseFeeEstimateInertiaId = Solgen.ArbOwner.Methods.SetL1BaseFeeEstimateInertia;
    private const uint _setL2BaseFeeId = Solgen.ArbOwner.Methods.SetL2BaseFee;
    private const uint _setMinimumL2BaseFeeId = Solgen.ArbOwner.Methods.SetMinimumL2BaseFee;
    private const uint _setSpeedLimitId = Solgen.ArbOwner.Methods.SetSpeedLimit;
    private const uint _setMaxTxGasLimitId = Solgen.ArbOwner.Methods.SetMaxTxGasLimit;
    private const uint _setMaxBlockGasLimitId = Solgen.ArbOwner.Methods.SetMaxBlockGasLimit;
    private const uint _setL2GasPricingInertiaId = Solgen.ArbOwner.Methods.SetL2GasPricingInertia;
    private const uint _setL2GasBacklogToleranceId = Solgen.ArbOwner.Methods.SetL2GasBacklogTolerance;
    private const uint _getNetworkFeeAccountId = Solgen.ArbOwner.Methods.GetNetworkFeeAccount;
    private const uint _getInfraFeeAccountId = Solgen.ArbOwner.Methods.GetInfraFeeAccount;
    private const uint _setNetworkFeeAccountId = Solgen.ArbOwner.Methods.SetNetworkFeeAccount;
    private const uint _setInfraFeeAccountId = Solgen.ArbOwner.Methods.SetInfraFeeAccount;
    private const uint _scheduleArbOSUpgradeId = Solgen.ArbOwner.Methods.ScheduleArbOSUpgrade;
    private const uint _setL1PricingEquilibrationUnitsId = Solgen.ArbOwner.Methods.SetL1PricingEquilibrationUnits;
    private const uint _setL1PricingInertiaId = Solgen.ArbOwner.Methods.SetL1PricingInertia;
    private const uint _setL1PricingRewardRecipientId = Solgen.ArbOwner.Methods.SetL1PricingRewardRecipient;
    private const uint _setL1PricingRewardRateId = Solgen.ArbOwner.Methods.SetL1PricingRewardRate;
    private const uint _setL1PricePerUnitId = Solgen.ArbOwner.Methods.SetL1PricePerUnit;
    private const uint _setPerBatchGasChargeId = Solgen.ArbOwner.Methods.SetPerBatchGasCharge;
    private const uint _setBrotliCompressionLevelId = Solgen.ArbOwner.Methods.SetBrotliCompressionLevel;
    private const uint _setAmortizedCostCapBipsId = Solgen.ArbOwner.Methods.SetAmortizedCostCapBips;
    private const uint _releaseL1PricerSurplusFundsId = Solgen.ArbOwner.Methods.ReleaseL1PricerSurplusFunds;
    private const uint _setInkPriceId = Solgen.ArbOwner.Methods.SetInkPrice;
    private const uint _setWasmMaxStackDepthId = Solgen.ArbOwner.Methods.SetWasmMaxStackDepth;
    private const uint _setWasmFreePagesId = Solgen.ArbOwner.Methods.SetWasmFreePages;
    private const uint _setWasmPageGasId = Solgen.ArbOwner.Methods.SetWasmPageGas;
    private const uint _setWasmPageLimitId = Solgen.ArbOwner.Methods.SetWasmPageLimit;
    private const uint _setWasmMaxSizeId = Solgen.ArbOwner.Methods.SetWasmMaxSize;
    private const uint _setWasmMinInitGasId = Solgen.ArbOwner.Methods.SetWasmMinInitGas;
    private const uint _setWasmInitCostScalarId = Solgen.ArbOwner.Methods.SetWasmInitCostScalar;
    private const uint _setWasmExpiryDaysId = Solgen.ArbOwner.Methods.SetWasmExpiryDays;
    private const uint _setWasmKeepaliveDaysId = Solgen.ArbOwner.Methods.SetWasmKeepaliveDays;
    private const uint _setWasmBlockCacheSizeId = Solgen.ArbOwner.Methods.SetWasmBlockCacheSize;
    private const uint _addWasmCacheManagerId = Solgen.ArbOwner.Methods.AddWasmCacheManager;
    private const uint _removeWasmCacheManagerId = Solgen.ArbOwner.Methods.RemoveWasmCacheManager;
    private const uint _setChainConfigId = Solgen.ArbOwner.Methods.SetChainConfig;
    private const uint _setCalldataPriceIncreaseId = Solgen.ArbOwner.Methods.SetCalldataPriceIncrease;
    private const uint _setParentGasFloorPerTokenId = Solgen.ArbOwner.Methods.SetParentGasFloorPerToken;
    private const uint _setGasBacklogId = Solgen.ArbOwner.Methods.SetGasBacklog;
    private const uint _setGasPricingConstraintsId = Solgen.ArbOwner.Methods.SetGasPricingConstraints;

    static ArbOwnerParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { _addChainOwnerId, AddChainOwner },
            { _removeChainOwnerId, RemoveChainOwner },
            { _isChainOwnerId, IsChainOwner },
            { _getAllChainOwnersId, GetAllChainOwners },
            { _setNativeTokenManagementFromId, SetNativeTokenManagementFrom },
            { _addNativeTokenOwnerId, AddNativeTokenOwner },
            { _removeNativeTokenOwnerId, RemoveNativeTokenOwner },
            { _isNativeTokenOwnerId, IsNativeTokenOwner },
            { _getAllNativeTokenOwnersId, GetAllNativeTokenOwners },
            { _setL1BaseFeeEstimateInertiaId, SetL1BaseFeeEstimateInertia },
            { _setL2BaseFeeId, SetL2BaseFee },
            { _setMinimumL2BaseFeeId, SetMinimumL2BaseFee },
            { _setSpeedLimitId, SetSpeedLimit },
            { _setMaxTxGasLimitId, SetMaxTxGasLimit },
            { _setMaxBlockGasLimitId, SetMaxBlockGasLimit },
            { _setL2GasPricingInertiaId, SetL2GasPricingInertia },
            { _setL2GasBacklogToleranceId, SetL2GasBacklogTolerance },
            { _getNetworkFeeAccountId, GetNetworkFeeAccount },
            { _getInfraFeeAccountId, GetInfraFeeAccount },
            { _setNetworkFeeAccountId, SetNetworkFeeAccount },
            { _setInfraFeeAccountId, SetInfraFeeAccount },
            { _scheduleArbOSUpgradeId, ScheduleArbOSUpgrade },
            { _setL1PricingEquilibrationUnitsId, SetL1PricingEquilibrationUnits },
            { _setL1PricingInertiaId, SetL1PricingInertia },
            { _setL1PricingRewardRecipientId, SetL1PricingRewardRecipient },
            { _setL1PricingRewardRateId, SetL1PricingRewardRate },
            { _setL1PricePerUnitId, SetL1PricePerUnit },
            { _setPerBatchGasChargeId, SetPerBatchGasCharge },
            { _setAmortizedCostCapBipsId, SetAmortizedCostCapBips },
            { _setBrotliCompressionLevelId, SetBrotliCompressionLevel },
            { _releaseL1PricerSurplusFundsId, ReleaseL1PricerSurplusFunds },
            { _setInkPriceId, SetInkPrice },
            { _setWasmMaxStackDepthId, SetWasmMaxStackDepth },
            { _setWasmFreePagesId, SetWasmFreePages },
            { _setWasmPageGasId, SetWasmPageGas },
            { _setWasmPageLimitId, SetWasmPageLimit },
            { _setWasmMinInitGasId, SetWasmMinInitGas },
            { _setWasmInitCostScalarId, SetWasmInitCostScalar },
            { _setWasmExpiryDaysId, SetWasmExpiryDays },
            { _setWasmKeepaliveDaysId, SetWasmKeepaliveDays },
            { _setWasmBlockCacheSizeId, SetWasmBlockCacheSize },
            { _setWasmMaxSizeId, SetWasmMaxSize },
            { _addWasmCacheManagerId, AddWasmCacheManager },
            { _removeWasmCacheManagerId, RemoveWasmCacheManager },
            { _setChainConfigId, SetChainConfig },
            { _setCalldataPriceIncreaseId, SetCalldataPriceIncrease },
            { _setParentGasFloorPerTokenId, SetParentGasFloorPerToken },
            { _setGasBacklogId, SetGasBacklog },
            { _setGasPricingConstraintsId, SetGasPricingConstraints }

        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[_getInfraFeeAccountId].ArbOSVersion = ArbosVersion.Five;
        PrecompileFunctionDescription[_setInfraFeeAccountId].ArbOSVersion = ArbosVersion.Five;
        PrecompileFunctionDescription[_releaseL1PricerSurplusFundsId].ArbOSVersion = ArbosVersion.Ten;
        PrecompileFunctionDescription[_setChainConfigId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctionDescription[_setBrotliCompressionLevelId].ArbOSVersion = ArbosVersion.Twenty;

        // Stylus methods
        PrecompileFunctionDescription[_setInkPriceId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmMaxStackDepthId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmFreePagesId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmPageGasId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmPageLimitId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmMinInitGasId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmInitCostScalarId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmExpiryDaysId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmKeepaliveDaysId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_setWasmBlockCacheSizeId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_addWasmCacheManagerId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctionDescription[_removeWasmCacheManagerId].ArbOSVersion = ArbosVersion.Stylus;

        PrecompileFunctionDescription[_setCalldataPriceIncreaseId].ArbOSVersion = ArbosVersion.Forty;
        PrecompileFunctionDescription[_setWasmMaxSizeId].ArbOSVersion = ArbosVersion.Forty;
        PrecompileFunctionDescription[_setNativeTokenManagementFromId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[_addNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[_removeNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[_isNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[_getAllNativeTokenOwnersId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctionDescription[_setMaxBlockGasLimitId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[_setParentGasFloorPerTokenId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[_setGasBacklogId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[_setGasPricingConstraintsId].ArbOSVersion = ArbosVersion.Fifty;
    }

    private static byte[] AddChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_addChainOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_removeChainOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.RemoveChainOwner(context, account);
        return [];
    }

    private static byte[] IsChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_isChainOwnerId].AbiFunctionDescription;

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

        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_getAllChainOwnersId].AbiFunctionDescription;

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
            PrecompileFunctionDescription[_setNativeTokenManagementFromId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_addNativeTokenOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_removeNativeTokenOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.RemoveNativeTokenOwner(context, account);
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

        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[_getAllNativeTokenOwnersId].AbiFunctionDescription;

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
            PrecompileFunctionDescription[_setL1BaseFeeEstimateInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setL2BaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setMinimumL2BaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setSpeedLimitId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setMaxTxGasLimitId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setMaxBlockGasLimitId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setL2GasPricingInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setL2GasBacklogToleranceId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_getNetworkFeeAccountId].AbiFunctionDescription.GetReturnInfo().Signature,
            networkFeeAccount
        );
    }

    private static byte[] GetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address infraFeeAccount = ArbOwner.GetInfraFeeAccount(context);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_getInfraFeeAccountId].AbiFunctionDescription.GetReturnInfo().Signature,
            infraFeeAccount
        );
    }

    private static byte[] SetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[_setNetworkFeeAccountId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setInfraFeeAccountId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_scheduleArbOSUpgradeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setL1PricingEquilibrationUnitsId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setL1PricingInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setL1PricingRewardRecipientId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setL1PricingRewardRateId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setL1PricePerUnitId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setPerBatchGasChargeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setAmortizedCostCapBipsId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setBrotliCompressionLevelId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_releaseL1PricerSurplusFundsId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setInkPriceId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmMaxStackDepthId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmFreePagesId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmPageGasId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmPageLimitId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmMinInitGasId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmInitCostScalarId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmExpiryDaysId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmKeepaliveDaysId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmBlockCacheSizeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setWasmMaxSizeId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_addWasmCacheManagerId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_removeWasmCacheManagerId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setChainConfigId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setCalldataPriceIncreaseId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setParentGasFloorPerTokenId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setGasBacklogId].AbiFunctionDescription.GetCallInfo().Signature,
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
            PrecompileFunctionDescription[_setGasPricingConstraintsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong[][] constraintsRaw = (ulong[][])decoded[0];
        ArbOwner.SetGasPricingConstraints(context, constraintsRaw);
        return [];
    }
}
