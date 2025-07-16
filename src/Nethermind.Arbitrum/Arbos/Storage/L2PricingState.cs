using Nethermind.Arbitrum.Math;
using Nethermind.Core;
using Nethermind.Int256;

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

    public const long BipsMultiplier = 10_000;

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
        storage.Set(SpeedLimitPerSecondOffset, InitialSpeedLimitPerSecondV0);
        storage.Set(PerBlockGasLimitOffset, InitialPerBlockGasLimitV0);
        storage.Set(BaseFeeWeiOffset, InitialBaseFeeWei);
        storage.Set(GasBacklogOffset, 0);
        storage.Set(PricingInertiaOffset, InitialPricingInertia);
        storage.Set(BacklogToleranceOffset, InitialBacklogTolerance);
        storage.Set(MinBaseFeeWeiOffset, InitialMinimumBaseFeeWei);
    }

    public void SetSpeedLimitPerSecond(ulong limit)
    {
        SpeedLimitPerSecondStorage.Set(limit);
    }

    public void SetMaxPerBlockGasLimit(ulong limit)
    {
        PerBlockGasLimitStorage.Set(limit);
    }

    public void AddToGasPool(long gas)
    {
        var backlog = GasBacklogStorage.Get();

        if (gas > 0)
        {
            backlog = backlog.SaturateSub((ulong)gas);
        }
        else
        {
            backlog = backlog.SaturateAdd((ulong)(gas * -1));
        }

        GasBacklogStorage.Set(backlog);
    }
    public void UpdatePricingModel(ulong timePassed)
    {
        var speedLimit = SpeedLimitPerSecondStorage.Get();

        AddToGasPool((long)timePassed.SaturateMul(speedLimit));

        var inertia = PricingInertiaStorage.Get();
        var tolerance = BacklogToleranceStorage.Get();
        var backlog = GasBacklogStorage.Get();
        var minBaseFee = MinBaseFeeWeiStorage.Get();

        var baseFee = minBaseFee;

        if (backlog > tolerance * speedLimit)
        {
            var excess = (long)(backlog - tolerance * speedLimit);
            var exponentBips = (excess * BipsMultiplier) / (long)inertia.SaturateMul(speedLimit);
            baseFee = minBaseFee * (UInt256)Utils.ApproxExpBasisPoints(exponentBips, 4) / BipsMultiplier;
        }

        BaseFeeWeiStorage.Set(baseFee);
    }
}
