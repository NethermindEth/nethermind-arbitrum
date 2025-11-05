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
    private const ulong PerTxGasLimitOffset = 7;

    private static readonly byte[] ConstraintsKey = [0];

    public const ulong InitialSpeedLimitPerSecondV0 = 1_000_000;
    public const ulong InitialPerBlockGasLimitV0 = 20 * 1_000_000;

    public const ulong InitialSpeedLimitPerSecondV6 = 7_000_000;
    public const ulong InitialPerBlockGasLimitV6 = 32 * 1_000_000;

    public const ulong InitialPerTxGasLimitV50 = 32 * 1_000_000;

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
    public ArbosStorageBackedULong PerTxGasLimitStorage { get; } = new(storage, PerTxGasLimitOffset);

    private readonly SubStorageVector _constraints = new(storage.OpenSubStorage(ConstraintsKey));

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

    public void SetMaxPerTxGasLimit(ulong limit)
    {
        PerTxGasLimitStorage.Set(limit);
    }

    public void AddToGasPool(long gas)
    {
        ulong backlog = GasBacklogStorage.Get();
        ulong newBacklog = gas > 0
            ? backlog.SaturateSub((ulong)gas)
            : backlog.SaturateAdd((ulong)(gas * -1));

        GasBacklogStorage.Set(newBacklog);
    }

    public void UpdatePricingModel(ulong timePassed)
    {
        ulong speedLimit = SpeedLimitPerSecondStorage.Get();

        AddToGasPool(timePassed.SaturateMul(speedLimit).ToLongSafe());

        ulong inertia = PricingInertiaStorage.Get();
        ulong tolerance = BacklogToleranceStorage.Get();
        ulong backlog = GasBacklogStorage.Get();
        UInt256 minBaseFee = MinBaseFeeWeiStorage.Get();

        UInt256 baseFee = minBaseFee;

        if (backlog > tolerance * speedLimit)
        {
            long excess = (backlog - tolerance * speedLimit).ToLongSafe();
            long exponentBips = excess * BipsMultiplier / inertia.SaturateMul(speedLimit).ToLongSafe();
            baseFee = minBaseFee * (UInt256)Utils.ApproxExpBasisPoints(exponentBips, 4) / BipsMultiplier;
        }

        BaseFeeWeiStorage.Set(baseFee);
    }

    public void SetBaseFeeWei(UInt256 baseFee)
    {
        BaseFeeWeiStorage.Set(baseFee);
    }

    public void SetMinBaseFeeWei(UInt256 priceInWei)
    {
        // This modifies the "minimum basefee" parameter, but doesn't modify the current basefee.
        // If this increases the minimum basefee, then the basefee might be below the minimum for a little while.
        // If so, the basefee will increase by up to a factor of two per block, until it reaches the minimum.
        MinBaseFeeWeiStorage.Set(priceInWei);
    }

    public void SetPricingInertia(ulong inertia)
    {
        PricingInertiaStorage.Set(inertia);
    }

    public void SetBacklogTolerance(ulong backlogTolerance)
    {
        BacklogToleranceStorage.Set(backlogTolerance);
    }

    /// <summary>
    /// Returns the number of gas constraints in storage.
    /// </summary>
    public ulong ConstraintsLength()
    {
        return _constraints.Length();
    }

    /// <summary>
    /// Opens the gas constraint at the given index.
    /// NOTE: This method does not verify bounds.
    /// </summary>
    public GasConstraint OpenConstraintAt(ulong index)
    {
        return new GasConstraint(_constraints.At(index));
    }

    /// <summary>
    /// Adds a new gas constraint with the specified target, adjustment window, and backlog.
    /// </summary>
    public void AddConstraint(ulong target, ulong adjustmentWindow, ulong backlog)
    {
        ArbosStorage subStorage = _constraints.Push();
        GasConstraint constraint = new(subStorage);
        constraint.SetTarget(target);
        constraint.SetAdjustmentWindow(adjustmentWindow);
        constraint.SetBacklog(backlog);
    }
}
