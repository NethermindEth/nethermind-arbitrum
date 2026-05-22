// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Runtime.CompilerServices;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Math;
using Nethermind.Core;
using Nethermind.Int256;

[assembly: InternalsVisibleTo("Nethermind.Arbitrum.Test")]

namespace Nethermind.Arbitrum.Arbos.Storage;

/// <summary>
/// Identifies which pricing model is active for L2 gas fee calculation.
/// </summary>
public enum GasModel
{
    Unknown,
    Legacy,
    SingleGasConstraints,
    MultiGasConstraints,
}

/// <summary>
/// Manages L2 gas pricing state including base fee calculation, backlog tracking,
/// and constraint-based pricing models (legacy, single-gas, multi-gas).
/// </summary>
public sealed class L2PricingState(ArbosStorage storage, ulong currentArbosVersion)
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
    private static readonly byte[] MultiGasConstraintsKey = [1];
    private static readonly byte[] MultiGasFeesKey = [2];

    private const ulong InitialSpeedLimitPerSecondV0 = 1_000_000;
    private const ulong InitialPerBlockGasLimitV0 = 20 * 1_000_000;
    private static readonly ulong InitialMinimumBaseFeeWei = (ulong)(Unit.GWei / 10);
    private const ulong InitialPricingInertia = 102;
    private const ulong InitialBacklogTolerance = 10;

    public const ulong InitialSpeedLimitPerSecondV6 = 7_000_000;
    public const ulong InitialPerBlockGasLimitV6 = 32 * 1_000_000;
    public const long BipsMultiplier = 10_000;
    // MaxPricingExponentBips caps the basefee growth: exp(8.5) ~= x5,000 min base fee.
    public const long MaxPricingExponentBips = 85_000;

    public const ulong InitialPerTxGasLimit = 32_000_000;
    public const int GasConstraintsMaxNum = 20;

    private static readonly ulong InitialBaseFeeWei = InitialMinimumBaseFeeWei;
    private readonly SubStorageVector _constraints = new(storage.OpenSubStorage(ConstraintsKey));
    private readonly SubStorageVector _multiGasConstraints = new(storage.OpenSubStorage(MultiGasConstraintsKey));
    internal readonly MultiGasFees MultiGasFees = new(storage.OpenSubStorage(MultiGasFeesKey));

    public ulong CurrentArbosVersion { get; internal set; } = currentArbosVersion;

    public ArbosStorageBackedULong SpeedLimitPerSecondStorage { get; } = new(storage, SpeedLimitPerSecondOffset);
    public ArbosStorageBackedULong PerBlockGasLimitStorage { get; } = new(storage, PerBlockGasLimitOffset);
    public ArbosStorageBackedUInt256 BaseFeeWeiStorage { get; } = new(storage, BaseFeeWeiOffset);
    public ArbosStorageBackedUInt256 MinBaseFeeWeiStorage { get; } = new(storage, MinBaseFeeWeiOffset);
    public ArbosStorageBackedULong GasBacklogStorage { get; } = new(storage, GasBacklogOffset);
    public ArbosStorageBackedULong PricingInertiaStorage { get; } = new(storage, PricingInertiaOffset);
    public ArbosStorageBackedULong BacklogToleranceStorage { get; } = new(storage, BacklogToleranceOffset);
    public ArbosStorageBackedULong PerTxGasLimitStorage { get; } = new(storage, PerTxGasLimitOffset);

    /// <summary>
    /// Writes default pricing parameters to the given storage (called during ArbOS genesis).
    /// </summary>
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
    /// Determines the active pricing model based on ArbOS version and configured constraints.
    /// </summary>
    public GasModel GetGasModelToUse()
    {
        if (CurrentArbosVersion >= ArbosVersion.MultiGasConstraintsVersion)
            if (MultiGasConstraintsLength() > 0)
                return GasModel.MultiGasConstraints;

        if (CurrentArbosVersion >= ArbosVersion.MultiConstraintPricing)
            if (ConstraintsLength() > 0)
                return GasModel.SingleGasConstraints;

        return GasModel.Legacy;
    }

    /// <summary>
    /// Recalculates the base fee by shrinking backlogs and recomputing exponents
    /// for the active pricing model.
    /// </summary>
    /// <param name="timePassed">Seconds elapsed since the previous block.</param>
    public void UpdatePricingModel(ulong timePassed)
    {
        GasModel model = GetGasModelToUse();
        switch (model)
        {
            case GasModel.MultiGasConstraints:
                UpdatePricingModelMultiGasConstraints(timePassed);
                break;
            case GasModel.SingleGasConstraints:
                UpdatePricingModelMultiConstraints(timePassed);
                break;
            case GasModel.Legacy:
                UpdatePricingModelLegacy(timePassed);
                break;
            case GasModel.Unknown:
            default:
                throw new InvalidOperationException($"Unexpected gas model: {model}");
        }
    }

    /// <summary>
    /// Adjusts the gas backlog by <paramref name="gas"/> units (positive shrinks, negative grows).
    /// </summary>
    public void AddToGasPool(long gas)
    {
        GasModel model = GetGasModelToUse();
        switch (model)
        {
            case GasModel.MultiGasConstraints:
            case GasModel.SingleGasConstraints:
                AddToGasPoolMultiConstraints(gas);
                break;
            case GasModel.Unknown:
            case GasModel.Legacy:
            default:
                AddToGasPoolLegacy(gas);
                break;
        }
    }

    /// <summary>
    /// Returns the ArbOS storage gas cost charged for updating the gas pool.
    /// </summary>
    public ulong GasPoolUpdateCost()
    {
        // Charge a static price for any pricer starting from ArbOS 60
        if (CurrentArbosVersion >= ArbosVersion.MultiGasConstraintsVersion)
            return ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;

        ulong result = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;

        if (CurrentArbosVersion >= ArbosVersion.MultiConstraintPricing)
            result += ArbosStorage.StorageReadCost;

        if (CurrentArbosVersion >= ArbosVersion.FiftyOne)
        {
            ulong constraintsLen = ConstraintsLength();
            if (constraintsLen > 0)
            {
                result += ArbosStorage.StorageReadCost;
                result += (constraintsLen - 1) * (ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost);
            }
        }

        return result;
    }

    /// <summary>
    /// Increases the backlog by the gas used in a transaction.
    /// </summary>
    public void GrowBacklog(ulong usedGas, MultiGas usedMultiGas)
        => UpdateBacklogByModel(BacklogOperation.Grow, usedGas, usedMultiGas);

    /// <summary>
    /// Decreases the backlog by the gas used in a transaction (saturates at zero).
    /// </summary>
    public void ShrinkBacklog(ulong usedGas, MultiGas usedMultiGas)
        => UpdateBacklogByModel(BacklogOperation.Shrink, usedGas, usedMultiGas);

    /// <summary>
    /// Directly sets the legacy gas backlog value.
    /// </summary>
    public void SetGasBacklog(ulong backlog) => GasBacklogStorage.Set(backlog);

    /// <summary>
    /// Returns the number of single-gas constraints currently configured.
    /// </summary>
    public ulong ConstraintsLength() => _constraints.Length();

    /// <summary>
    /// Opens the single-gas constraint at the given index.
    /// </summary>
    public GasConstraint OpenConstraintAt(ulong index) => new(_constraints.At(index));

    /// <summary>
    /// Appends a new single-gas constraint with the specified parameters.
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
    /// Removes all single-gas constraints and zeroes their storage.
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
    /// Returns the number of multi-gas constraints currently configured (ArbOS 60+).
    /// </summary>
    public ulong MultiGasConstraintsLength() => _multiGasConstraints.Length();

    /// <summary>
    /// Opens the multi-gas constraint at the given index.
    /// </summary>
    public MultiGasConstraint OpenMultiGasConstraintAt(ulong index) => new(_multiGasConstraints.At(index));

    /// <summary>
    /// Appends a new multi-gas constraint with per-resource weights.
    /// </summary>
    public void AddMultiGasConstraint(ulong target, uint adjustmentWindow, ulong backlog, Dictionary<ResourceKind, ulong> weights)
    {
        ArbosStorage subStorage = _multiGasConstraints.Push();
        MultiGasConstraint constraint = new(subStorage);
        constraint.SetTarget(target);
        constraint.SetAdjustmentWindow(adjustmentWindow);
        constraint.SetBacklog(backlog);
        constraint.SetResourceWeights(weights);
    }

    /// <summary>
    /// Removes all multi-gas constraints and zeroes their storage.
    /// </summary>
    public void ClearMultiGasConstraints()
    {
        ulong length = MultiGasConstraintsLength();
        for (ulong i = 0; i < length; i++)
        {
            ArbosStorage subStorage = _multiGasConstraints.Pop();
            MultiGasConstraint constraint = new(subStorage);
            constraint.Clear();
        }
    }

    /// <summary>
    /// Computes the pricing exponent (in basis points) per resource kind across all multi-gas constraints.
    /// Each constraint contributes <c>backlog * weight * BIPS / (target * window * maxWeight)</c>.
    /// </summary>
    public long[] CalcMultiGasConstraintsExponents()
    {
        long[] exponentPerKind = new long[MultiGas.NumResourceKinds];
        ulong constraintsLength = MultiGasConstraintsLength();

        for (ulong i = 0; i < constraintsLength; i++)
        {
            MultiGasConstraint constraint = OpenMultiGasConstraintAt(i);
            ulong target = constraint.Target;
            ulong backlog = constraint.Backlog;

            if (backlog == 0)
                continue;

            uint adjustmentWindow = constraint.AdjustmentWindow;
            ulong maxWeight = constraint.MaxWeight;

            ulong divisor = ((ulong)adjustmentWindow).SaturateMul(target).SaturateMul(maxWeight);

            for (int kindIndex = 0; kindIndex < MultiGas.NumResourceKinds; kindIndex++)
            {
                ulong weight = constraint.GetResourceWeight((ResourceKind)kindIndex);
                if (weight == 0)
                    continue;

                long dividend = backlog.SaturateMul(weight).SaturateMul(Utils.BipsMultiplier).ToLongSafe();
                if (divisor == 0)
                    throw new InvalidOperationException($"Invalid multi-gas constraint at index {i}: divisor is zero (target={target}, window={adjustmentWindow}, maxWeight={maxWeight})");
                long exp = dividend / divisor.ToLongSafe();
                exponentPerKind[kindIndex] = Utils.SaturatingSignedAdd(exponentPerKind[kindIndex], exp);
            }
        }

        return exponentPerKind;
    }

    /// <summary>
    /// Calculates the total wei cost of <paramref name="gasUsed"/> using per-resource base fees
    /// (used for retryable ticket refund computation).
    /// </summary>
    public UInt256 MultiDimensionalPriceForRefund(MultiGas gasUsed)
    {
        Span<UInt256> fees = stackalloc UInt256[MultiGas.NumResourceKinds];
        GetMultiGasBaseFeePerResource(fees);
        UInt256 total = UInt256.Zero;

        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            ResourceKind kind = (ResourceKind)i;
            ulong amount = gasUsed.Get(kind);
            if (amount == 0)
                continue;

            UInt256 part = fees[i] * amount;
            total += part;
        }

        return total;
    }

    /// <summary>
    /// Promotes next-block per-resource fees to current-block fees (end-of-block commit).
    /// </summary>
    public void CommitMultiGasFees()
    {
        GasModel model = GetGasModelToUse();
        if (model != GasModel.MultiGasConstraints)
            return;
        MultiGasFees.CommitNextToCurrent();
    }

    public void SetSpeedLimitPerSecond(ulong limit) => SpeedLimitPerSecondStorage.Set(limit);

    public void SetMaxPerBlockGasLimit(ulong limit) => PerBlockGasLimitStorage.Set(limit);

    public void SetBaseFeeWei(UInt256 baseFee) => BaseFeeWeiStorage.Set(baseFee);

    public void SetMinBaseFeeWei(UInt256 priceInWei) => MinBaseFeeWeiStorage.Set(priceInWei);

    public void SetPricingInertia(ulong inertia) => PricingInertiaStorage.Set(inertia);

    public void SetBacklogTolerance(ulong backlogTolerance) => BacklogToleranceStorage.Set(backlogTolerance);

    public void SetMaxPerTxGasLimit(ulong limit) => PerTxGasLimitStorage.Set(limit);

    internal void SetMultiGasConstraintsFromSingleGasConstraints()
    {
        ClearMultiGasConstraints();

        ulong length = ConstraintsLength();
        for (ulong i = 0; i < length; i++)
        {
            GasConstraint c = OpenConstraintAt(i);

            Dictionary<ResourceKind, ulong> weights = new()
            {
                { ResourceKind.Computation, 1 },
                { ResourceKind.HistoryGrowth, 1 },
                { ResourceKind.StorageAccessRead, 1 },
                { ResourceKind.StorageAccessWrite, 1 },
                { ResourceKind.StorageGrowth, 1 },
                { ResourceKind.L2Calldata, 1 },
                { ResourceKind.WasmComputation, 1 },
            };

            uint adjustmentWindow = c.AdjustmentWindow > uint.MaxValue ? uint.MaxValue : (uint)c.AdjustmentWindow;
            AddMultiGasConstraint(c.Target, adjustmentWindow, c.Backlog, weights);
        }
    }

    internal void UpdatePricingModelMultiConstraints(ulong timePassed)
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
            if (divisor == 0)
                throw new InvalidOperationException($"Invalid gas constraint at index {i}: divisor is zero (target={target}, inertia={inertia})");
            long exponent = backlog.SaturateMul(Utils.BipsMultiplier).ToLongSafe() / divisor.ToLongSafe();
            totalExponentBips = Utils.SaturatingSignedAdd(totalExponentBips, exponent);
        }

        UInt256 minBaseFee = MinBaseFeeWeiStorage.Get();
        UInt256 baseFee = totalExponentBips > 0
            ? minBaseFee * (ulong)Utils.ApproxExpBasisPoints(totalExponentBips, 4) / Utils.BipsMultiplier
            : minBaseFee;

        BaseFeeWeiStorage.Set(baseFee);
    }

    internal void UpdatePricingModelMultiGasConstraints(ulong timePassed)
    {
        ulong constraintsLength = MultiGasConstraintsLength();

        for (ulong i = 0; i < constraintsLength; i++)
        {
            MultiGasConstraint constraint = OpenMultiGasConstraintAt(i);
            ulong target = constraint.Target;
            ulong backlog = constraint.Backlog;

            long gas = timePassed.SaturateMul(target).ToLongSafe();
            ulong newBacklog = ApplyGasDelta(backlog, gas);
            constraint.SetBacklog(newBacklog);
        }

        long[] exponentPerKind = CalcMultiGasConstraintsExponents();

        UInt256 minBaseFee = MinBaseFeeWeiStorage.Get();
        UInt256 maxBaseFee = minBaseFee;

        for (int kind = 0; kind < MultiGas.NumResourceKinds; kind++)
        {
            UInt256 baseFeeKind = CalcBaseFeeFromExponent(exponentPerKind[kind], minBaseFee);
            MultiGasFees.SetNextBlockFee((ResourceKind)kind, baseFeeKind);

            if (baseFeeKind > maxBaseFee)
                maxBaseFee = baseFeeKind;
        }

        BaseFeeWeiStorage.Set(maxBaseFee);
    }

    private void UpdateBacklogByModel(BacklogOperation op, ulong usedGas, MultiGas usedMultiGas)
    {
        GasModel model = GetGasModelToUse();
        switch (model)
        {
            case GasModel.Legacy:
                UpdateLegacyBacklog(op, usedGas);
                break;
            case GasModel.SingleGasConstraints:
                UpdateSingleGasConstraintsBacklogs(op, usedGas);
                break;
            case GasModel.MultiGasConstraints:
                UpdateMultiGasConstraintsBacklogs(op, usedMultiGas);
                break;
            case GasModel.Unknown:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void UpdateLegacyBacklog(BacklogOperation op, ulong usedGas)
    {
        ulong backlog = GasBacklogStorage.Get();
        ulong newBacklog = op == BacklogOperation.Grow
            ? backlog.SaturateAdd(usedGas)
            : backlog.SaturateSub(usedGas);
        GasBacklogStorage.Set(newBacklog);
    }

    private void UpdateSingleGasConstraintsBacklogs(BacklogOperation op, ulong usedGas)
    {
        ulong constraintsLength = ConstraintsLength();
        for (ulong i = 0; i < constraintsLength; i++)
        {
            GasConstraint constraint = OpenConstraintAt(i);
            ulong backlog = constraint.Backlog;
            ulong newBacklog = op == BacklogOperation.Grow
                ? backlog.SaturateAdd(usedGas)
                : backlog.SaturateSub(usedGas);
            constraint.SetBacklog(newBacklog);
        }
    }

    private void UpdateMultiGasConstraintsBacklogs(BacklogOperation op, MultiGas usedMultiGas)
    {
        ulong constraintsLength = MultiGasConstraintsLength();
        for (ulong i = 0; i < constraintsLength; i++)
        {
            MultiGasConstraint constraint = OpenMultiGasConstraintAt(i);
            if (op == BacklogOperation.Grow)
                constraint.GrowBacklog(usedMultiGas);
            else
                constraint.ShrinkBacklog(usedMultiGas);
        }
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
            baseFee = minBaseFee * (ulong)Utils.ApproxExpBasisPoints(exponentBips, 4) / Utils.BipsMultiplier;
        }

        BaseFeeWeiStorage.Set(baseFee);
    }

    private void GetMultiGasBaseFeePerResource(Span<UInt256> fees)
    {
        UInt256 baseFeeWei = BaseFeeWeiStorage.Get();

        for (int i = 0; i < MultiGas.NumResourceKinds; i++)
        {
            ResourceKind kind = (ResourceKind)i;
            UInt256 baseFee = MultiGasFees.GetCurrentBlockFee(kind);

            if (kind == ResourceKind.SingleDim || baseFee.IsZero)
                baseFee = baseFeeWei;

            fees[i] = baseFee;
        }
    }

    private static UInt256 CalcBaseFeeFromExponent(long exponent, UInt256 minBaseFee)
    {
        if (exponent > 0)
            return minBaseFee * (ulong)Utils.ApproxExpBasisPoints(exponent, 4) / Utils.BipsMultiplier;
        return minBaseFee;
    }

    private static ulong ApplyGasDelta(ulong backlog, long gas)
    {
        return gas > 0
            ? backlog.SaturateSub((ulong)gas)
            : backlog.SaturateAdd((ulong)(-gas));
    }
}
