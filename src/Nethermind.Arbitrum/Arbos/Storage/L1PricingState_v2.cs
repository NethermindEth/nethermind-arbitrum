using Nethermind.Arbitrum.Execution;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.State;
using System.Numerics;

namespace Nethermind.Arbitrum.Arbos.Storage;

public partial class L1PricingState
{
    public void UpdateForBatchPosterSpending_v2(ulong updateTime, ulong currentTime, Address batchPosterAddress, BigInteger weiSpent, UInt256 l1BaseFee, ArbosState arbosState, IWorldState worldState, IReleaseSpec releaseSpec)
    {
        var batchPoster = BatchPosterTable.OpenPoster(batchPosterAddress, true);

        var lastUpdateTime = LastUpdateTimeStorage.Get();
        if (lastUpdateTime == 0 && currentTime > 0)
        {
            // it's the first update, so there isn't a last update time
            lastUpdateTime = updateTime - 1;
        }

        if (updateTime >= currentTime || updateTime < lastUpdateTime)
            return;

        var allocationNumerator = updateTime - lastUpdateTime;
        var allocationDenominator = currentTime - lastUpdateTime;

        if (allocationDenominator == 0)
        {
            allocationNumerator = allocationDenominator = 1;
        }

        var unitsSinceUpdate = UnitsSinceStorage.Get();
        ulong unitsAllocated = unitsSinceUpdate * allocationNumerator / allocationDenominator;
        unitsSinceUpdate -= unitsAllocated;
        UnitsSinceStorage.Set(unitsSinceUpdate);

        BigInteger totalFundsDue = BatchPosterTable.GetTotalFundsDue();
        UInt256 oldSurplus = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress) -
                         ((UInt256)totalFundsDue + FundsDueForRewardsStorage.Get());

        batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() + weiSpent);
        var fundsDueForRewards = FundsDueForRewardsStorage.Get() + unitsAllocated * PerUnitRewardStorage.Get();
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        // allocate funds to this update
        var collectedSinceUpdate = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
        UInt256 availableFunds = (collectedSinceUpdate * allocationNumerator) / allocationDenominator;

        // pay rewards, as much as possible
        var paymentForRewards = PerUnitRewardStorage.Get() * new UInt256(unitsAllocated);
        if (availableFunds < paymentForRewards)
            paymentForRewards = availableFunds;

        fundsDueForRewards -= paymentForRewards;
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        if (ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress, PayRewardsToStorage.Get(),
            paymentForRewards, arbosState, worldState, releaseSpec) != TransactionResult.Ok)
        {
            return;
        }

        availableFunds -= paymentForRewards;

        // settle up our batch poster payments owed, as much as possible
        var allPosterAddrs = BatchPosterTable.GetAllPosters(ulong.MaxValue);
        foreach (var posterAddr in allPosterAddrs)
        {
            var innerBatchPoster = BatchPosterTable.OpenPoster(posterAddr, true);
            var balanceToTransfer = innerBatchPoster.GetFundsDue();

            if (availableFunds < (UInt256)balanceToTransfer)
                balanceToTransfer = (BigInteger)availableFunds;

            if (balanceToTransfer > 0)
            {
                if (ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress, innerBatchPoster.GetPayTo(),
                        (UInt256)balanceToTransfer, arbosState, worldState, releaseSpec) != TransactionResult.Ok)
                {
                    return;
                }
                availableFunds -= (UInt256)balanceToTransfer;
                innerBatchPoster.SetFundsDueSaturating(innerBatchPoster.GetFundsDue() - balanceToTransfer);
            }
        }

        LastUpdateTimeStorage.Set(updateTime);

        if (unitsAllocated > 0)
        {
            totalFundsDue = BatchPosterTable.GetTotalFundsDue();
            BigInteger surplus = (BigInteger)worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress) - (totalFundsDue + (BigInteger)fundsDueForRewards);

            BigInteger equilUnits = (BigInteger)EquilibrationUnitsStorage.Get();
            var inertiaUnits = equilUnits / InertiaStorage.Get();

            var allocPlusInert = inertiaUnits + unitsAllocated;
            BigInteger priceChange = (surplus * (equilUnits - BigInteger.One) - (BigInteger)oldSurplus * equilUnits) /
                                     (equilUnits * allocPlusInert);

            var newPrice = (BigInteger)PricePerUnitStorage.Get() + priceChange;

            if (newPrice < 0)
                newPrice = 0;

            PricePerUnitStorage.Set((UInt256)newPrice);
        }
    }
}
