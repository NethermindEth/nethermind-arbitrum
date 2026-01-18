using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Math;
using Nethermind.Core;
using Nethermind.Int256;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Test")]

namespace Nethermind.Arbitrum.Arbos.Storage;

public class L2PricingState(ArbosStorage storage, ulong currentArbosVersion)
{
    public const long BipsMultiplier = 10_000;
    public const int GasConstraintsMaxNum = 20;
    public const ulong InitialBacklogTolerance = 10;
    public const ulong InitialPerBlockGasLimitV0 = 20 * 1_000_000;
    public const ulong InitialPerBlockGasLimitV6 = 32 * 1_000_000;
    public const ulong InitialPerTxGasLimit = 32_000_000; // ArbOS 50
    public const ulong InitialPricingInertia = 102;

    public const ulong InitialSpeedLimitPerSecondV0 = 1_000_000;

    public const ulong InitialSpeedLimitPerSecondV6 = 7_000_000;
    public static readonly ulong InitialBaseFeeWei = InitialMinimumBaseFeeWei;

    // params.GWei / 10 = 10^9 / 10 = 10^8 = 100_000_000
    public static readonly ulong InitialMinimumBaseFeeWei = (ulong)(Unit.GWei / 10);
    private const ulong BacklogToleranceOffset = 6;
    private const ulong BaseFeeWeiOffset = 2;
    private const ulong GasBacklogOffset = 4;
    private const ulong MinBaseFeeWeiOffset = 3;
    private const ulong PerBlockGasLimitOffset = 1;
    private const ulong PerTxGasLimitOffset = 7;
    private const ulong PricingInertiaOffset = 5;
    private const ulong SpeedLimitPerSecondOffset = 0;

    private static readonly byte[] ConstraintsKey = [0];

    private readonly SubStorageVector _constraints = new(storage.OpenSubStorage(ConstraintsKey));
    public ArbosStorageBackedULong BacklogToleranceStorage { get; } = new(storage, BacklogToleranceOffset);
    public ArbosStorageBackedUInt256 BaseFeeWeiStorage { get; } = new(storage, BaseFeeWeiOffset);

    public ulong CurrentArbosVersion { get; internal set; } = currentArbosVersion;
    public ArbosStorageBackedULong GasBacklogStorage { get; } = new(storage, GasBacklogOffset);
    public ArbosStorageBackedUInt256 MinBaseFeeWeiStorage { get; } = new(storage, MinBaseFeeWeiOffset);
    public ArbosStorageBackedULong PerBlockGasLimitStorage { get; } = new(storage, PerBlockGasLimitOffset);
    public ArbosStorageBackedULong PerTxGasLimitStorage { get; } = new(storage, PerTxGasLimitOffset);
    public ArbosStorageBackedULong PricingInertiaStorage { get; } = new(storage, PricingInertiaOffset);

    public ArbosStorageBackedULong SpeedLimitPerSecondStorage { get; } = new(storage, SpeedLimitPerSecondOffset);

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

    /// <summary>
    /// Adds gas to the gas pool. Negative gas increases the backlog, positive gas decreases it.
    /// Routes to either legacy or multi-constraint implementation based on ArbOS version and constraint configuration.
    /// </summary>
    public void AddToGasPool(long gas)
    {
        if (ShouldUseGasConstraints())
            AddToGasPoolMultiConstraints(gas);
        else
            AddToGasPoolLegacy(gas);
    }

