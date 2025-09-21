using System.Numerics;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbGasInfoParser : IArbitrumPrecompile<ArbGasInfoParser>
{
    public static readonly ArbGasInfoParser Instance = new();

    public static Address Address { get; } = ArbGasInfo.Address;

    public static string Abi => ArbGasInfo.Abi;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(Abi);

    private static readonly uint _getPricesInWeiWithAggregatorId = MethodIdHelper.GetMethodId("getPricesInWeiWithAggregator(address)");
    private static readonly uint _getPricesInWeiId = MethodIdHelper.GetMethodId("getPricesInWei()");
    private static readonly uint _getPricesInArbGasWithAggregatorId = MethodIdHelper.GetMethodId("getPricesInArbGasWithAggregator(address)");
    private static readonly uint _getPricesInArbGasId = MethodIdHelper.GetMethodId("getPricesInArbGas()");
    private static readonly uint _getGasAccountingParamsId = MethodIdHelper.GetMethodId("getGasAccountingParams()");
    private static readonly uint _getL1FeesAvailableId = MethodIdHelper.GetMethodId("getL1FeesAvailable()");
    private static readonly uint _getL1RewardRateId = MethodIdHelper.GetMethodId("getL1RewardRate()");
    private static readonly uint _getL1RewardRecipientId = MethodIdHelper.GetMethodId("getL1RewardRecipient()");
    private static readonly uint _getL1PricingEquilibrationUnitsId = MethodIdHelper.GetMethodId("getL1PricingEquilibrationUnits()");
    private static readonly uint _getLastL1PricingUpdateTimeId = MethodIdHelper.GetMethodId("getLastL1PricingUpdateTime()");
    private static readonly uint _getL1PricingFundsDueForRewardsId = MethodIdHelper.GetMethodId("getL1PricingFundsDueForRewards()");
    private static readonly uint _getL1PricingUnitsSinceUpdateId = MethodIdHelper.GetMethodId("getL1PricingUnitsSinceUpdate()");
    private static readonly uint _getLastL1PricingSurplusId = MethodIdHelper.GetMethodId("getLastL1PricingSurplus()");
    private static readonly Dictionary<uint, Func<ArbitrumPrecompileExecutionContext, ReadOnlySpan<byte>, byte[]>> _methodIdToParsingFunction
        = new()
    {
        { _getPricesInWeiWithAggregatorId, GetPricesInWeiWithAggregator },
        { _getPricesInWeiId, GetPricesInWei },
        { _getPricesInArbGasWithAggregatorId, GetPricesInArbGasWithAggregator },
        { _getPricesInArbGasId, GetPricesInArbGas },
        { _getGasAccountingParamsId, GetGasAccountingParams },
        { MethodIdHelper.GetMethodId("getMinimumGasPrice()"), GetMinimumGasPrice },
        { MethodIdHelper.GetMethodId("getL1BaseFeeEstimate()"), GetL1BaseFeeEstimate },
        { MethodIdHelper.GetMethodId("getL1BaseFeeEstimateInertia()"), GetL1BaseFeeEstimateInertia },
        { _getL1RewardRateId, GetL1RewardRate },
        { _getL1RewardRecipientId, GetL1RewardRecipient },
        { MethodIdHelper.GetMethodId("getL1GasPriceEstimate()"), GetL1GasPriceEstimate },
        { MethodIdHelper.GetMethodId("getCurrentTxL1GasFees()"), GetCurrentTxL1GasFees },
        { MethodIdHelper.GetMethodId("getGasBacklog()"), GetGasBacklog },
        { MethodIdHelper.GetMethodId("getPricingInertia()"), GetPricingInertia },
        { MethodIdHelper.GetMethodId("getGasBacklogTolerance()"), GetGasBacklogTolerance },
        { MethodIdHelper.GetMethodId("getL1PricingSurplus()"), GetL1PricingSurplus },
        { MethodIdHelper.GetMethodId("getPerBatchGasCharge()"), GetPerBatchGasCharge },
        { MethodIdHelper.GetMethodId("getAmortizedCostCapBips()"), GetAmortizedCostCapBips },
        { _getL1FeesAvailableId, GetL1FeesAvailable },
        { _getL1PricingEquilibrationUnitsId, GetL1PricingEquilibrationUnits },
        { _getLastL1PricingUpdateTimeId, GetLastL1PricingUpdateTime },
        { _getL1PricingFundsDueForRewardsId, GetL1PricingFundsDueForRewards },
        { _getL1PricingUnitsSinceUpdateId, GetL1PricingUnitsSinceUpdate },
        { _getLastL1PricingSurplusId, GetLastL1PricingSurplus },
    };

    static ArbGasInfoParser()
    {
        // Not wrapped by OwnerWrapper<T> so we customize the functions here.
        CustomizeFunctionDescriptionsWithArbosVersion(PrecompileFunctions);
    }

    public static void CustomizeFunctionDescriptionsWithArbosVersion(IReadOnlyDictionary<uint, ArbitrumFunctionDescription> precompileFunctions)
    {
        precompileFunctions[_getL1FeesAvailableId].ArbOSVersion = ArbosVersion.Ten;
        precompileFunctions[_getL1RewardRateId].ArbOSVersion = ArbosVersion.Eleven;
        precompileFunctions[_getL1RewardRecipientId].ArbOSVersion = ArbosVersion.Eleven;
        precompileFunctions[_getL1PricingEquilibrationUnitsId].ArbOSVersion = ArbosVersion.Twenty;
        precompileFunctions[_getLastL1PricingUpdateTimeId].ArbOSVersion = ArbosVersion.Twenty;
        precompileFunctions[_getL1PricingFundsDueForRewardsId].ArbOSVersion = ArbosVersion.Twenty;
        precompileFunctions[_getL1PricingUnitsSinceUpdateId].ArbOSVersion = ArbosVersion.Twenty;
        precompileFunctions[_getLastL1PricingSurplusId].ArbOSVersion = ArbosVersion.Twenty;
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (_methodIdToParsingFunction.TryGetValue(methodId, out Func<ArbitrumPrecompileExecutionContext, ReadOnlySpan<byte>, byte[]>? function))
            return function(context, inputDataSpan);

        throw new ArgumentException($"Invalid precompile method ID: {methodId} for ArbGasInfo precompile");
    }

    private static byte[] GetPricesInWeiWithAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> aggregatorBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address aggregator = new(aggregatorBytes[(Hash256.Size - Address.Size)..]);

        ArbGasInfo.PricesInWei prices = ArbGasInfo.GetPricesInWeiWithAggregator(context, aggregator);

        AbiFunctionDescription function = PrecompileFunctions[_getPricesInWeiWithAggregatorId].AbiFunctionDescription;

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [prices.PerL2Tx, prices.WeiForL1Calldata, prices.WeiForL2Storage,
            prices.PerArbGasBase, prices.PerArbGasCongestion, prices.PerArbGasTotal]
        );

        return abiEncodedResult;
    }

    private static byte[] GetPricesInWei(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbGasInfo.PricesInWei prices = ArbGasInfo.GetPricesInWei(context);

        AbiFunctionDescription function = PrecompileFunctions[_getPricesInWeiId].AbiFunctionDescription;

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [prices.PerL2Tx, prices.WeiForL1Calldata, prices.WeiForL2Storage,
            prices.PerArbGasBase, prices.PerArbGasCongestion, prices.PerArbGasTotal]
        );

        return abiEncodedResult;
    }

    private static byte[] GetPricesInArbGasWithAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        ReadOnlySpan<byte> aggregatorBytes = ArbitrumBinaryReader.ReadBytesOrFail(ref inputData, Hash256.Size);
        Address aggregator = new(aggregatorBytes[(Hash256.Size - Address.Size)..]);

        ArbGasInfo.PricesInArbGas prices = ArbGasInfo.GetPricesInArbGasWithAggregator(context, aggregator);

        AbiFunctionDescription function = PrecompileFunctions[_getPricesInArbGasWithAggregatorId].AbiFunctionDescription;

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [prices.GasPerL2Tx, prices.GasForL1Calldata, prices.GasForL2Storage]
        );

        return abiEncodedResult;
    }

    private static byte[] GetPricesInArbGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbGasInfo.PricesInArbGas prices = ArbGasInfo.GetPricesInArbGas(context);

        AbiFunctionDescription function = PrecompileFunctions[_getPricesInArbGasId].AbiFunctionDescription;

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [prices.GasPerL2Tx, prices.GasForL1Calldata, prices.GasForL2Storage]
        );

        return abiEncodedResult;
    }

    private static byte[] GetGasAccountingParams(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbGasInfo.GasAccountingParams accountingParams = ArbGasInfo.GetGasAccountingParams(context);

        AbiFunctionDescription function = PrecompileFunctions[_getGasAccountingParamsId].AbiFunctionDescription;

        byte[] abiEncodedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [accountingParams.SpeedLimit, accountingParams.PoolSize, accountingParams.TxGasLimit]
        );

        return abiEncodedResult;
    }

    private static byte[] GetMinimumGasPrice(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetMinimumGasPrice(context).ToBigEndian();

    private static byte[] GetL1BaseFeeEstimate(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetL1BaseFeeEstimate(context).ToBigEndian();

    private static byte[] GetL1BaseFeeEstimateInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbGasInfo.GetL1BaseFeeEstimateInertia(context)).ToBigEndian();

    private static byte[] GetL1RewardRate(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbGasInfo.GetL1RewardRate(context)).ToBigEndian();

    private static byte[] GetL1RewardRecipient(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        Address l1RewardRecipient = ArbGasInfo.GetL1RewardRecipient(context);

        byte[] abiEncodedAddress = new byte[Hash256.Size];
        l1RewardRecipient.Bytes.CopyTo(abiEncodedAddress, Hash256.Size - Address.Size);

        return abiEncodedAddress;
    }

    private static byte[] GetL1GasPriceEstimate(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetL1GasPriceEstimate(context).ToBigEndian();

    private static byte[] GetCurrentTxL1GasFees(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetCurrentTxL1GasFees(context).ToBigEndian();

    private static byte[] GetGasBacklog(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbGasInfo.GetGasBacklog(context)).ToBigEndian();

    private static byte[] GetPricingInertia(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbGasInfo.GetPricingInertia(context)).ToBigEndian();

    private static byte[] GetGasBacklogTolerance(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbGasInfo.GetGasBacklogTolerance(context)).ToBigEndian();

    private static byte[] GetL1PricingSurplus(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        BigInteger l1PricingSurplus = ArbGasInfo.GetL1PricingSurplus(context);
        return l1PricingSurplus.ToBigEndianByteArray(outputLength: 32);
    }

    private static byte[] GetPerBatchGasCharge(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        long perBatchGasCharge = ArbGasInfo.GetPerBatchGasCharge(context);
        return ((BigInteger)perBatchGasCharge).ToBigEndianByteArray(outputLength: 32);
    }

    private static byte[] GetAmortizedCostCapBips(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbGasInfo.GetAmortizedCostCapBips(context)).ToBigEndian();

    private static byte[] GetL1FeesAvailable(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetL1FeesAvailable(context).ToBigEndian();

    private static byte[] GetL1PricingEquilibrationUnits(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetL1PricingEquilibrationUnits(context).ToBigEndian();

    private static byte[] GetLastL1PricingUpdateTime(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbGasInfo.GetLastL1PricingUpdateTime(context)).ToBigEndian();

    private static byte[] GetL1PricingFundsDueForRewards(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetL1PricingFundsDueForRewards(context).ToBigEndian();

    private static byte[] GetL1PricingUnitsSinceUpdate(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => new UInt256(ArbGasInfo.GetL1PricingUnitsSinceUpdate(context)).ToBigEndian();

    private static byte[] GetLastL1PricingSurplus(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        BigInteger l1PricingSurplus = ArbGasInfo.GetLastL1PricingSurplus(context);
        return l1PricingSurplus.ToBigEndianByteArray(outputLength: 32);
    }
}
