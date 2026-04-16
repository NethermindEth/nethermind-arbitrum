// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public partial class L2PricingStateTests
{
    [Test]
    public void UpdatePricingModelLegacy_HighBacklog_IncreasesBaseFee()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyNine) // Force legacy mode
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        UInt256 minPrice = l2Pricing.MinBaseFeeWeiStorage.Get();
        l2Pricing.BaseFeeWeiStorage.Set(minPrice);

        // Set high backlog: tolerance * speedLimit = 10 * 7M = 70M
        // backlog = 200M, excess = 130M
        l2Pricing.GasBacklogStorage.Set(200_000_000);

        // Update pricing with 0 time passed (just calculate, don't reduce backlog)
        l2Pricing.UpdatePricingModel(0);

        UInt256 newPrice = l2Pricing.BaseFeeWeiStorage.Get();
        newPrice.Should().BeGreaterThan(minPrice);
    }

    [Test]
    public void GrowBacklog_LegacyModel_UpdatesGasBacklog()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyNine) // Force legacy mode
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        l2Pricing.GasBacklogStorage.Set(1000);

        // Grow backlog (legacy model uses usedGas, ignores usedMultiGas)
        l2Pricing.GrowBacklog(500, default);

        l2Pricing.GasBacklogStorage.Get().Should().Be(1500);
    }

    [Test]
    public void ShrinkBacklog_LegacyModel_ReducesGasBacklog()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyNine) // Force legacy mode
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        l2Pricing.GasBacklogStorage.Set(1000);

        // Shrink backlog (legacy model uses usedGas, ignores usedMultiGas)
        l2Pricing.ShrinkBacklog(300, default);

        l2Pricing.GasBacklogStorage.Get().Should().Be(700);
    }

    [Test]
    public void GrowBacklog_SingleGasConstraints_UpdatesAllConstraintBacklogs()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty) // Single-gas constraints mode
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        l2Pricing.AddConstraint(1_000_000, 60, 1000);
        l2Pricing.AddConstraint(2_000_000, 120, 2000);

        // Grow backlog
        l2Pricing.GrowBacklog(500, default);

        l2Pricing.OpenConstraintAt(0).Backlog.Should().Be(1500);
        l2Pricing.OpenConstraintAt(1).Backlog.Should().Be(2500);
    }

    [Test]
    public void ShrinkBacklog_SingleGasConstraints_ReducesAllConstraintBacklogs()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty) // Single-gas constraints mode
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        l2Pricing.AddConstraint(1_000_000, 60, 1000);
        l2Pricing.AddConstraint(2_000_000, 120, 2000);

        // Shrink backlog
        l2Pricing.ShrinkBacklog(300, default);

        l2Pricing.OpenConstraintAt(0).Backlog.Should().Be(700);
        l2Pricing.OpenConstraintAt(1).Backlog.Should().Be(1700);
    }

    [Test]
    public void CommitMultiGasFees_WhenNotMultiGasConstraints_DoesNothing()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty) // Single-gas constraints mode
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add single-gas constraint (not multi-gas)
        l2Pricing.AddConstraint(1_000_000, 60, 5_000_000);
        l2Pricing.GetGasModelToUse().Should().Be(GasModel.SingleGasConstraints);

        // Set a next-block fee
        l2Pricing.MultiGasFees.SetNextBlockFee(ResourceKind.Computation, 12345);

        // CommitMultiGasFees should do nothing (early return)
        l2Pricing.CommitMultiGasFees();

        // Current block fee should still be 0 (not committed)
        l2Pricing.MultiGasFees.GetCurrentBlockFee(ResourceKind.Computation).Should().Be(UInt256.Zero);
    }

    [Test]
    public void CommitMultiGasFees_WhenMultiGasConstraints_CommitsNextToCurrent()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add multi-gas constraint
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 5_000_000, weights);
        l2Pricing.GetGasModelToUse().Should().Be(GasModel.MultiGasConstraints);

        // Set a next-block fee
        l2Pricing.MultiGasFees.SetNextBlockFee(ResourceKind.Computation, 12345);

        // CommitMultiGasFees should commit next to current
        l2Pricing.CommitMultiGasFees();

        // Current block fee should now be 12345
        l2Pricing.MultiGasFees.GetCurrentBlockFee(ResourceKind.Computation).Should().Be(new UInt256(12345));
    }

    [Test]
    public void MultiDimensionalPriceForRefund_WhenL1Calldata_UsesBaseFeeWei()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add multi-gas constraint and update pricing to set fees
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 5_000_000, weights);
        l2Pricing.UpdatePricingModel(0);
        l2Pricing.CommitMultiGasFees();

        UInt256 baseFeeWei = l2Pricing.BaseFeeWeiStorage.Get();

        // L1Calldata should always use baseFeeWei, not per-resource fee
        MultiGas gasUsed = default;
        gasUsed.Increment(ResourceKind.L1Calldata, 100);

        UInt256 refund = l2Pricing.MultiDimensionalPriceForRefund(gasUsed);

        // L1Calldata uses baseFeeWei regardless of per-resource fee
        refund.Should().Be(baseFeeWei * 100);
    }

    [Test]
    public void MultiDimensionalPriceForRefund_WhenZeroFee_UsesBaseFeeWei()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Don't add any multi-gas constraints - fees will be zero
        // But we need at least one constraint for the model to be MultiGasConstraints
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights); // backlog=0 means base fee
        l2Pricing.UpdatePricingModel(0);
        // Don't commit - current block fees remain zero

        UInt256 baseFeeWei = l2Pricing.BaseFeeWeiStorage.Get();

        // StorageAccess has no weight, so fee should be zero -> falls back to baseFeeWei
        MultiGas gasUsed = default;
        gasUsed.Increment(ResourceKind.StorageAccess, 100);

        UInt256 refund = l2Pricing.MultiDimensionalPriceForRefund(gasUsed);

        // Zero fee falls back to baseFeeWei
        refund.Should().Be(baseFeeWei * 100);
    }

    [Test]
    public void CalcMultiGasConstraintsExponents_ZeroBacklog_ReturnsZeroExponents()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add constraint with zero backlog
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights); // backlog=0

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // All exponents should be zero when backlog is zero
        foreach (long exp in exponents)
            exp.Should().Be(0);
    }

    [Test]
    public void UpdatePricingModelMultiGasConstraints_ZeroExponent_UsesMinBaseFee()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        UInt256 minPrice = l2Pricing.MinBaseFeeWeiStorage.Get();

        // Add constraint with zero backlog
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights); // backlog=0

        l2Pricing.UpdatePricingModel(0);

        // With zero backlog, exponent is 0, so base fee should be minBaseFee
        UInt256 baseFee = l2Pricing.BaseFeeWeiStorage.Get();
        baseFee.Should().Be(minPrice);

        // Per-resource fee should also be minBaseFee
        l2Pricing.MultiGasFees.GetNextBlockFee(ResourceKind.Computation).Should().Be(minPrice);
    }
}
