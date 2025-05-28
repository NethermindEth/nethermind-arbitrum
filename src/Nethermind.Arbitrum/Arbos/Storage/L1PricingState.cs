using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class L1PricingState
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

    public L1PricingState(ArbosStorage storage)
    {
        BatchPosterTable = new BatchPostersTable(storage.OpenSubStorage(BatchPosterTableKey));
        PayRewardsToStorage = new ArbosStorageBackedAddress(storage, PayRewardsToOffset);
        EquilibrationUnitsStorage = new ArbosStorageBackedUInt256(storage, EquilibrationUnitsOffset);
        InertiaStorage = new ArbosStorageBackedULong(storage, InertiaOffset);
        PerUnitRewardStorage = new ArbosStorageBackedULong(storage, PerUnitRewardOffset);
        LastUpdateTimeStorage = new ArbosStorageBackedULong(storage, LastUpdateTimeOffset);
        FundsDueForRewardsStorage = new ArbosStorageBackedUInt256(storage, FundsDueForRewardsOffset);
        UnitsSinceStorage = new ArbosStorageBackedULong(storage, UnitsSinceOffset);
        PricePerUnitStorage = new ArbosStorageBackedUInt256(storage, PricePerUnitOffset);
        LastSurplusStorage = new ArbosStorageBackedUInt256(storage, LastSurplusOffset);
        PerBatchGasCostStorage = new ArbosStorageBackedULong(storage, PerBatchGasCostOffset);
        AmortizedCostCapBipsStorage = new ArbosStorageBackedULong(storage, AmortizedCostCapBipsOffset);
        L1FeesAvailableStorage = new ArbosStorageBackedUInt256(storage, L1FeesAvailableOffset);
    }

    public BatchPostersTable BatchPosterTable { get; }
    public ArbosStorageBackedAddress PayRewardsToStorage { get; }
    public ArbosStorageBackedUInt256 EquilibrationUnitsStorage { get; }
    public ArbosStorageBackedULong InertiaStorage { get; }
    public ArbosStorageBackedULong PerUnitRewardStorage { get; }
    public ArbosStorageBackedULong LastUpdateTimeStorage { get; }
    public ArbosStorageBackedUInt256 FundsDueForRewardsStorage { get; }
    public ArbosStorageBackedULong UnitsSinceStorage { get; }
    public ArbosStorageBackedUInt256 PricePerUnitStorage { get; }
    public ArbosStorageBackedUInt256 LastSurplusStorage { get; }
    public ArbosStorageBackedULong PerBatchGasCostStorage { get; }
    public ArbosStorageBackedULong AmortizedCostCapBipsStorage { get; }
    public ArbosStorageBackedUInt256 L1FeesAvailableStorage { get; }

    public static void Initialize(ArbosStorage storage, Address initialRewardsRecipient, UInt256 initialL1BaseFee)
    {
        var bptStorage = storage.OpenSubStorage(BatchPosterTableKey);
        BatchPostersTable.Initialize(bptStorage);

        var bpTable = new BatchPostersTable(bptStorage);
        bpTable.AddPoster(ArbosAddresses.BatchPosterAddress, ArbosAddresses.BatchPosterPayToAddress);

        storage.SetByULong(PayRewardsToOffset, initialRewardsRecipient.ToHash2());

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

    public ulong AmortizedCostCapBips()
    {
        return AmortizedCostCapBipsStorage.Get();
    }

    public void SetEquilibrationUnits(ulong units)
    {
        EquilibrationUnitsStorage.Set(new UInt256(units));
    }
}
