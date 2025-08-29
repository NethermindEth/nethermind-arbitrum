using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using System.Numerics;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Arbos.Storage;

public partial class L1PricingState
{
    public ArbosStorageUpdateResult UpdateForBatchPosterSpending_v2(ulong updateTime, ulong currentTime, Address batchPosterAddress, BigInteger weiSpent, UInt256 l1BaseFee, ArbosState arbosState, IWorldState worldState, IReleaseSpec releaseSpec, TracingInfo? tracingInfo)
    {
        var batchPoster = BatchPosterTable.OpenPoster(batchPosterAddress, true);

        var lastUpdateTime = LastUpdateTimeStorage.Get();
        if (lastUpdateTime == 0 && currentTime > 0)
        {
            // it's the first update, so there isn't a last update time
            lastUpdateTime = updateTime - 1;
        }

        if (updateTime >= currentTime || updateTime < lastUpdateTime)
            return ArbosStorageUpdateResult.Ok;

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
        UInt256 fundsDueForRewards = FundsDueForRewardsStorage.Get();

        UInt256 oldSurplus = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress) -
                         ((UInt256)totalFundsDue + fundsDueForRewards);

        batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() + weiSpent);

        ulong perUnitReward = PerUnitRewardStorage.Get();
        fundsDueForRewards += unitsAllocated * perUnitReward;
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        // allocate funds to this update
        UInt256 collectedSinceUpdate = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
        UInt256 availableFunds = collectedSinceUpdate * allocationNumerator / allocationDenominator;

        // pay rewards, as much as possible
        UInt256 paymentForRewards = perUnitReward * new UInt256(unitsAllocated);
        if (availableFunds < paymentForRewards)
            paymentForRewards = availableFunds;

        fundsDueForRewards -= paymentForRewards;
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        var tr = ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress,
            PayRewardsToStorage.Get(),
            paymentForRewards, arbosState, worldState, releaseSpec, tracingInfo);

        if (tr != TransactionResult.Ok)
            return new ArbosStorageUpdateResult(tr.Error);

        availableFunds -= paymentForRewards;

        // settle up our batch poster payments owed, as much as possible
        var allPosterAddrs = BatchPosterTable.GetAllPosters(ulong.MaxValue);
        foreach (var posterAddr in allPosterAddrs)
        {
            var innerBatchPoster = BatchPosterTable.OpenPoster(posterAddr, false);
            BigInteger balanceToTransfer = innerBatchPoster.GetFundsDue();

            if (availableFunds < (UInt256)balanceToTransfer)
                balanceToTransfer = (BigInteger)availableFunds;

            if (balanceToTransfer > 0)
            {
                tr = ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress,
                    innerBatchPoster.GetPayTo(),
                    (UInt256)balanceToTransfer, arbosState, worldState, releaseSpec, tracingInfo);

                if (tr != TransactionResult.Ok)
                    return new ArbosStorageUpdateResult(tr.Error);

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
            BigInteger inertiaUnits = Utils.FloorDiv(equilUnits, InertiaStorage.Get());

            BigInteger allocPlusInert = inertiaUnits + unitsAllocated;
            BigInteger priceChange =
                Utils.FloorDiv(surplus * (equilUnits - BigInteger.One) - (BigInteger)oldSurplus * equilUnits,
                    equilUnits * allocPlusInert);

            var newPrice = (BigInteger)PricePerUnitStorage.Get() + priceChange;

            if (newPrice < 0)
                newPrice = 0;

            PricePerUnitStorage.Set((UInt256)newPrice);
        }
        return ArbosStorageUpdateResult.Ok;
    }
}
