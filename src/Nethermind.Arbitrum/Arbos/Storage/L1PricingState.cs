using System.Numerics;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Math;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.State;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class L1PricingState(ArbosStorage storage)
{
    private const ulong PayRewardsToOffset = 0;
    private const ulong EquilibrationUnitsOffset = 1;
    private const ulong InertiaOffset = 2;
    private const ulong PerUnitRewardOffset = 3;
    private const ulong LastUpdateTimeOffset = 4;
    private const ulong FundsDueForRewardsOffset = 5;
    private const ulong UnitsSinceOffset = 6;
    private const ulong PricePerUnitOffset = 7;
    private const ulong LastSurplusOffset = 8;
    private const ulong PerBatchGasCostOffset = 9;
    private const ulong AmortizedCostCapBipsOffset = 10;
    private const ulong L1FeesAvailableOffset = 11;

    private static readonly byte[] BatchPosterTableKey = [0];

    private const ulong InitialInertia = 10;
    private const ulong InitialPerUnitReward = 10;
    public const ulong InitialPerBatchGasCostV6 = 100_000;
    public const ulong InitialPerBatchGasCostV12 = 210_000;

    public static readonly UInt256 InitialEquilibrationUnitsV0 = 60 * GasCostOf.TxDataNonZeroEip2028 * 100_000;
    public static readonly ulong InitialEquilibrationUnitsV6 = GasCostOf.TxDataNonZeroEip2028 * 10_000_000;

    public BatchPostersTable BatchPosterTable { get; } = new(storage.OpenSubStorage(BatchPosterTableKey));
    public ArbosStorageBackedAddress PayRewardsToStorage { get; } = new(storage, PayRewardsToOffset);
    public ArbosStorageBackedUInt256 EquilibrationUnitsStorage { get; } = new(storage, EquilibrationUnitsOffset);
    public ArbosStorageBackedULong InertiaStorage { get; } = new(storage, InertiaOffset);
    public ArbosStorageBackedULong PerUnitRewardStorage { get; } = new(storage, PerUnitRewardOffset);
    public ArbosStorageBackedULong LastUpdateTimeStorage { get; } = new(storage, LastUpdateTimeOffset);
    public ArbosStorageBackedUInt256 FundsDueForRewardsStorage { get; } = new(storage, FundsDueForRewardsOffset);
    public ArbosStorageBackedULong UnitsSinceStorage { get; } = new(storage, UnitsSinceOffset);
    public ArbosStorageBackedUInt256 PricePerUnitStorage { get; } = new(storage, PricePerUnitOffset);
    public ArbosStorageBackedUInt256 LastSurplusStorage { get; } = new(storage, LastSurplusOffset);
    public ArbosStorageBackedULong PerBatchGasCostStorage { get; } = new(storage, PerBatchGasCostOffset);
    public ArbosStorageBackedULong AmortizedCostCapBipsStorage { get; } = new(storage, AmortizedCostCapBipsOffset);
    public ArbosStorageBackedUInt256 L1FeesAvailableStorage { get; } = new(storage, L1FeesAvailableOffset);

    public static void Initialize(ArbosStorage storage, Address initialRewardsRecipient, UInt256 initialL1BaseFee)
    {
        var bptStorage = storage.OpenSubStorage(BatchPosterTableKey);
        BatchPostersTable.Initialize(bptStorage);

        var bpTable = new BatchPostersTable(bptStorage);
        bpTable.AddPoster(ArbosAddresses.BatchPosterAddress, ArbosAddresses.BatchPosterPayToAddress);

        storage.Set(PayRewardsToOffset, initialRewardsRecipient.ToHash());

        var equilibrationUnits = new ArbosStorageBackedUInt256(storage, EquilibrationUnitsOffset);
        equilibrationUnits.Set(InitialEquilibrationUnitsV0);

        var inertia = new ArbosStorageBackedULong(storage, InertiaOffset);
        inertia.Set(InitialInertia);

        var fundsDueForRewards = new ArbosStorageBackedUInt256(storage, FundsDueForRewardsOffset);
        fundsDueForRewards.Set(UInt256.Zero);

        var perUnitReward = new ArbosStorageBackedULong(storage, PerUnitRewardOffset);
        perUnitReward.Set(InitialPerUnitReward);

        var pricePerUnit = new ArbosStorageBackedUInt256(storage, PricePerUnitOffset);
        pricePerUnit.Set(initialL1BaseFee);
    }

    public void SetLastSurplus(UInt256 surplus)
    {
        LastSurplusStorage.Set(surplus);
    }

    public void SetPerBatchGasCost(ulong cost)
    {
        PerBatchGasCostStorage.Set(cost);
    }

    public void SetAmortizedCostCapBips(ulong bips)
    {
        AmortizedCostCapBipsStorage.Set(bips);
    }

    public void SetL1FeesAvailable(UInt256 fees)
    {
        L1FeesAvailableStorage.Set(fees);
    }

    public UInt256 AddToL1FeesAvailable(UInt256 delta)
    {
        var currentFees = L1FeesAvailableStorage.Get();
        var newFees = currentFees + delta;
        L1FeesAvailableStorage.Set(newFees);
        return newFees;
    }

    public ulong AmortizedCostCapBips()
    {
        return AmortizedCostCapBipsStorage.Get();
    }

    public void SetEquilibrationUnits(ulong units)
    {
        EquilibrationUnitsStorage.Set(new UInt256(units));
    }

    public void UpdateForBatchPosterSpending(ulong updateTime, ulong currentTime, Address batchPosterAddress, BigInteger weiSpent, UInt256 l1BaseFee, ArbosState arbosState, IWorldState worldState, IReleaseSpec releaseSpec)
    {
        var currentArbosVersion = arbosState.CurrentArbosVersion;
        if (currentArbosVersion < 10)
        {
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
        //TODO multiplication over ulong.Max ?
        var fundsDueForRewards = FundsDueForRewardsStorage.Get() + unitsAllocated * PerUnitRewardStorage.Get();
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        var l1FeesAvailable = L1FeesAvailableStorage.Get();
        var paymentForRewards = PerUnitRewardStorage.Get() * new UInt256(unitsAllocated);
        if (l1FeesAvailable < paymentForRewards)
            paymentForRewards = l1FeesAvailable;

        //TODO check underflow ?
        fundsDueForRewards -= paymentForRewards;
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        l1FeesAvailable = TransferFromL1FeesAvailable(PayRewardsToStorage.Get(), paymentForRewards, arbosState, worldState, releaseSpec);

        var balanceToTransfer = batchPoster.GetFundsDue();
        if (l1FeesAvailable < (UInt256)balanceToTransfer)
            balanceToTransfer = (BigInteger)l1FeesAvailable;

        if (balanceToTransfer > 0)
        {
            l1FeesAvailable = TransferFromL1FeesAvailable(PayRewardsToStorage.Get(), (UInt256)balanceToTransfer, arbosState, worldState, releaseSpec);
            batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() - balanceToTransfer);
        }

        LastUpdateTimeStorage.Set(updateTime);

        if (unitsAllocated > 0)
        {
            var totalFundsDue = BatchPosterTable.GetTotalFundsDue();
            var surplus = l1FeesAvailable - ((UInt256)totalFundsDue + fundsDueForRewards);

            var inertiaUnits = EquilibrationUnitsStorage.Get() / InertiaStorage.Get();

            var allocPlusInert = inertiaUnits * unitsSinceUpdate;
            var lastSurplus = LastSurplusStorage.Get();

            var desiredDerivative = surplus / EquilibrationUnitsStorage.Get();
            var actualDerivative = (surplus - lastSurplus) / unitsAllocated;
            var changeDerivativeBy = desiredDerivative - actualDerivative;
            var priceChange = (changeDerivativeBy * unitsAllocated) / allocPlusInert;

            LastSurplusStorage.Set(surplus);
            var newPrice = PricePerUnitStorage.Get() + priceChange;
            //TODO
            if (newPrice < 0)
                newPrice = 0;

            PricePerUnitStorage.Set(newPrice);
        }
    }

    public UInt256 TransferFromL1FeesAvailable(Address recipient, UInt256 amount, ArbosState arbosState, IWorldState worldState, IReleaseSpec releaseSpec)
    {
        var tr = ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress, recipient,
            amount, arbosState, worldState, releaseSpec);
        if (tr != TransactionResult.Ok)
            return 0;

        var l1FeesAvailable = L1FeesAvailableStorage.Get();
        if (amount > l1FeesAvailable)
            return 0;

        var l1FeesLeft = l1FeesAvailable - amount;
        L1FeesAvailableStorage.Set(l1FeesLeft);
        return l1FeesLeft;
    }
}
