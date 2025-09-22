using System.Diagnostics;
using Nethermind.Abi;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbOwnerParser : IArbitrumPrecompile<ArbOwnerParser>
{
    public static readonly ArbOwnerParser Instance = new();
    public static Address Address { get; } = ArbOwner.Address;

    private static readonly Dictionary<string, AbiFunctionDescription> precompileFunctions;

    private static readonly uint _addChainOwnerId;
    private static readonly uint _removeChainOwnerId;
    private static readonly uint _isChainOwnerId;
    private static readonly uint _getAllChainOwnersId;
    private static readonly uint _setNativeTokenManagementFromId;
    private static readonly uint _addNativeTokenOwnerId;
    private static readonly uint _removeNativeTokenOwnerId;
    private static readonly uint _isNativeTokenOwnerId;
    private static readonly uint _getAllNativeTokenOwnersId;
    private static readonly uint _setL1BaseFeeEstimateInertiaId;
    private static readonly uint _setL2BaseFeeId;
    private static readonly uint _setMinimumL2BaseFeeId;
    private static readonly uint _setSpeedLimitId;
    private static readonly uint _setMaxTxGasLimitId;
    private static readonly uint _setL2GasPricingInertiaId;
    private static readonly uint _setL2GasBacklogToleranceId;
    private static readonly uint _getNetworkFeeAccountId;
    private static readonly uint _getInfraFeeAccountId;
    private static readonly uint _setNetworkFeeAccountId;
    private static readonly uint _setInfraFeeAccountId;
    private static readonly uint _scheduleArbOSUpgradeId;
    private static readonly uint _setL1PricingEquilibrationUnitsId;
    private static readonly uint _setL1PricingInertiaId;
    private static readonly uint _setL1PricingRewardRecipientId;
    private static readonly uint _setL1PricingRewardRateId;
    private static readonly uint _setL1PricePerUnitId;
    private static readonly uint _setPerBatchGasChargeId;
    private static readonly uint _setBrotliCompressionLevelId;
    private static readonly uint _setAmortizedCostCapBipsId;
    private static readonly uint _releaseL1PricerSurplusFundsId;
    private static readonly uint _setInkPriceId;
    private static readonly uint _setWasmMaxStackDepthId;
    private static readonly uint _setWasmFreePagesId;
    private static readonly uint _setWasmPageGasId;
    private static readonly uint _setWasmPageLimitId;
    private static readonly uint _setWasmMaxSizeId;
    private static readonly uint _setWasmMinInitGasId;
    private static readonly uint _setWasmInitCostScalarId;
    private static readonly uint _setWasmExpiryDaysId;
    private static readonly uint _setWasmKeepaliveDaysId;
    private static readonly uint _setWasmBlockCacheSizeId;
    private static readonly uint _addWasmCacheManagerId;
    private static readonly uint _removeWasmCacheManagerId;
    private static readonly uint _setChainConfigId;
    private static readonly uint _setCalldataPriceIncreaseId;

    static ArbOwnerParser()
    {
        precompileFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbOwner.Abi);

        _addChainOwnerId = MethodIdHelper.GetMethodId("addChainOwner(address)");
        _removeChainOwnerId = MethodIdHelper.GetMethodId("removeChainOwner(address)");
        _isChainOwnerId = MethodIdHelper.GetMethodId("isChainOwner(address)");
        _getAllChainOwnersId = MethodIdHelper.GetMethodId("getAllChainOwners()");
        _setNativeTokenManagementFromId = MethodIdHelper.GetMethodId("setNativeTokenManagementFrom(uint64)");
        _addNativeTokenOwnerId = MethodIdHelper.GetMethodId("addNativeTokenOwner(address)");
        _removeNativeTokenOwnerId = MethodIdHelper.GetMethodId("removeNativeTokenOwner(address)");
        _isNativeTokenOwnerId = MethodIdHelper.GetMethodId("isNativeTokenOwner(address)");
        _getAllNativeTokenOwnersId = MethodIdHelper.GetMethodId("getAllNativeTokenOwners()");
        _setL1BaseFeeEstimateInertiaId = MethodIdHelper.GetMethodId("setL1BaseFeeEstimateInertia(uint64)");
        _setL2BaseFeeId = MethodIdHelper.GetMethodId("setL2BaseFee(uint256)");
        _setMinimumL2BaseFeeId = MethodIdHelper.GetMethodId("setMinimumL2BaseFee(uint256)");
        _setSpeedLimitId = MethodIdHelper.GetMethodId("setSpeedLimit(uint64)");
        _setMaxTxGasLimitId = MethodIdHelper.GetMethodId("setMaxTxGasLimit(uint64)");
        _setL2GasPricingInertiaId = MethodIdHelper.GetMethodId("setL2GasPricingInertia(uint64)");
        _setL2GasBacklogToleranceId = MethodIdHelper.GetMethodId("setL2GasBacklogTolerance(uint64)");
        _getNetworkFeeAccountId = MethodIdHelper.GetMethodId("getNetworkFeeAccount()");
        _getInfraFeeAccountId = MethodIdHelper.GetMethodId("getInfraFeeAccount()");
        _setNetworkFeeAccountId = MethodIdHelper.GetMethodId("setNetworkFeeAccount(address)");
        _setInfraFeeAccountId = MethodIdHelper.GetMethodId("setInfraFeeAccount(address)");
        _scheduleArbOSUpgradeId = MethodIdHelper.GetMethodId("scheduleArbOSUpgrade(uint64,uint64)");
        _setL1PricingEquilibrationUnitsId = MethodIdHelper.GetMethodId("setL1PricingEquilibrationUnits(uint256)");
        _setL1PricingInertiaId = MethodIdHelper.GetMethodId("setL1PricingInertia(uint64)");
        _setL1PricingRewardRecipientId = MethodIdHelper.GetMethodId("setL1PricingRewardRecipient(address)");
        _setL1PricingRewardRateId = MethodIdHelper.GetMethodId("setL1PricingRewardRate(uint64)");
        _setL1PricePerUnitId = MethodIdHelper.GetMethodId("setL1PricePerUnit(uint256)");
        _setPerBatchGasChargeId = MethodIdHelper.GetMethodId("setPerBatchGasCharge(int64)");
        _setBrotliCompressionLevelId = MethodIdHelper.GetMethodId("setBrotliCompressionLevel(uint64)");
        _setAmortizedCostCapBipsId = MethodIdHelper.GetMethodId("setAmortizedCostCapBips(uint64)");
        _releaseL1PricerSurplusFundsId = MethodIdHelper.GetMethodId("releaseL1PricerSurplusFunds(uint256)");
        _setInkPriceId = MethodIdHelper.GetMethodId("setInkPrice(uint32)");
        _setWasmMaxStackDepthId = MethodIdHelper.GetMethodId("setWasmMaxStackDepth(uint32)");
        _setWasmFreePagesId = MethodIdHelper.GetMethodId("setWasmFreePages(uint16)");
        _setWasmPageGasId = MethodIdHelper.GetMethodId("setWasmPageGas(uint16)");
        _setWasmPageLimitId = MethodIdHelper.GetMethodId("setWasmPageLimit(uint16)");
        _setWasmMaxSizeId = MethodIdHelper.GetMethodId("setWasmMaxSize(uint32)");
        _setWasmMinInitGasId = MethodIdHelper.GetMethodId("setWasmMinInitGas(uint8,uint16)");
        _setWasmInitCostScalarId = MethodIdHelper.GetMethodId("setWasmInitCostScalar(uint64)");
        _setWasmExpiryDaysId = MethodIdHelper.GetMethodId("setWasmExpiryDays(uint16)");
        _setWasmKeepaliveDaysId = MethodIdHelper.GetMethodId("setWasmKeepaliveDays(uint16)");
        _setWasmBlockCacheSizeId = MethodIdHelper.GetMethodId("setWasmBlockCacheSize(uint16)");
        _addWasmCacheManagerId = MethodIdHelper.GetMethodId("addWasmCacheManager(address)");
        _removeWasmCacheManagerId = MethodIdHelper.GetMethodId("removeWasmCacheManager(address)");
        _setChainConfigId = MethodIdHelper.GetMethodId("setChainConfig(string)");
        _setCalldataPriceIncreaseId = MethodIdHelper.GetMethodId("setCalldataPriceIncrease(bool)");
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

    private static byte[] AddChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "addChainOwner",
            inputData,
            AbiType.Address
        );

        Address account = (Address)decoded[0];
        ArbOwner.AddChainOwner(context, account);
        return [];
    }

    private static byte[] RemoveChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        ArbOwner.RemoveChainOwner(context, account);
        return [];
    }

    private static byte[] IsChainOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        bool isOwner = ArbOwner.IsChainOwner(context, account);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        if (isOwner)
            abiEncodedResult[Hash256.Size - 1] = 1;

        return abiEncodedResult;
    }

    private static byte[] GetAllChainOwners(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] allChainOwners = ArbOwner.GetAllChainOwners(context);

        AbiFunctionDescription function = precompileFunctions["getAllChainOwners"];

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [allChainOwners]
        );

        return abiEncodedResult;
    }

    private static byte[] SetNativeTokenManagementFrom(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> timestampBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong timestamp = timestampBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetNativeTokenManagementFrom(context, timestamp);
        return [];
    }

    private static byte[] AddNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        ArbOwner.AddNativeTokenOwner(context, account);
        return [];
    }

    private static byte[] RemoveNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        ArbOwner.RemoveNativeTokenOwner(context, account);
        return [];
    }

    private static byte[] IsNativeTokenOwner(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        bool isOwner = ArbOwner.IsNativeTokenOwner(context, account);

        byte[] abiEncodedResult = new byte[Hash256.Size];
        if (isOwner)
            abiEncodedResult[Hash256.Size - 1] = 1;

        return abiEncodedResult;
    }

    private static byte[] GetAllNativeTokenOwners(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address[] allNativeTokenOwners = ArbOwner.GetAllNativeTokenOwners(context);

        AbiFunctionDescription function = precompileFunctions["getAllNativeTokenOwners"];

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [allNativeTokenOwners]
        );

        return abiEncodedResult;
    }

    private static byte[] SetL1BaseFeeEstimateInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "setL1BaseFeeEstimateInertia",
            inputData,
            AbiType.UInt64
        );

        ulong inertia = (ulong)decoded[0];
        ArbOwner.SetL1BaseFeeEstimateInertia(context, inertia);
        return [];
    }
    private static byte[] SetL2BaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "setL2BaseFee",
            inputData,
            AbiType.UInt256
        );

        UInt256 l2BaseFee = (UInt256)decoded[0];
        ArbOwner.SetL2BaseFee(context, l2BaseFee);
        return [];
    }

    private static byte[] SetMinimumL2BaseFee(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> priceInWeiBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        UInt256 priceInWei = new(priceInWeiBytes, isBigEndian: true);

        ArbOwner.SetMinimumL2BaseFee(context, priceInWei);
        return [];
    }

    private static byte[] SetSpeedLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> limitBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong limit = limitBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetSpeedLimit(context, limit);
        return [];
    }

    private static byte[] SetMaxTxGasLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> limitBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong limit = limitBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetMaxTxGasLimit(context, limit);
        return [];
    }

    private static byte[] SetL2GasPricingInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> inertiaBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong inertia = inertiaBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetL2GasPricingInertia(context, inertia);
        return [];
    }

    private static byte[] SetL2GasBacklogTolerance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> backlogToleranceBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong backlogTolerance = backlogToleranceBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

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
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        ArbOwner.SetNetworkFeeAccount(context, account);
        return [];
    }

    private static byte[] SetInfraFeeAccount(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> accountBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address account = new(accountBytes[(Hash256.Size - Address.Size)..]);

        ArbOwner.SetInfraFeeAccount(context, account);
        return [];
    }

    private static byte[] ScheduleArbOSUpgrade(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "scheduleArbOSUpgrade",
            inputData,
            AbiType.UInt64,
            AbiType.UInt64
        );

        ulong version = (ulong)decoded[0];
        ulong timestamp = (ulong)decoded[1];
        ArbOwner.ScheduleArbOSUpgrade(context, version, timestamp);
        return [];
    }

    private static byte[] SetL1PricingEquilibrationUnits(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> unitsBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        UInt256 units = new(unitsBytes, isBigEndian: true);

        ArbOwner.SetL1PricingEquilibrationUnits(context, units);
        return [];
    }

    private static byte[] SetL1PricingInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> inertiaBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong inertia = inertiaBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetL1PricingInertia(context, inertia);
        return [];
    }

    private static byte[] SetL1PricingRewardRecipient(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> recipientBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address recipient = new(recipientBytes[(Hash256.Size - Address.Size)..]);

        ArbOwner.SetL1PricingRewardRecipient(context, recipient);
        return [];
    }

    private static byte[] SetL1PricingRewardRate(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> weiPerUnitBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong weiPerUnit = weiPerUnitBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetL1PricingRewardRate(context, weiPerUnit);
        return [];
    }

    private static byte[] SetL1PricePerUnit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> pricePerUnitBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        UInt256 pricePerUnit = new(pricePerUnitBytes, isBigEndian: true);

        ArbOwner.SetL1PricePerUnit(context, pricePerUnit);
        return [];
    }

    private static byte[] SetPerBatchGasCharge(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> baseChargeBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong baseCharge = baseChargeBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetPerBatchGasCharge(context, baseCharge);
        return [];
    }

    private static byte[] SetAmortizedCostCapBips(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> capBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong cap = capBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetAmortizedCostCapBips(context, cap);
        return [];
    }

    private static byte[] SetBrotliCompressionLevel(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> levelBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong level = levelBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetBrotliCompressionLevel(context, level);
        return [];
    }

    private static byte[] ReleaseL1PricerSurplusFunds(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> maxWeiToReleaseBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        UInt256 maxWeiToRelease = new(maxWeiToReleaseBytes, isBigEndian: true);

        UInt256 weiToRelease = ArbOwner.ReleaseL1PricerSurplusFunds(context, maxWeiToRelease);
        return weiToRelease.ToBigEndian();
    }

    private static byte[] SetInkPrice(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "setInkPrice",
            inputData,
            AbiType.UInt32
        );

        uint inkPrice = (uint)decoded[0];
        ArbOwner.SetInkPrice(context, inkPrice);
        return [];
    }

    private static byte[] SetWasmMaxStackDepth(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> maxStackDepthBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        uint maxStackDepth = BytesToUint32BigEndian(maxStackDepthBytes[(Hash256.Size - 4)..]);

        ArbOwner.SetWasmMaxStackDepth(context, maxStackDepth);
        return [];
    }

    private static byte[] SetWasmFreePages(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "setWasmFreePages",
            inputData,
            AbiType.UInt16
        );

        ushort freePages = (ushort)decoded[0];
        ArbOwner.SetWasmFreePages(context, freePages);
        return [];
    }

    private static byte[] SetWasmPageGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> pageGasBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ushort pageGas = BytesToUshortBigEndian(pageGasBytes[(Hash256.Size - 2)..]);

        ArbOwner.SetWasmPageGas(context, pageGas);
        return [];
    }

    private static byte[] SetWasmPageLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> pageLimitBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ushort pageLimit = BytesToUshortBigEndian(pageLimitBytes[(Hash256.Size - 2)..]);

        ArbOwner.SetWasmPageLimit(context, pageLimit);
        return [];
    }

    private static byte[] SetWasmMinInitGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> gasBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong gas = gasBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ReadOnlySpan<byte> cachedBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong cached = cachedBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetWasmMinInitGas(context, gas, cached);
        return [];
    }

    private static byte[] SetWasmInitCostScalar(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> percentBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ulong percent = percentBytes[(Hash256.Size - 8)..].ToULongFromBigEndianByteArrayWithoutLeadingZeros();

        ArbOwner.SetWasmInitCostScalar(context, percent);
        return [];
    }

    private static byte[] SetWasmExpiryDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> expiryDaysBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ushort expiryDays = BytesToUshortBigEndian(expiryDaysBytes[(Hash256.Size - 2)..]);

        ArbOwner.SetWasmExpiryDays(context, expiryDays);
        return [];
    }

    private static byte[] SetWasmKeepaliveDays(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> keepaliveDaysBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ushort keepaliveDays = BytesToUshortBigEndian(keepaliveDaysBytes[(Hash256.Size - 2)..]);

        ArbOwner.SetWasmKeepaliveDays(context, keepaliveDays);
        return [];
    }

    private static byte[] SetWasmBlockCacheSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> blockCacheSizeBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        ushort blockCacheSize = BytesToUshortBigEndian(blockCacheSizeBytes[(Hash256.Size - 2)..]);

        ArbOwner.SetWasmBlockCacheSize(context, blockCacheSize);
        return [];
    }

    private static byte[] SetWasmMaxSize(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> maxWasmSizeBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        uint maxWasmSize = BytesToUint32BigEndian(maxWasmSizeBytes[(Hash256.Size - 4)..]);

        ArbOwner.SetWasmMaxSize(context, maxWasmSize);
        return [];
    }

    private static byte[] AddWasmCacheManager(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> managerBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address manager = new(managerBytes[(Hash256.Size - Address.Size)..]);

        ArbOwner.AddWasmCacheManager(context, manager);
        return [];
    }

    private static byte[] RemoveWasmCacheManager(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> managerBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address manager = new(managerBytes[(Hash256.Size - Address.Size)..]);

        ArbOwner.RemoveWasmCacheManager(context, manager);
        return [];
    }

    private static byte[] SetChainConfig(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "setChainConfig",
            inputData,
            AbiType.String
        );

        string chainConfig = (string)decoded[0];
        ArbOwner.SetChainConfig(context, System.Text.Encoding.UTF8.GetBytes(chainConfig));
        return [];
    }

    private static byte[] SetCalldataPriceIncrease(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        object[] decoded = ArbitrumPrecompileAbiDecoder.Decode(
            "setCalldataPriceIncrease",
            inputData,
            AbiType.Bool
        );

        bool enabled = (bool)decoded[0];
        ArbOwner.SetCalldataPriceIncrease(context, enabled);
        return [];
    }

    private static uint BytesToUint32BigEndian(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
            throw new ArgumentException($"Input data too short: want 4 bytes and got {bytes.Length}");

        return (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
    }

    private static ushort BytesToUshortBigEndian(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2)
            throw new ArgumentException($"Input data too short: want 2 bytes and got {bytes.Length}");

        return (ushort)(bytes[0] << 8 | bytes[1]);
    }
}
