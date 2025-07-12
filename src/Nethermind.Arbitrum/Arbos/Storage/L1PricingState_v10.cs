using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Math;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.State;
using System.Numerics;

namespace Nethermind.Arbitrum.Arbos.Storage;

public partial class L1PricingState
{
    public void UpdateForBatchPosterSpending_v10(ulong updateTime, ulong currentTime, Address batchPosterAddress, BigInteger weiSpent, UInt256 l1BaseFee, ArbosState arbosState, IWorldState worldState, IReleaseSpec releaseSpec)
    {
        var currentArbosVersion = arbosState.CurrentArbosVersion;
        if (currentArbosVersion < ArbosVersion.Two)
        {
            UpdateForBatchPosterSpending_v2(updateTime, currentTime, batchPosterAddress, weiSpent, l1BaseFee,
                arbosState, worldState, releaseSpec);
            return;
        }

        var batchPoster = BatchPosterTable.OpenPoster(batchPosterAddress, true);

        var lastUpdateTime = LastUpdateTimeStorage.Get();
        if (lastUpdateTime == 0 && updateTime > 0)
        {
            // it's the first update, so there isn't a last update time
            lastUpdateTime = updateTime - 1;
        }

        if (updateTime > currentTime || updateTime < lastUpdateTime)
            throw new ArgumentException("Invalid time");

        var allocationNumerator = updateTime - lastUpdateTime;
        var allocationDenominator = currentTime - lastUpdateTime;

        if (allocationDenominator == 0)
        {
            allocationNumerator = allocationDenominator = 1;
        }

        var unitsSinceUpdate = UnitsSinceStorage.Get();
        ulong unitsAllocated = unitsSinceUpdate.SaturateMul(allocationNumerator) / allocationDenominator;
        unitsSinceUpdate -= unitsAllocated;
        UnitsSinceStorage.Set(unitsSinceUpdate);

        if (currentArbosVersion >= 3)
        {
            var amortizedCostCapBips = arbosState.L1PricingState.AmortizedCostCapBipsStorage.Get();

            if (amortizedCostCapBips > 0)
            {
                UInt256 weiSpentCap = Utils.BipsMultiplier *
                                  (l1BaseFee * unitsAllocated * amortizedCostCapBips);
                if (weiSpentCap < (UInt256)weiSpent)
                {
                    weiSpent = (BigInteger)weiSpentCap;
                }
            }
        }

        batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() + weiSpent);
        var fundsDueForRewards = FundsDueForRewardsStorage.Get() + unitsAllocated * PerUnitRewardStorage.Get();
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        var paymentForRewards = PerUnitRewardStorage.Get() * new UInt256(unitsAllocated);
        UInt256 availableFunds = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
        if (availableFunds < paymentForRewards)
            paymentForRewards = availableFunds;

        fundsDueForRewards -= paymentForRewards;
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        if (ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress, PayRewardsToStorage.Get(),
            paymentForRewards, arbosState, worldState, releaseSpec) != TransactionResult.Ok)
        {
            return;
        }

        availableFunds = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);

        var balanceToTransfer = batchPoster.GetFundsDue();
        if (availableFunds < (UInt256)balanceToTransfer)
            balanceToTransfer = (BigInteger)availableFunds;

        if (balanceToTransfer > 0)
        {
            if (ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress, batchPoster.GetPayTo(),
                    (UInt256)balanceToTransfer, arbosState, worldState, releaseSpec) != TransactionResult.Ok)
            {
                return;
            }
            batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() - balanceToTransfer);
        }

        LastUpdateTimeStorage.Set(updateTime);

        if (unitsAllocated > 0)
        {
            BigInteger totalFundsDue = BatchPosterTable.GetTotalFundsDue();
            BigInteger surplus = (BigInteger)worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress) - (totalFundsDue + (BigInteger)fundsDueForRewards);

            var inertiaUnits = EquilibrationUnitsStorage.Get() / InertiaStorage.Get();

            var allocPlusInert = inertiaUnits + unitsAllocated;
            var lastSurplus = LastSurplusStorage.Get();

            BigInteger desiredDerivative = BigInteger.Negate(surplus) / (BigInteger)EquilibrationUnitsStorage.Get();
            BigInteger actualDerivative = (surplus - lastSurplus) / unitsAllocated;
            BigInteger changeDerivativeBy = desiredDerivative - actualDerivative;
            BigInteger priceChange = (changeDerivativeBy * unitsAllocated) / (BigInteger)allocPlusInert;

            SetLastSurplus(surplus, arbosState.CurrentArbosVersion);
            var newPrice = (BigInteger)PricePerUnitStorage.Get() + priceChange;

            if (newPrice < 0)
                newPrice = 0;

            PricePerUnitStorage.Set((UInt256)newPrice);
        }
    }
}
