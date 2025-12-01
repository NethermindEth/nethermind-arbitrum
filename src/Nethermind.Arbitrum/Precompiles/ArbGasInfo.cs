using System.Numerics;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Precompiles;

public static class ArbGasInfo
{
    public const ulong AssumedSimpleTxSize = 140;

    public static readonly UInt256 StorageArbGas = ArbosStorage.StorageWriteCost;

    public static Address Address => ArbosAddresses.ArbGasInfoAddress;

    public static readonly string Abi =
        "[{\"inputs\":[],\"name\":\"getAmortizedCostCapBips\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getCurrentTxL1GasFees\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getGasAccountingParams\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getGasBacklog\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getGasBacklogTolerance\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1BaseFeeEstimate\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1BaseFeeEstimateInertia\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1FeesAvailable\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1GasPriceEstimate\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1PricingEquilibrationUnits\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1PricingFundsDueForRewards\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1PricingSurplus\",\"outputs\":[{\"internalType\":\"int256\",\"name\":\"\",\"type\":\"int256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1PricingUnitsSinceUpdate\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1RewardRate\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getL1RewardRecipient\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"\",\"type\":\"address\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getLastL1PricingSurplus\",\"outputs\":[{\"internalType\":\"int256\",\"name\":\"\",\"type\":\"int256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getLastL1PricingUpdateTime\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getMaxTxGasLimit\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getMinimumGasPrice\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getPerBatchGasCharge\",\"outputs\":[{\"internalType\":\"int64\",\"name\":\"\",\"type\":\"int64\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getPricesInArbGas\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"aggregator\",\"type\":\"address\"}],\"name\":\"getPricesInArbGasWithAggregator\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getPricesInWei\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[{\"internalType\":\"address\",\"name\":\"aggregator\",\"type\":\"address\"}],\"name\":\"getPricesInWeiWithAggregator\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"},{\"inputs\":[],\"name\":\"getPricingInertia\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"}]";

    // GetPricesInWeiWithAggregator gets  prices in wei when using the provided aggregator
    public static PricesInWei GetPricesInWeiWithAggregator(
        ArbitrumPrecompileExecutionContext context, Address aggregator)
    {
        if (context.ArbosState.CurrentArbosVersion < ArbosVersion.Four)
            return GetPricesInWeiWithAggregatorPreVersion4(context, aggregator);

        UInt256 l1GasPrice = context.ArbosState.L1PricingState.PricePerUnitStorage.Get();

        // aggregators compress calldata, so we must estimate accordingly
        UInt256 weiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;

        // the cost of a simple tx without calldata
        UInt256 perL2Tx = weiForL1Calldata * AssumedSimpleTxSize;

        UInt256 l2GasPrice = context.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();

        // nitro's compute-centric l2 gas pricing has no special compute component that rises independently
        UInt256 perArbGasBase = context.ArbosState.L2PricingState.MinBaseFeeWeiStorage.Get();
        if (l2GasPrice < perArbGasBase)
            perArbGasBase = l2GasPrice;

        UInt256 perArbGasCongestion = l2GasPrice - perArbGasBase;
        UInt256 perArbGasTotal = l2GasPrice;

        UInt256 weiForL2Storage = l2GasPrice * StorageArbGas;

        return new(perL2Tx, weiForL1Calldata, weiForL2Storage, perArbGasBase, perArbGasCongestion, perArbGasTotal);
    }

    // GetPricesInWei gets prices in wei when using the caller's preferred aggregator
    public static PricesInWei GetPricesInWei(ArbitrumPrecompileExecutionContext context)
        => GetPricesInWeiWithAggregator(context, Address.Zero);

    // GetPricesInArbGasWithAggregator gets prices in ArbGas when using the provided aggregator
    public static PricesInArbGas GetPricesInArbGasWithAggregator(
        ArbitrumPrecompileExecutionContext context, Address aggregator)
    {
        if (context.ArbosState.CurrentArbosVersion < ArbosVersion.Four)
            return GetPricesInArbGasWithAggregatorPreVersion4(context, aggregator);

        UInt256 l1GasPrice = context.ArbosState.L1PricingState.PricePerUnitStorage.Get();
        UInt256 l2GasPrice = context.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();

        // aggregators compress calldata, so we must estimate accordingly
        UInt256 weiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 weiPerL2Tx = weiForL1Calldata * AssumedSimpleTxSize;

        UInt256 gasForL1Calldata = l2GasPrice > 0 ? weiForL1Calldata / l2GasPrice : 0;
        UInt256 gasPerL2Tx = l2GasPrice > 0 ? weiPerL2Tx / l2GasPrice : 0;

        return new(gasPerL2Tx, gasForL1Calldata, StorageArbGas);
    }

    // GetPricesInArbGas gets prices in ArbGas when using the caller's preferred aggregator
    public static PricesInArbGas GetPricesInArbGas(ArbitrumPrecompileExecutionContext context)
        => GetPricesInArbGasWithAggregator(context, Address.Zero);

    // GetGasAccountingParams gets the rollup's speed limit, pool size, and tx gas limit
    public static GasAccountingParams GetGasAccountingParams(ArbitrumPrecompileExecutionContext context)
    {
        ulong speedLimit = context.ArbosState.L2PricingState.SpeedLimitPerSecondStorage.Get();
        ulong perBlockGasLimit = context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get();

        // Before ArbOS 50, the third return value was the block limit (for backward compatibility)
        // After ArbOS 50, return the actual per-tx limit
        ulong perTxGasLimit = context.ArbosState.CurrentArbosVersion < ArbosVersion.Fifty
            ? perBlockGasLimit
            : context.ArbosState.L2PricingState.PerTxGasLimitStorage.Get();

        return new(speedLimit, perBlockGasLimit, perTxGasLimit);
    }

    public static UInt256 GetMaxTxGasLimit(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L2PricingState.PerTxGasLimitStorage.Get();

    // GetMinimumGasPrice gets the minimum gas price needed for a transaction to succeed
    public static UInt256 GetMinimumGasPrice(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L2PricingState.MinBaseFeeWeiStorage.Get();

    // GetL1BaseFeeEstimate gets the current estimate of the L1 basefee
    public static UInt256 GetL1BaseFeeEstimate(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.PricePerUnitStorage.Get();

    // GetL1BaseFeeEstimateInertia gets how slowly ArbOS updates its estimate of the L1 basefee
    public static ulong GetL1BaseFeeEstimateInertia(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.InertiaStorage.Get();

    // GetL1RewardRate gets the L1 pricer reward rate
    public static ulong GetL1RewardRate(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.PerUnitRewardStorage.Get();

    // GetL1RewardRecipient gets the L1 pricer reward recipient
    public static Address GetL1RewardRecipient(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.PayRewardsToStorage.Get();

    // GetL1GasPriceEstimate gets the current estimate of the L1 basefee
    public static UInt256 GetL1GasPriceEstimate(ArbitrumPrecompileExecutionContext context)
        => GetL1BaseFeeEstimate(context);

    // GetCurrentTxL1GasFees gets the fee paid to the aggregator for posting this tx
    public static UInt256 GetCurrentTxL1GasFees(ArbitrumPrecompileExecutionContext context)
        => context.PosterFee;

    // GetGasBacklog gets the backlogged amount of gas burnt in excess of the speed limit
    public static ulong GetGasBacklog(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L2PricingState.GasBacklogStorage.Get();

    // GetPricingInertia gets how slowly ArbOS updates the L2 basefee in response to backlogged gas
    public static ulong GetPricingInertia(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L2PricingState.PricingInertiaStorage.Get();

    // GetGasBacklogTolerance gets the forgivable amount of backlogged gas ArbOS will ignore when raising the basefee
    public static ulong GetGasBacklogTolerance(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L2PricingState.BacklogToleranceStorage.Get();

    // GetL1PricingSurplus gets the surplus of funds for L1 batch posting payments (may be negative)
    public static BigInteger GetL1PricingSurplus(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.CurrentArbosVersion < ArbosVersion.Ten
            ? GetL1PricingSurplusPreVersion10(context)
            : context.ArbosState.L1PricingState.GetL1PricingSurplus();

    // GetPerBatchGasCharge gets the base charge (in L1 gas) attributed to each data batch in the calldata pricer
    public static long GetPerBatchGasCharge(ArbitrumPrecompileExecutionContext context)
        => (long)context.ArbosState.L1PricingState.PerBatchGasCostStorage.Get();

    // GetAmortizedCostCapBips gets the cost amortization cap in basis points
    public static ulong GetAmortizedCostCapBips(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.AmortizedCostCapBipsStorage.Get();

    // GetL1FeesAvailable gets the available funds from L1 fees
    public static UInt256 GetL1FeesAvailable(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.L1FeesAvailableStorage.Get();

    // GetL1PricingEquilibrationUnits gets the equilibration units parameter for L1 price adjustment algorithm
    public static UInt256 GetL1PricingEquilibrationUnits(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.EquilibrationUnitsStorage.Get();

    // GetLastL1PricingUpdateTime gets the last time the L1 calldata pricer was updated
    public static ulong GetLastL1PricingUpdateTime(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.LastUpdateTimeStorage.Get();

    // GetL1PricingFundsDueForRewards gets the amount of L1 calldata payments due for rewards (per the L1 reward rate)
    public static UInt256 GetL1PricingFundsDueForRewards(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.FundsDueForRewardsStorage.Get();

    // GetL1PricingUnitsSinceUpdate gets the amount of L1 calldata posted since the last update
    public static ulong GetL1PricingUnitsSinceUpdate(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.UnitsSinceStorage.Get();

    // GetLastL1PricingSurplus gets the L1 pricing surplus as of the last update (may be negative)
    public static BigInteger GetLastL1PricingSurplus(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L1PricingState.LastSurplusStorage.Get();

    // GetMaxBlockGasLimit gets the maximum gas limit for a block
    public static UInt256 GetMaxBlockGasLimit(ArbitrumPrecompileExecutionContext context)
        => context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get();

    /// <summary>
    /// Gets multi-constraint pricing info.
    /// Returns an array of constraints where each constraint contains:
    /// [gas_target_per_second, adjustment_window_seconds, backlog]
    /// </summary>
    public static ulong[][] GetGasPricingConstraints(ArbitrumPrecompileExecutionContext context)
    {
        ulong constraintsCount = context.ArbosState.L2PricingState.ConstraintsLength();
        ulong[][] constraints = new ulong[constraintsCount][];

        for (ulong i = 0; i < constraintsCount; i++)
        {
            GasConstraint constraint = context.ArbosState.L2PricingState.OpenConstraintAt(i);
            constraints[i] =
            [
                constraint.Target,
                constraint.AdjustmentWindow,
                constraint.Backlog
            ];
        }

        return constraints;
    }

    private static PricesInWei GetPricesInWeiWithAggregatorPreVersion4(
        ArbitrumPrecompileExecutionContext context, Address _)
    {
        UInt256 l1GasPrice = context.ArbosState.L1PricingState.PricePerUnitStorage.Get();

        // aggregators compress calldata, so we must estimate accordingly
        UInt256 weiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;

        // the cost of a simple tx without calldata
        UInt256 perL2Tx = weiForL1Calldata * AssumedSimpleTxSize;

        UInt256 l2GasPrice = context.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();

        // nitro's compute-centric l2 gas pricing has no special compute component that rises independently
        UInt256 perArbGasBase = l2GasPrice;
        UInt256 perArbGasCongestion = UInt256.Zero;
        UInt256 perArbGasTotal = l2GasPrice;

        UInt256 weiForL2Storage = l2GasPrice * StorageArbGas;

        return new(perL2Tx, weiForL1Calldata, weiForL2Storage, perArbGasBase, perArbGasCongestion, perArbGasTotal);
    }

    private static PricesInArbGas GetPricesInArbGasWithAggregatorPreVersion4(
        ArbitrumPrecompileExecutionContext context, Address _)
    {
        UInt256 l1GasPrice = context.ArbosState.L1PricingState.PricePerUnitStorage.Get();
        UInt256 l2GasPrice = context.BlockExecutionContext.GetEffectiveBaseFeeForGasCalculations();

        // aggregators compress calldata, so we must estimate accordingly
        UInt256 weiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;

        UInt256 gasForL1Calldata = l2GasPrice > 0 ? weiForL1Calldata / l2GasPrice : 0;

        return new(AssumedSimpleTxSize, gasForL1Calldata, StorageArbGas);
    }

    private static BigInteger GetL1PricingSurplusPreVersion10(ArbitrumPrecompileExecutionContext context)
    {
        BigInteger fundsDueForRefunds = context.ArbosState.L1PricingState.BatchPosterTable.GetTotalFundsDue();
        UInt256 fundsDueForRewards = context.ArbosState.L1PricingState.FundsDueForRewardsStorage.Get();

        BigInteger fundsNeeded = fundsDueForRefunds + (BigInteger)fundsDueForRewards;

        UInt256 fundsAvailable = context.WorldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);

        return (BigInteger)fundsAvailable - fundsNeeded;
    }

    public record struct PricesInWei(
        UInt256 PerL2Tx, UInt256 WeiForL1Calldata, UInt256 WeiForL2Storage,
        UInt256 PerArbGasBase, UInt256 PerArbGasCongestion, UInt256 PerArbGasTotal
    );

    public record struct PricesInArbGas(
        UInt256 GasPerL2Tx, UInt256 GasForL1Calldata, UInt256 GasForL2Storage
    );

    public record struct GasAccountingParams(UInt256 SpeedLimit, UInt256 PoolSize, UInt256 TxGasLimit);
}
