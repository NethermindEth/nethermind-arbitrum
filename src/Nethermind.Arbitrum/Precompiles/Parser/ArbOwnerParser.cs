using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbOwnerParser : IArbitrumPrecompile<ArbOwnerParser>
{
    public static readonly ArbOwnerParser Instance = new();

    public static Address Address { get; } = ArbOwner.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbOwner.Abi);

    private static readonly uint _addChainOwnerId = PrecompileHelper.GetMethodId("addChainOwner(address)");
    private static readonly uint _removeChainOwnerId = PrecompileHelper.GetMethodId("removeChainOwner(address)");
    private static readonly uint _isChainOwnerId = PrecompileHelper.GetMethodId("isChainOwner(address)");
    private static readonly uint _getAllChainOwnersId = PrecompileHelper.GetMethodId("getAllChainOwners()");
    private static readonly uint _setNativeTokenManagementFromId = PrecompileHelper.GetMethodId("setNativeTokenManagementFrom(uint64)");
    private static readonly uint _addNativeTokenOwnerId = PrecompileHelper.GetMethodId("addNativeTokenOwner(address)");
    private static readonly uint _removeNativeTokenOwnerId = PrecompileHelper.GetMethodId("removeNativeTokenOwner(address)");
    private static readonly uint _isNativeTokenOwnerId = PrecompileHelper.GetMethodId("isNativeTokenOwner(address)");
    private static readonly uint _getAllNativeTokenOwnersId = PrecompileHelper.GetMethodId("getAllNativeTokenOwners()");
    private static readonly uint _setL1BaseFeeEstimateInertiaId = PrecompileHelper.GetMethodId("setL1BaseFeeEstimateInertia(uint64)");
    private static readonly uint _setL2BaseFeeId = PrecompileHelper.GetMethodId("setL2BaseFee(uint256)");
    private static readonly uint _setMinimumL2BaseFeeId = PrecompileHelper.GetMethodId("setMinimumL2BaseFee(uint256)");
    private static readonly uint _setSpeedLimitId = PrecompileHelper.GetMethodId("setSpeedLimit(uint64)");
    private static readonly uint _setMaxTxGasLimitId = PrecompileHelper.GetMethodId("setMaxTxGasLimit(uint64)");
    private static readonly uint _setL2GasPricingInertiaId = PrecompileHelper.GetMethodId("setL2GasPricingInertia(uint64)");
    private static readonly uint _setL2GasBacklogToleranceId = PrecompileHelper.GetMethodId("setL2GasBacklogTolerance(uint64)");
    private static readonly uint _getNetworkFeeAccountId = PrecompileHelper.GetMethodId("getNetworkFeeAccount()");
    private static readonly uint _getInfraFeeAccountId = PrecompileHelper.GetMethodId("getInfraFeeAccount()");
    private static readonly uint _setNetworkFeeAccountId = PrecompileHelper.GetMethodId("setNetworkFeeAccount(address)");
    private static readonly uint _setInfraFeeAccountId = PrecompileHelper.GetMethodId("setInfraFeeAccount(address)");
    private static readonly uint _scheduleArbOSUpgradeId = PrecompileHelper.GetMethodId("scheduleArbOSUpgrade(uint64,uint64)");
    private static readonly uint _setL1PricingEquilibrationUnitsId = PrecompileHelper.GetMethodId("setL1PricingEquilibrationUnits(uint256)");
    private static readonly uint _setL1PricingInertiaId = PrecompileHelper.GetMethodId("setL1PricingInertia(uint64)");
    private static readonly uint _setL1PricingRewardRecipientId = PrecompileHelper.GetMethodId("setL1PricingRewardRecipient(address)");
    private static readonly uint _setL1PricingRewardRateId = PrecompileHelper.GetMethodId("setL1PricingRewardRate(uint64)");
    private static readonly uint _setL1PricePerUnitId = PrecompileHelper.GetMethodId("setL1PricePerUnit(uint256)");
    private static readonly uint _setPerBatchGasChargeId = PrecompileHelper.GetMethodId("setPerBatchGasCharge(int64)");
    private static readonly uint _setBrotliCompressionLevelId = PrecompileHelper.GetMethodId("setBrotliCompressionLevel(uint64)");
    private static readonly uint _setAmortizedCostCapBipsId = PrecompileHelper.GetMethodId("setAmortizedCostCapBips(uint64)");
    private static readonly uint _releaseL1PricerSurplusFundsId = PrecompileHelper.GetMethodId("releaseL1PricerSurplusFunds(uint256)");
    private static readonly uint _setInkPriceId = PrecompileHelper.GetMethodId("setInkPrice(uint32)");
    private static readonly uint _setWasmMaxStackDepthId = PrecompileHelper.GetMethodId("setWasmMaxStackDepth(uint32)");
    private static readonly uint _setWasmFreePagesId = PrecompileHelper.GetMethodId("setWasmFreePages(uint16)");
    private static readonly uint _setWasmPageGasId = PrecompileHelper.GetMethodId("setWasmPageGas(uint16)");
    private static readonly uint _setWasmPageLimitId = PrecompileHelper.GetMethodId("setWasmPageLimit(uint16)");
    private static readonly uint _setWasmMaxSizeId = PrecompileHelper.GetMethodId("setWasmMaxSize(uint32)");
    private static readonly uint _setWasmMinInitGasId = PrecompileHelper.GetMethodId("setWasmMinInitGas(uint8,uint16)");
    private static readonly uint _setWasmInitCostScalarId = PrecompileHelper.GetMethodId("setWasmInitCostScalar(uint64)");
    private static readonly uint _setWasmExpiryDaysId = PrecompileHelper.GetMethodId("setWasmExpiryDays(uint16)");
    private static readonly uint _setWasmKeepaliveDaysId = PrecompileHelper.GetMethodId("setWasmKeepaliveDays(uint16)");
    private static readonly uint _setWasmBlockCacheSizeId = PrecompileHelper.GetMethodId("setWasmBlockCacheSize(uint16)");
    private static readonly uint _addWasmCacheManagerId = PrecompileHelper.GetMethodId("addWasmCacheManager(address)");
    private static readonly uint _removeWasmCacheManagerId = PrecompileHelper.GetMethodId("removeWasmCacheManager(address)");
    private static readonly uint _setChainConfigId = PrecompileHelper.GetMethodId("setChainConfig(string)");
    private static readonly uint _setCalldataPriceIncreaseId = PrecompileHelper.GetMethodId("setCalldataPriceIncrease(bool)");

    static ArbOwnerParser()
    {
        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (methodId == _addChainOwnerId)
        {
            return AddChainOwner(context, inputDataSpan);
        }

        if (methodId == _removeChainOwnerId)
        {
            return RemoveChainOwner(context, inputDataSpan);
        }

        if (methodId == _isChainOwnerId)
        {
            return IsChainOwner(context, inputDataSpan);
        }

        if (methodId == _getAllChainOwnersId)
        {
            return GetAllChainOwners(context, inputDataSpan);
        }

        if (methodId == _setNativeTokenManagementFromId)
        {
            return SetNativeTokenManagementFrom(context, inputDataSpan);
        }

        if (methodId == _addNativeTokenOwnerId)
        {
            return AddNativeTokenOwner(context, inputDataSpan);
        }

        if (methodId == _removeNativeTokenOwnerId)
        {
            return RemoveNativeTokenOwner(context, inputDataSpan);
        }

        if (methodId == _isNativeTokenOwnerId)
        {
            return IsNativeTokenOwner(context, inputDataSpan);
        }

        if (methodId == _getAllNativeTokenOwnersId)
        {
            return GetAllNativeTokenOwners(context, inputDataSpan);
        }

        if (methodId == _setL1BaseFeeEstimateInertiaId)
        {
            return SetL1BaseFeeEstimateInertia(context, inputDataSpan);
        }

        if (methodId == _setL2BaseFeeId)
        {
            return SetL2BaseFee(context, inputDataSpan);
        }

        if (methodId == _setMinimumL2BaseFeeId)
        {
            return SetMinimumL2BaseFee(context, inputDataSpan);
        }

        if (methodId == _setSpeedLimitId)
        {
            return SetSpeedLimit(context, inputDataSpan);
        }

        if (methodId == _setMaxTxGasLimitId)
        {
            return SetMaxTxGasLimit(context, inputDataSpan);
        }

        if (methodId == _setL2GasPricingInertiaId)
        {
            return SetL2GasPricingInertia(context, inputDataSpan);
        }

        if (methodId == _setL2GasBacklogToleranceId)
        {
            return SetL2GasBacklogTolerance(context, inputDataSpan);
        }

        if (methodId == _getNetworkFeeAccountId)
        {
            return GetNetworkFeeAccount(context, inputDataSpan);
        }

        if (methodId == _getInfraFeeAccountId)
        {
            return GetInfraFeeAccount(context, inputDataSpan);
        }

        if (methodId == _setNetworkFeeAccountId)
        {
            return SetNetworkFeeAccount(context, inputDataSpan);
        }

        if (methodId == _setInfraFeeAccountId)
        {
            return SetInfraFeeAccount(context, inputDataSpan);
        }

        if (methodId == _scheduleArbOSUpgradeId)
        {
            return ScheduleArbOSUpgrade(context, inputDataSpan);
        }

        if (methodId == _setL1PricingEquilibrationUnitsId)
        {
            return SetL1PricingEquilibrationUnits(context, inputDataSpan);
        }

        if (methodId == _setL1PricingInertiaId)
        {
            return SetL1PricingInertia(context, inputDataSpan);
        }

        if (methodId == _setL1PricingRewardRecipientId)
        {
            return SetL1PricingRewardRecipient(context, inputDataSpan);
        }

        if (methodId == _setL1PricingRewardRateId)
        {
            return SetL1PricingRewardRate(context, inputDataSpan);
        }

        if (methodId == _setL1PricePerUnitId)
        {
            return SetL1PricePerUnit(context, inputDataSpan);
        }

        if (methodId == _setPerBatchGasChargeId)
        {
            return SetPerBatchGasCharge(context, inputDataSpan);
        }

        if (methodId == _setAmortizedCostCapBipsId)
        {
            return SetAmortizedCostCapBips(context, inputDataSpan);
        }

        if (methodId == _setBrotliCompressionLevelId)
        {
            return SetBrotliCompressionLevel(context, inputDataSpan);
        }

        if (methodId == _releaseL1PricerSurplusFundsId)
        {
            return ReleaseL1PricerSurplusFunds(context, inputDataSpan);
        }

        if (methodId == _setInkPriceId)
        {
            return SetInkPrice(context, inputDataSpan);
        }

        if (methodId == _setWasmMaxStackDepthId)
        {
            return SetWasmMaxStackDepth(context, inputDataSpan);
        }

        if (methodId == _setWasmFreePagesId)
        {
            return SetWasmFreePages(context, inputDataSpan);
        }

        if (methodId == _setWasmPageGasId)
        {
            return SetWasmPageGas(context, inputDataSpan);
        }

        if (methodId == _setWasmPageLimitId)
        {
            return SetWasmPageLimit(context, inputDataSpan);
        }

        if (methodId == _setWasmMinInitGasId)
        {
            return SetWasmMinInitGas(context, inputDataSpan);
        }

        if (methodId == _setWasmInitCostScalarId)
        {
            return SetWasmInitCostScalar(context, inputDataSpan);
        }

        if (methodId == _setWasmExpiryDaysId)
        {
            return SetWasmExpiryDays(context, inputDataSpan);
        }

        if (methodId == _setWasmKeepaliveDaysId)
        {
            return SetWasmKeepaliveDays(context, inputDataSpan);
        }

        if (methodId == _setWasmBlockCacheSizeId)
        {
            return SetWasmBlockCacheSize(context, inputDataSpan);
        }

        if (methodId == _setWasmMaxSizeId)
        {
            return SetWasmMaxSize(context, inputDataSpan);
        }

        if (methodId == _addWasmCacheManagerId)
        {
            return AddWasmCacheManager(context, inputDataSpan);
        }

        if (methodId == _removeWasmCacheManagerId)
        {
            return RemoveWasmCacheManager(context, inputDataSpan);
        }

        if (methodId == _setChainConfigId)
        {
            return SetChainConfig(context, inputDataSpan);
        }

        if (methodId == _setCalldataPriceIncreaseId)
        {
            return SetCalldataPriceIncrease(context, inputDataSpan);
        }

        throw new ArgumentException($"Invalid precompile method ID: {methodId}");
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctions[_getInfraFeeAccountId].ArbOSVersion = ArbosVersion.Five;
        PrecompileFunctions[_setInfraFeeAccountId].ArbOSVersion = ArbosVersion.Five;
        PrecompileFunctions[_releaseL1PricerSurplusFundsId].ArbOSVersion = ArbosVersion.Ten;
        PrecompileFunctions[_setChainConfigId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctions[_setBrotliCompressionLevelId].ArbOSVersion = ArbosVersion.Twenty;

        // Stylus methods
        PrecompileFunctions[_setInkPriceId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmMaxStackDepthId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmFreePagesId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmPageGasId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmPageLimitId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmMinInitGasId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmInitCostScalarId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmExpiryDaysId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmKeepaliveDaysId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_setWasmBlockCacheSizeId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_addWasmCacheManagerId].ArbOSVersion = ArbosVersion.Stylus;
        PrecompileFunctions[_removeWasmCacheManagerId].ArbOSVersion = ArbosVersion.Stylus;

        PrecompileFunctions[_setCalldataPriceIncreaseId].ArbOSVersion = ArbosVersion.Forty;
        PrecompileFunctions[_setWasmMaxSizeId].ArbOSVersion = ArbosVersion.Forty;
        PrecompileFunctions[_setNativeTokenManagementFromId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctions[_addNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctions[_removeNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctions[_isNativeTokenOwnerId].ArbOSVersion = ArbosVersion.FortyOne;
        PrecompileFunctions[_getAllNativeTokenOwnersId].ArbOSVersion = ArbosVersion.FortyOne;
    }

    private static byte[] AddChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_addChainOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.AddChainOwner(context, account);
        return [];
    }

    private static byte[] RemoveChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_removeChainOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.RemoveChainOwner(context, account);
        return [];
    }

    private static byte[] IsChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_isChainOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        bool isOwner = ArbOwner.IsChainOwner(context, account);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        if (isOwner)
            abiEncodedResult[Hash256.Size - 1] = 1;
        return abiEncodedResult;
    }

    private static byte[] GetAllChainOwners(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] allChainOwners = ArbOwner.GetAllChainOwners(context);

        AbiFunctionDescription function = PrecompileFunctions[_getAllChainOwnersId].AbiFunctionDescription;

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [allChainOwners]
        );

        return abiEncodedResult;
    }

    private static byte[] SetNativeTokenManagementFrom(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setNativeTokenManagementFromId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong timestamp = (ulong)decoded[0];
        ArbOwner.SetNativeTokenManagementFrom(context, timestamp);
        return [];
    }

    private static byte[] AddNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_addNativeTokenOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.AddNativeTokenOwner(context, account);
        return [];
    }

    private static byte[] RemoveNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_removeNativeTokenOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.RemoveNativeTokenOwner(context, account);
        return [];
    }

    private static byte[] IsNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_isNativeTokenOwnerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        bool isOwner = ArbOwner.IsNativeTokenOwner(context, account);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        if (isOwner)
            abiEncodedResult[Hash256.Size - 1] = 1;
        return abiEncodedResult;
    }

    private static byte[] GetAllNativeTokenOwners(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] allNativeTokenOwners = ArbOwner.GetAllNativeTokenOwners(context);

        AbiFunctionDescription function = PrecompileFunctions[_getAllNativeTokenOwnersId].AbiFunctionDescription;

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [allNativeTokenOwners]
        );

        return abiEncodedResult;
    }

    private static byte[] SetL1BaseFeeEstimateInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL1BaseFeeEstimateInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong inertia = (ulong)decoded[0];
        ArbOwner.SetL1BaseFeeEstimateInertia(context, inertia);
        return [];
    }
    private static byte[] SetL2BaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL2BaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 l2BaseFee = (UInt256)decoded[0];
        ArbOwner.SetL2BaseFee(context, l2BaseFee);
        return [];
    }

    private static byte[] SetMinimumL2BaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setMinimumL2BaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 priceInWei = (UInt256)decoded[0];
        ArbOwner.SetMinimumL2BaseFee(context, priceInWei);
        return [];
    }

    private static byte[] SetSpeedLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setSpeedLimitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong limit = (ulong)decoded[0];
        ArbOwner.SetSpeedLimit(context, limit);
        return [];
    }

    private static byte[] SetMaxTxGasLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setMaxTxGasLimitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong limit = (ulong)decoded[0];
        ArbOwner.SetMaxTxGasLimit(context, limit);
        return [];
    }

    private static byte[] SetL2GasPricingInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL2GasPricingInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong inertia = (ulong)decoded[0];
        ArbOwner.SetL2GasPricingInertia(context, inertia);
        return [];
    }

    private static byte[] SetL2GasBacklogTolerance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL2GasBacklogToleranceId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong backlogTolerance = (ulong)decoded[0];
        ArbOwner.SetL2GasBacklogTolerance(context, backlogTolerance);
        return [];
    }

    private static byte[] GetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address networkFeeAccount = ArbOwner.GetNetworkFeeAccount(context);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        networkFeeAccount.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);
        return abiEncodedResult;
    }

    private static byte[] GetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address infraFeeAccount = ArbOwner.GetInfraFeeAccount(context);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        infraFeeAccount.Bytes.CopyTo(abiEncodedResult, Hash256.Size - Address.Size);
        return abiEncodedResult;
    }

    private static byte[] SetNetworkFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setNetworkFeeAccountId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.SetNetworkFeeAccount(context, account);
        return [];
    }

    private static byte[] SetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setInfraFeeAccountId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address account = (Address)decoded[0];
        ArbOwner.SetInfraFeeAccount(context, account);
        return [];
    }

    private static byte[] ScheduleArbOSUpgrade(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_scheduleArbOSUpgradeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong version = (ulong)decoded[0];
        ulong timestamp = (ulong)decoded[1];
        ArbOwner.ScheduleArbOSUpgrade(context, version, timestamp);
        return [];
    }

    private static byte[] SetL1PricingEquilibrationUnits(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL1PricingEquilibrationUnitsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 units = (UInt256)decoded[0];
        ArbOwner.SetL1PricingEquilibrationUnits(context, units);
        return [];
    }

    private static byte[] SetL1PricingInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL1PricingInertiaId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong inertia = (ulong)decoded[0];
        ArbOwner.SetL1PricingInertia(context, inertia);
        return [];
    }

    private static byte[] SetL1PricingRewardRecipient(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL1PricingRewardRecipientId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address recipient = (Address)decoded[0];
        ArbOwner.SetL1PricingRewardRecipient(context, recipient);
        return [];
    }

    private static byte[] SetL1PricingRewardRate(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL1PricingRewardRateId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong weiPerUnit = (ulong)decoded[0];
        ArbOwner.SetL1PricingRewardRate(context, weiPerUnit);
        return [];
    }

    private static byte[] SetL1PricePerUnit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setL1PricePerUnitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 pricePerUnit = (UInt256)decoded[0];
        ArbOwner.SetL1PricePerUnit(context, pricePerUnit);
        return [];
    }

    private static byte[] SetPerBatchGasCharge(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setPerBatchGasChargeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        long baseCharge = (long)decoded[0];
        ArbOwner.SetPerBatchGasCharge(context, (ulong)baseCharge);
        return [];
    }

    private static byte[] SetAmortizedCostCapBips(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setAmortizedCostCapBipsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong cap = (ulong)decoded[0];
        ArbOwner.SetAmortizedCostCapBips(context, cap);
        return [];
    }

    private static byte[] SetBrotliCompressionLevel(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setBrotliCompressionLevelId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong level = (ulong)decoded[0];
        ArbOwner.SetBrotliCompressionLevel(context, level);
        return [];
    }

    private static byte[] ReleaseL1PricerSurplusFunds(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_releaseL1PricerSurplusFundsId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        UInt256 maxWeiToRelease = (UInt256)decoded[0];
        UInt256 weiToRelease = ArbOwner.ReleaseL1PricerSurplusFunds(context, maxWeiToRelease);
        return weiToRelease.ToBigEndian();
    }

    private static byte[] SetInkPrice(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setInkPriceId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        uint inkPrice = (uint)decoded[0];
        ArbOwner.SetInkPrice(context, inkPrice);
        return [];
    }

    private static byte[] SetWasmMaxStackDepth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmMaxStackDepthId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        uint maxStackDepth = (uint)decoded[0];
        ArbOwner.SetWasmMaxStackDepth(context, maxStackDepth);
        return [];
    }

    private static byte[] SetWasmFreePages(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmFreePagesId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort freePages = (ushort)decoded[0];
        ArbOwner.SetWasmFreePages(context, freePages);
        return [];
    }

    private static byte[] SetWasmPageGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmPageGasId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort pageGas = (ushort)decoded[0];
        ArbOwner.SetWasmPageGas(context, pageGas);
        return [];
    }


    private static byte[] SetWasmPageLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmPageLimitId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort pageLimit = (ushort)decoded[0];
        ArbOwner.SetWasmPageLimit(context, pageLimit);
        return [];
    }

    private static byte[] SetWasmMinInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmMinInitGasId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );
        ulong gas = (byte)decoded[0];
        ulong cached = (ushort)decoded[1];
        ArbOwner.SetWasmMinInitGas(context, gas, cached);
        return [];
    }

    private static byte[] SetWasmInitCostScalar(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmInitCostScalarId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ulong percent = (ulong)decoded[0];
        ArbOwner.SetWasmInitCostScalar(context, percent);
        return [];
    }

    private static byte[] SetWasmExpiryDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmExpiryDaysId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort expiryDays = (ushort)decoded[0];
        ArbOwner.SetWasmExpiryDays(context, expiryDays);
        return [];
    }

    private static byte[] SetWasmKeepaliveDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmKeepaliveDaysId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort keepaliveDays = (ushort)decoded[0];
        ArbOwner.SetWasmKeepaliveDays(context, keepaliveDays);
        return [];
    }

    private static byte[] SetWasmBlockCacheSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmBlockCacheSizeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        ushort blockCacheSize = (ushort)decoded[0];
        ArbOwner.SetWasmBlockCacheSize(context, blockCacheSize);
        return [];
    }

    private static byte[] SetWasmMaxSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setWasmMaxSizeId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        uint maxWasmSize = (uint)decoded[0];
        ArbOwner.SetWasmMaxSize(context, maxWasmSize);
        return [];
    }

    private static byte[] AddWasmCacheManager(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_addWasmCacheManagerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address manager = (Address)decoded[0];
        ArbOwner.AddWasmCacheManager(context, manager);
        return [];
    }

    private static byte[] RemoveWasmCacheManager(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_removeWasmCacheManagerId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address manager = (Address)decoded[0];
        ArbOwner.RemoveWasmCacheManager(context, manager);
        return [];
    }

    private static byte[] SetChainConfig(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setChainConfigId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        string chainConfig = (string)decoded[0];
        ArbOwner.SetChainConfig(context, System.Text.Encoding.UTF8.GetBytes(chainConfig));
        return [];
    }

    private static byte[] SetCalldataPriceIncrease(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_setCalldataPriceIncreaseId].AbiFunctionDescription.GetCallInfo().Signature,
            inputData.ToArray()
        );

        bool enabled = (bool)decoded[0];
        ArbOwner.SetCalldataPriceIncrease(context, enabled);
        return [];
    }
}
