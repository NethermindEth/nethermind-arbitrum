// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Frozen;
using System.Numerics;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles.Parser;

public class ArbGasInfoParser : IArbitrumPrecompile<ArbGasInfoParser>
{
    public static readonly ArbGasInfoParser Instance = new();

    public static Address Address { get; } = ArbGasInfo.Address;

    public static IReadOnlyDictionary<uint, ArbitrumFunctionDescription> PrecompileFunctionDescription { get; }
        = Solgen.ArbGasInfo.Functions.All.ToFrozenDictionary(f => f.Key, f => f.Value.ToArbitrumFunctionDescription());

    public static FrozenDictionary<uint, PrecompileHandler> PrecompileImplementation { get; }

    private const uint GetPricesInWeiWithAggregatorId = Solgen.ArbGasInfo.Methods.GetPricesInWeiWithAggregator;
    private const uint GetPricesInWeiId = Solgen.ArbGasInfo.Methods.GetPricesInWei;
    private const uint GetPricesInArbGasWithAggregatorId = Solgen.ArbGasInfo.Methods.GetPricesInArbGasWithAggregator;
    private const uint GetPricesInArbGasId = Solgen.ArbGasInfo.Methods.GetPricesInArbGas;
    private const uint GetGasAccountingParamsId = Solgen.ArbGasInfo.Methods.GetGasAccountingParams;
    private const uint GetMinimumGasPriceId = Solgen.ArbGasInfo.Methods.GetMinimumGasPrice;
    private const uint GetL1BaseFeeEstimateId = Solgen.ArbGasInfo.Methods.GetL1BaseFeeEstimate;
    private const uint GetL1BaseFeeEstimateInertiaId = Solgen.ArbGasInfo.Methods.GetL1BaseFeeEstimateInertia;
    private const uint GetL1RewardRateId = Solgen.ArbGasInfo.Methods.GetL1RewardRate;
    private const uint GetL1RewardRecipientId = Solgen.ArbGasInfo.Methods.GetL1RewardRecipient;
    private const uint GetL1GasPriceEstimateId = Solgen.ArbGasInfo.Methods.GetL1GasPriceEstimate;
    private const uint GetCurrentTxL1GasFeesId = Solgen.ArbGasInfo.Methods.GetCurrentTxL1GasFees;
    private const uint GetGasBacklogId = Solgen.ArbGasInfo.Methods.GetGasBacklog;
    private const uint GetPricingInertiaId = Solgen.ArbGasInfo.Methods.GetPricingInertia;
    private const uint GetGasBacklogToleranceId = Solgen.ArbGasInfo.Methods.GetGasBacklogTolerance;
    private const uint GetMaxTxGasLimitId = Solgen.ArbGasInfo.Methods.GetMaxTxGasLimit;
    private const uint GetL1PricingSurplusId = Solgen.ArbGasInfo.Methods.GetL1PricingSurplus;
    private const uint GetPerBatchGasChargeId = Solgen.ArbGasInfo.Methods.GetPerBatchGasCharge;
    private const uint GetAmortizedCostCapBipsId = Solgen.ArbGasInfo.Methods.GetAmortizedCostCapBips;
    private const uint GetL1FeesAvailableId = Solgen.ArbGasInfo.Methods.GetL1FeesAvailable;
    private const uint GetL1PricingEquilibrationUnitsId = Solgen.ArbGasInfo.Methods.GetL1PricingEquilibrationUnits;
    private const uint GetLastL1PricingUpdateTimeId = Solgen.ArbGasInfo.Methods.GetLastL1PricingUpdateTime;
    private const uint GetL1PricingFundsDueForRewardsId = Solgen.ArbGasInfo.Methods.GetL1PricingFundsDueForRewards;
    private const uint GetL1PricingUnitsSinceUpdateId = Solgen.ArbGasInfo.Methods.GetL1PricingUnitsSinceUpdate;
    private const uint GetLastL1PricingSurplusId = Solgen.ArbGasInfo.Methods.GetLastL1PricingSurplus;
    private const uint GetMaxBlockGasLimitId = Solgen.ArbGasInfo.Methods.GetMaxBlockGasLimit;
    private const uint GetGasPricingConstraintsId = Solgen.ArbGasInfo.Methods.GetGasPricingConstraints;