    /// <summary>
    /// Clears all gas constraints from storage.
    /// </summary>
    public void ClearConstraints()
    {
        ulong length = ConstraintsLength();
        for (ulong i = 0; i < length; i++)
        {
            ArbosStorage subStorage = _constraints.Pop();
            GasConstraint constraint = new(subStorage);
            constraint.Clear();
        }
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

    public void SetBacklogTolerance(ulong backlogTolerance)
    {
        BacklogToleranceStorage.Set(backlogTolerance);
    }

    public void SetBaseFeeWei(UInt256 baseFee)
    {
        BaseFeeWeiStorage.Set(baseFee);
    }

    /// <summary>
    /// Sets the gas backlog directly (used by a single-constraint pricing model only).
    /// </summary>
    public void SetGasBacklog(ulong backlog) => GasBacklogStorage.Set(backlog);

    public void SetMaxPerBlockGasLimit(ulong limit)
    {
        PerBlockGasLimitStorage.Set(limit);
    }

    public void SetMaxPerTxGasLimit(ulong limit)
    {
        PerTxGasLimitStorage.Set(limit);
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

    public void SetSpeedLimitPerSecond(ulong limit)
    {
        SpeedLimitPerSecondStorage.Set(limit);
    }

    /// <summary>
    /// Returns true if multi-constraint pricing should be used.
    /// Multi-constraint pricing is used when ArbOS version >= 50 and at least one constraint is configured.
    /// </summary>
    public bool ShouldUseGasConstraints()
        => CurrentArbosVersion >= ArbosVersion.MultiConstraintPricing && ConstraintsLength() > 0;

    /// <summary>
    /// Updates the pricing model based on time passed.
    /// Routes to either legacy or multi-constraint implementation based on ArbOS version and constraint configuration.
    /// </summary>
    public void UpdatePricingModel(ulong timePassed)
    {
        if (ShouldUseGasConstraints())
            UpdatePricingModelMultiConstraints(timePassed);
        else
            UpdatePricingModelLegacy(timePassed);
    }

    private static ulong ApplyGasDelta(ulong backlog, long gas)
    {
        return gas > 0
            ? backlog.SaturateSub((ulong)gas)
            : backlog.SaturateAdd((ulong)(-gas));
    }

    private void AddToGasPoolLegacy(long gas)
    {
        ulong backlog = GasBacklogStorage.Get();
        ulong newBacklog = ApplyGasDelta(backlog, gas);
        GasBacklogStorage.Set(newBacklog);
    }

    private void AddToGasPoolMultiConstraints(long gas)
    {
        ulong constraintsLength = ConstraintsLength();
        for (ulong i = 0; i < constraintsLength; i++)
        {
            GasConstraint constraint = OpenConstraintAt(i);
            ulong backlog = constraint.Backlog;
            ulong newBacklog = ApplyGasDelta(backlog, gas);
            constraint.SetBacklog(newBacklog);
        }
    }

    private void UpdatePricingModelLegacy(ulong timePassed)
    {
        ulong speedLimit = SpeedLimitPerSecondStorage.Get();

        AddToGasPoolLegacy(timePassed.SaturateMul(speedLimit).ToLongSafe());

        ulong inertia = PricingInertiaStorage.Get();
        ulong tolerance = BacklogToleranceStorage.Get();
        ulong backlog = GasBacklogStorage.Get();
        UInt256 minBaseFee = MinBaseFeeWeiStorage.Get();

        UInt256 baseFee = minBaseFee;

        if (backlog > tolerance * speedLimit)
        {
            ulong excess = backlog - tolerance * speedLimit;
            long exponentBips = excess.SaturateMul(Utils.BipsMultiplier).ToLongSafe() / inertia.SaturateMul(speedLimit).ToLongSafe();
            baseFee = minBaseFee * (ulong)Utils.ApproxExpBasisPoints(exponentBips, 4) / (ulong)BipsMultiplier;
        }

        BaseFeeWeiStorage.Set(baseFee);
    }

    private void UpdatePricingModelMultiConstraints(ulong timePassed)
    {
        long totalExponentBips = 0;
        ulong constraintsLength = ConstraintsLength();

        for (ulong i = 0; i < constraintsLength; i++)
        {
            GasConstraint constraint = OpenConstraintAt(i);
            ulong target = constraint.Target;
            ulong backlog = constraint.Backlog;

            long gas = timePassed.SaturateMul(target).ToLongSafe();
            backlog = ApplyGasDelta(backlog, gas);
            constraint.SetBacklog(backlog);

            if (backlog == 0)
                continue;
            ulong inertia = constraint.AdjustmentWindow;
            ulong divisor = inertia.SaturateMul(target);
            long exponent = backlog.SaturateMul(Utils.BipsMultiplier).ToLongSafe() / divisor.ToLongSafe();
            totalExponentBips = Utils.SaturatingSignedAdd(totalExponentBips, exponent);
        }

        UInt256 minBaseFee = MinBaseFeeWeiStorage.Get();
        UInt256 baseFee = totalExponentBips > 0
            ? minBaseFee * (ulong)Utils.ApproxExpBasisPoints(totalExponentBips, 4) / (ulong)BipsMultiplier
            : minBaseFee;

        BaseFeeWeiStorage.Set(baseFee);
    }
}
