using Nethermind.Core;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class L2PricingState(ArbosStorage storage)
{
    private const ulong SpeedLimitPerSecondOffset = 0;
    private const ulong PerBlockGasLimitOffset = 1;
    private const ulong BaseFeeWeiOffset = 2;
    private const ulong MinBaseFeeWeiOffset = 3;
    private const ulong GasBacklogOffset = 4;
    private const ulong PricingInertiaOffset = 5;
    private const ulong BacklogToleranceOffset = 6;

    public const ulong InitialSpeedLimitPerSecondV0 = 1_000_000;
    public const ulong InitialPerBlockGasLimitV0 = 20 * 1_000_000;

    public const ulong InitialSpeedLimitPerSecondV6 = 7_000_000;
    public const ulong InitialPerBlockGasLimitV6 = 32 * 1_000_000;

    // params.GWei / 10 = 10^9 / 10 = 10^8 = 100_000_000
    public static readonly ulong InitialMinimumBaseFeeWei = (ulong)(Unit.GWei / 10);
    public static readonly ulong InitialBaseFeeWei = InitialMinimumBaseFeeWei;
    public const ulong InitialPricingInertia = 102;
    public const ulong InitialBacklogTolerance = 10;

    public ArbosStorageBackedULong SpeedLimitPerSecondStorage { get; } = new(storage, SpeedLimitPerSecondOffset);
    public ArbosStorageBackedULong PerBlockGasLimitStorage { get; } = new(storage, PerBlockGasLimitOffset);
    public ArbosStorageBackedUInt256 BaseFeeWeiStorage { get; } = new(storage, BaseFeeWeiOffset);
    public ArbosStorageBackedUInt256 MinBaseFeeWeiStorage { get; } = new(storage, MinBaseFeeWeiOffset);
    public ArbosStorageBackedULong GasBacklogStorage { get; } = new(storage, GasBacklogOffset);
    public ArbosStorageBackedULong PricingInertiaStorage { get; } = new(storage, PricingInertiaOffset);
    public ArbosStorageBackedULong BacklogToleranceStorage { get; } = new(storage, BacklogToleranceOffset);

    public static void Initialize(ArbosStorage storage)
    {
        storage.SetULongByULong(SpeedLimitPerSecondOffset, InitialSpeedLimitPerSecondV0);
        storage.SetULongByULong(PerBlockGasLimitOffset, InitialPerBlockGasLimitV0);
        storage.SetULongByULong(BaseFeeWeiOffset, InitialBaseFeeWei);
        storage.SetULongByULong(GasBacklogOffset, 0);
        storage.SetULongByULong(PricingInertiaOffset, InitialPricingInertia);
        storage.SetULongByULong(BacklogToleranceOffset, InitialBacklogTolerance);
        storage.SetULongByULong(MinBaseFeeWeiOffset, InitialMinimumBaseFeeWei);
    }

    public void SetSpeedLimitPerSecond(ulong limit)
    {
        SpeedLimitPerSecondStorage.Set(limit);
    }

    public void SetMaxPerBlockGasLimit(ulong limit)
    {
        PerBlockGasLimitStorage.Set(limit);
    }
}