    static ArbGasInfoParser()
    {
        PrecompileImplementation = new Dictionary<uint, PrecompileHandler>
        {
            { GetPricesInWeiWithAggregatorId, GetPricesInWeiWithAggregator },
            { GetPricesInWeiId, GetPricesInWei },
            { GetPricesInArbGasWithAggregatorId, GetPricesInArbGasWithAggregator },
            { GetPricesInArbGasId, GetPricesInArbGas },
            { GetGasAccountingParamsId, GetGasAccountingParams },
            { GetMinimumGasPriceId, GetMinimumGasPrice },
            { GetL1BaseFeeEstimateId, GetL1BaseFeeEstimate },
            { GetL1BaseFeeEstimateInertiaId, GetL1BaseFeeEstimateInertia },
            { GetL1RewardRateId, GetL1RewardRate },
            { GetL1RewardRecipientId, GetL1RewardRecipient },
            { GetL1GasPriceEstimateId, GetL1GasPriceEstimate },
            { GetCurrentTxL1GasFeesId, GetCurrentTxL1GasFees },
            { GetGasBacklogId, GetGasBacklog },
            { GetPricingInertiaId, GetPricingInertia },
            { GetGasBacklogToleranceId, GetGasBacklogTolerance },
            { GetMaxTxGasLimitId, GetMaxTxGasLimit },
            { GetL1PricingSurplusId, GetL1PricingSurplus },
            { GetPerBatchGasChargeId, GetPerBatchGasCharge },
            { GetAmortizedCostCapBipsId, GetAmortizedCostCapBips },
            { GetL1FeesAvailableId, GetL1FeesAvailable },
            { GetL1PricingEquilibrationUnitsId, GetL1PricingEquilibrationUnits },
            { GetLastL1PricingUpdateTimeId, GetLastL1PricingUpdateTime },
            { GetL1PricingFundsDueForRewardsId, GetL1PricingFundsDueForRewards },
            { GetL1PricingUnitsSinceUpdateId, GetL1PricingUnitsSinceUpdate },
            { GetLastL1PricingSurplusId, GetLastL1PricingSurplus },
            { GetMaxBlockGasLimitId, GetMaxBlockGasLimit },
            { GetGasPricingConstraintsId, GetGasPricingConstraints },
        }.ToFrozenDictionary();

        CustomizeFunctionDescriptionsWithArbosVersion();
    }

    private static void CustomizeFunctionDescriptionsWithArbosVersion()
    {
        PrecompileFunctionDescription[GetL1FeesAvailableId].ArbOSVersion = ArbosVersion.Ten;
        PrecompileFunctionDescription[GetL1RewardRateId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctionDescription[GetL1RewardRecipientId].ArbOSVersion = ArbosVersion.Eleven;
        PrecompileFunctionDescription[GetL1PricingEquilibrationUnitsId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[GetLastL1PricingUpdateTimeId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[GetL1PricingFundsDueForRewardsId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[GetL1PricingUnitsSinceUpdateId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[GetLastL1PricingSurplusId].ArbOSVersion = ArbosVersion.Twenty;
        PrecompileFunctionDescription[GetMaxTxGasLimitId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[GetMaxBlockGasLimitId].ArbOSVersion = ArbosVersion.Fifty;
        PrecompileFunctionDescription[GetGasPricingConstraintsId].ArbOSVersion = ArbosVersion.Fifty;
    }

    private static byte[] GetPricesInWeiWithAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetPricesInWeiWithAggregatorId].AbiFunctionDescription;

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

        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetPricesInWeiId].AbiFunctionDescription;

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [prices.PerL2Tx, prices.WeiForL1Calldata, prices.WeiForL2Storage,
            prices.PerArbGasBase, prices.PerArbGasCongestion, prices.PerArbGasTotal]
        );
    }

    private static byte[] GetPricesInArbGasWithAggregator(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> inputData)
    {
        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetPricesInArbGasWithAggregatorId].AbiFunctionDescription;

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

        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetPricesInArbGasId].AbiFunctionDescription;

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            functionAbi.GetReturnInfo().Signature,
            [prices.GasPerL2Tx, prices.GasForL1Calldata, prices.GasForL2Storage]
        );
    }

    private static byte[] GetGasAccountingParams(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ArbGasInfo.GasAccountingParams accountingParams = ArbGasInfo.GetGasAccountingParams(context);

        AbiFunctionDescription functionAbi = PrecompileFunctionDescription[GetGasAccountingParamsId].AbiFunctionDescription;

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
            PrecompileFunctionDescription[GetL1RewardRecipientId].AbiFunctionDescription.GetReturnInfo().Signature,
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

    private static byte[] GetMaxTxGasLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetMaxTxGasLimit(context).ToBigEndian();

    private static byte[] GetMaxBlockGasLimit(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
        => ArbGasInfo.GetMaxBlockGasLimit(context).ToBigEndian();

    private static byte[] GetGasPricingConstraints(ArbitrumPrecompileExecutionContext context, ReadOnlySpan<byte> _)
    {
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Convert ulong[][] to object[] for ABI encoding
        // Each constraint is an array of 3 ulongs: [target, adjustmentWindow, backlog]
        object[] constraintsObjects = new object[constraints.Length];
        for (int i = 0; i < constraints.Length; i++)
            constraintsObjects[i] = constraints[i];

        return PrecompileAbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            PrecompileFunctionDescription[GetGasPricingConstraintsId].AbiFunctionDescription.GetReturnInfo().Signature,
            constraintsObjects
        );
    }
}
