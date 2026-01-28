using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Math;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using System.Numerics;
using Nethermind.Arbitrum.State;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Arbos.Storage;

public partial class L1PricingState
{
    public ArbosStorageUpdateResult UpdateForBatchPosterSpending_v10(ulong updateTime, ulong currentTime, Address batchPosterAddress, BigInteger weiSpent, UInt256 l1BaseFee, ArbosState arbosState, IArbitrumWorldState worldState, IReleaseSpec releaseSpec, TracingInfo? tracingInfo)
    {
        ulong currentArbosVersion = arbosState.CurrentArbosVersion;
        if (currentArbosVersion < ArbosVersion.Two)
        {
            return UpdateForBatchPosterSpending_v2(updateTime, currentTime, batchPosterAddress, weiSpent, l1BaseFee,
                arbosState, worldState, releaseSpec, tracingInfo);
        }

        BatchPostersTable.BatchPoster batchPoster = BatchPosterTable.OpenPoster(batchPosterAddress, true);

        ulong lastUpdateTime = LastUpdateTimeStorage.Get();
        if (lastUpdateTime == 0 && updateTime > 0)
        {
            // it's the first update, so there isn't a last update time
            lastUpdateTime = updateTime - 1;
        }

        if (updateTime > currentTime || updateTime < lastUpdateTime)
            return ArbosStorageUpdateResult.InvalidTime;

        ulong allocationNumerator = updateTime - lastUpdateTime;
        ulong allocationDenominator = currentTime - lastUpdateTime;

        if (allocationDenominator == 0)
        {
            allocationNumerator = allocationDenominator = 1;
        }

        ulong unitsSinceUpdate = UnitsSinceStorage.Get();
        ulong unitsAllocated = unitsSinceUpdate.SaturateMul(allocationNumerator) / allocationDenominator;
        unitsSinceUpdate -= unitsAllocated;
        UnitsSinceStorage.Set(unitsSinceUpdate);

        if (currentArbosVersion >= ArbosVersion.Three)
        {
            ulong amortizedCostCapBips = arbosState.L1PricingState.AmortizedCostCapBipsStorage.Get();

            if (amortizedCostCapBips > 0)
            {
                UInt256 weiSpentCap = (l1BaseFee * unitsAllocated * amortizedCostCapBips) / Utils.BipsMultiplier;
                if (weiSpentCap < (UInt256)weiSpent)
                {
                    // apply the cap on assignment of amortized cost;
                    // the difference will be a loss for the batch poster
                    weiSpent = (BigInteger)weiSpentCap;
                }
            }
        }

        batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() + weiSpent);
        UInt256 fundsDueForRewards = FundsDueForRewardsStorage.Get() + unitsAllocated * PerUnitRewardStorage.Get();
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        UInt256 paymentForRewards = PerUnitRewardStorage.Get() * new UInt256(unitsAllocated);
        UInt256 availableFunds = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);
        if (availableFunds < paymentForRewards)
            paymentForRewards = availableFunds;

        fundsDueForRewards -= paymentForRewards;
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        TransactionResult tr = ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress,
            PayRewardsToStorage.Get(),
            paymentForRewards, arbosState, worldState, releaseSpec, tracingInfo, BalanceChangeReason.BalanceChangeTransferBatchPosterReward);

        if (tr != TransactionResult.Ok)
            return new ArbosStorageUpdateResult(tr.ErrorDescription);

        availableFunds = worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress);

        BigInteger balanceToTransfer = batchPoster.GetFundsDue();
        if (availableFunds < (UInt256)balanceToTransfer)
            balanceToTransfer = (BigInteger)availableFunds;

        if (balanceToTransfer > 0)
        {
            tr = ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress,
                batchPoster.GetPayTo(),
                (UInt256)balanceToTransfer, arbosState, worldState, releaseSpec, tracingInfo, BalanceChangeReason.BalanceChangeTransferBatchPosterReward);

            if (tr != TransactionResult.Ok)
                return new ArbosStorageUpdateResult(tr.ErrorDescription);

            batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() - balanceToTransfer);
        }

        LastUpdateTimeStorage.Set(updateTime);

        if (unitsAllocated > 0)
        {
            BigInteger totalFundsDue = BatchPosterTable.GetTotalFundsDue();
            BigInteger surplus = (BigInteger)worldState.GetBalance(ArbosAddresses.L1PricerFundsPoolAddress) - (totalFundsDue + (BigInteger)fundsDueForRewards);

            UInt256 equilibrationUnits = EquilibrationUnitsStorage.Get();
            UInt256 inertiaUnits = equilibrationUnits / InertiaStorage.Get();

            UInt256 allocPlusInert = inertiaUnits + unitsAllocated;
            BigInteger lastSurplus = LastSurplusStorage.Get();

            BigInteger desiredDerivative = Utils.FloorDiv(BigInteger.Negate(surplus), (BigInteger)equilibrationUnits);
            BigInteger actualDerivative = Utils.FloorDiv(surplus - lastSurplus, unitsAllocated);
            BigInteger changeDerivativeBy = desiredDerivative - actualDerivative;
            BigInteger priceChange = Utils.FloorDiv(changeDerivativeBy * unitsAllocated, (BigInteger)allocPlusInert);

            SetLastSurplus(surplus, arbosState.CurrentArbosVersion);
            BigInteger newPrice = (BigInteger)PricePerUnitStorage.Get() + priceChange;

            if (newPrice < 0)
                newPrice = 0;

            PricePerUnitStorage.Set((UInt256)newPrice);
        }
        return ArbosStorageUpdateResult.Ok;
    }
}
