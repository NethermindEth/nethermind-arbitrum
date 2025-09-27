using System.Numerics;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbGasInfoParser : IArbitrumPrecompile<ArbGasInfoParser>
{
    public static readonly ArbGasInfoParser Instance = new();

    public static Address Address { get; } = ArbGasInfo.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctions { get; }
        = AbiMetadata.GetAllFunctionDescriptions(ArbGasInfo.Abi);

    private static readonly uint _getPricesInWeiWithAggregatorId = PrecompileHelper.GetMethodId("getPricesInWeiWithAggregator(address)");
    private static readonly uint _getPricesInWeiId = PrecompileHelper.GetMethodId("getPricesInWei()");
    private static readonly uint _getPricesInArbGasWithAggregatorId = PrecompileHelper.GetMethodId("getPricesInArbGasWithAggregator(address)");
    private static readonly uint _getPricesInArbGasId = PrecompileHelper.GetMethodId("getPricesInArbGas()");
    private static readonly uint _getGasAccountingParamsId = PrecompileHelper.GetMethodId("getGasAccountingParams()");
    private static readonly uint _getL1RewardRateId = PrecompileHelper.GetMethodId("getL1RewardRate()");
    private static readonly uint _getL1RewardRecipientId = PrecompileHelper.GetMethodId("getL1RewardRecipient()");
    private static readonly uint _getL1FeesAvailableId = PrecompileHelper.GetMethodId("getL1FeesAvailable()");
    private static readonly uint _getL1PricingEquilibrationUnitsId = PrecompileHelper.GetMethodId("getL1PricingEquilibrationUnits()");
    private static readonly uint _getLastL1PricingUpdateTimeId = PrecompileHelper.GetMethodId("getLastL1PricingUpdateTime()");
    private static readonly uint _getL1PricingFundsDueForRewardsId = PrecompileHelper.GetMethodId("getL1PricingFundsDueForRewards()");
    private static readonly uint _getL1PricingUnitsSinceUpdateId = PrecompileHelper.GetMethodId("getL1PricingUnitsSinceUpdate()");
    private static readonly uint _getLastL1PricingSurplusId = PrecompileHelper.GetMethodId("getLastL1PricingSurplus()");
    private static readonly Dictionary<uint, Func<ArbitrumPrecompileExecutionContext, ReadOnlySpan<byte>, byte[]>> _methodIdToParsingFunction
        = new()
    {
        { _getPricesInWeiWithAggregatorId, GetPricesInWeiWithAggregator },
        { _getPricesInWeiId, GetPricesInWei },
        { _getPricesInArbGasWithAggregatorId, GetPricesInArbGasWithAggregator },
        { _getPricesInArbGasId, GetPricesInArbGas },
        { _getGasAccountingParamsId, GetGasAccountingParams },
        { PrecompileHelper.GetMethodId("getMinimumGasPrice()"), GetMinimumGasPrice },
        { PrecompileHelper.GetMethodId("getL1BaseFeeEstimate()"), GetL1BaseFeeEstimate },
        { PrecompileHelper.GetMethodId("getL1BaseFeeEstimateInertia()"), GetL1BaseFeeEstimateInertia },
        { _getL1RewardRateId, GetL1RewardRate },
        { _getL1RewardRecipientId, GetL1RewardRecipient },
        { PrecompileHelper.GetMethodId("getL1GasPriceEstimate()"), GetL1GasPriceEstimate },
        { PrecompileHelper.GetMethodId("getCurrentTxL1GasFees()"), GetCurrentTxL1GasFees },
        { PrecompileHelper.GetMethodId("getGasBacklog()"), GetGasBacklog },
        { PrecompileHelper.GetMethodId("getPricingInertia()"), GetPricingInertia },
        { PrecompileHelper.GetMethodId("getGasBacklogTolerance()"), GetGasBacklogTolerance },
        { PrecompileHelper.GetMethodId("getL1PricingSurplus()"), GetL1PricingSurplus },
        { PrecompileHelper.GetMethodId("getPerBatchGasCharge()"), GetPerBatchGasCharge },
        { PrecompileHelper.GetMethodId("getAmortizedCostCapBips()"), GetAmortizedCostCapBips },
        { _getL1FeesAvailableId, GetL1FeesAvailable },
        { _getL1PricingEquilibrationUnitsId, GetL1PricingEquilibrationUnits },
        { _getLastL1PricingUpdateTimeId, GetLastL1PricingUpdateTime },
        { _getL1PricingFundsDueForRewardsId, GetL1PricingFundsDueForRewards },
        { _getL1PricingUnitsSinceUpdateId, GetL1PricingUnitsSinceUpdate },
        { _getLastL1PricingSurplusId, GetLastL1PricingSurplus },
    };

    static ArbGasInfoParser()
    {
        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    public byte[] RunAdvanced(ArbitrumPrecompileExecutionContext context, ReadOnlyMemory<byte> inputData)
    {
        ReadOnlySpan<byte> inputDataSpan = inputData.Span;
        uint methodId = ArbitrumBinaryReader.ReadUInt32OrFail(ref inputDataSpan);

        if (_methodIdToParsingFunction.TryGetValue(methodId, out Func<ArbitrumPrecompileExecutionContext, ReadOnlySpan<byte>, byte[]>? function))
            return function(context, inputDataSpan);

        throw new ArgumentException($"Invalid precompile method ID: {methodId} for ArbGasInfo precompile");
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctions[_getL1FeesAvailableId].ArbOSVersion = ArbosVersion.Ten;
        PrecompileFunctions[_getL1RewardRateId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctions[_getL1RewardRecipientId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctions[_getL1PricingEquilibrationUnitsId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctions[_getLastL1PricingUpdateTimeId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctions[_getL1PricingFundsDueForRewardsId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctions[_getL1PricingUnitsSinceUpdateId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctions[_getLastL1PricingSurplusId].ArbOSVersion = ArbosVersion.Twenty;
    }

    private static byte[] GetPricesInWeiWithAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctions[_getPricesInWeiWithAggregatorId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address aggregator = (Address)decoded[0];
        ArbGasInfo.PricesInWei prices = ArbGasInfo.GetPricesInWeiWithAggregator(context, aggregator);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [prices.PerL2Tx, prices.WeiForL1Calldata, prices.WeiForL2Storage,
                prices.PerArbGasBase, prices.PerArbGasCongestion, prices.PerArbGasTotal]
        );
    }

    private static byte[] GetPricesInWei(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbGasInfo.PricesInWei prices = ArbGasInfo.GetPricesInWei(context);

        AbiFunctionDescription functionAbi = PrecompileFunctions[_getPricesInWeiId].AbiFunctionDescription;

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [prices.PerL2Tx, prices.WeiForL1Calldata, prices.WeiForL2Storage,
            prices.PerArbGasBase, prices.PerArbGasCongestion, prices.PerArbGasTotal]
        );
    }

    private static byte[] GetPricesInArbGasWithAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctions[_getPricesInArbGasWithAggregatorId].AbiFunctionDescription;

        object[] decoded = PrecompileAbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            functionAbi.GetCallInfo().Signature,
            inputData.ToArray()
        );

        Address aggregator = (Address)decoded[0];
        ArbGasInfo.PricesInArbGas prices = ArbGasInfo.GetPricesInArbGasWithAggregator(context, aggregator);

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [prices.GasPerL2Tx, prices.GasForL1Calldata, prices.GasForL2Storage]
        );
    }

    private static byte[] GetPricesInArbGas(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbGasInfo.PricesInArbGas prices = ArbGasInfo.GetPricesInArbGas(context);

        AbiFunctionDescription functionAbi = PrecompileFunctions[_getPricesInArbGasId].AbiFunctionDescription;

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [prices.GasPerL2Tx, prices.GasForL1Calldata, prices.GasForL2Storage]
        );
    }

    private static byte[] GetGasAccountingParams(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbGasInfo.GasAccountingParams accountingParams = ArbGasInfo.GetGasAccountingParams(context);

        AbiFunctionDescription functionAbi = PrecompileFunctions[_getGasAccountingParamsId].AbiFunctionDescription;

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [accountingParams.SpeedLimit, accountingParams.PoolSize, accountingParams.TxGasLimit]
        );
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

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctions[_getL1RewardRecipientId].AbiFunctionDescription.GetReturnInfo().Signature,
            l1RewardRecipient
        );
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
