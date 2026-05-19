// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public partial class L2PricingStateTests
{
    [Test]
    public void GetGasModelToUse_WhenArbOS49_ReturnsLegacy()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyNine)
            .WithReleaseSpec();

        context.ArbosState.L2PricingState.GetGasModelToUse().Should().Be(GasModel.Legacy);
    }

    [Test]
    public void GetGasModelToUse_WhenArbOS50WithConstraints_ReturnsSingleGasConstraints()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        context.ArbosState.L2PricingState.AddConstraint(7_000_000, 60, 0);
        context.ArbosState.L2PricingState.GetGasModelToUse().Should().Be(GasModel.SingleGasConstraints);
    }

    [Test]
    public void GetGasModelToUse_WhenArbOS60WithMultiGasConstraints_ReturnsMultiGasConstraints()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        context.ArbosState.L2PricingState.AddMultiGasConstraint(7_000_000, 60, 0, weights);
        context.ArbosState.L2PricingState.GetGasModelToUse().Should().Be(GasModel.MultiGasConstraints);
    }

    [Test]
    public void GasPoolUpdateCost_ArbOS60_ReturnsStaticCost()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // For ArbOS >= 60, GasPoolUpdateCost should return static cost regardless of constraints
        ulong staticCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;

        // With no constraints
        l2Pricing.GasPoolUpdateCost().Should().Be(staticCost);

        // With single-gas constraints
        l2Pricing.AddConstraint(7_000_000, 60, 0);
        l2Pricing.GasPoolUpdateCost().Should().Be(staticCost);

        // With multi-gas constraints
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);
        l2Pricing.GasPoolUpdateCost().Should().Be(staticCost);
    }

    [Test]
    public void GasPoolUpdateCost_ArbOS51WithConstraints_ReturnsCorrectCost()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FiftyOne)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // For ArbOS 51 without constraints: base (read+write) + MultiConstraintPricing overhead (read)
        ulong baseCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;
        ulong withOverhead = baseCost + ArbosStorage.StorageReadCost;
        l2Pricing.GasPoolUpdateCost().Should().Be(withOverhead);

        // With 2 constraints: overhead + read length + (n-1) * (read+write)
        l2Pricing.AddConstraint(7_000_000, 60, 0);
        l2Pricing.AddConstraint(7_000_000, 60, 0);
        ulong constraintCost = withOverhead + ArbosStorage.StorageReadCost + 1 * (ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost);
        l2Pricing.GasPoolUpdateCost().Should().Be(constraintCost);
    }

    [Test]
    public void CalcMultiGasConstraintsExponents_DivisorZero_ThrowsException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a constraint with target=0 which will cause divisor to be zero
        // divisor = adjustmentWindow * target * maxWeight = 60 * 0 * 1 = 0
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(0, 60, 1000, weights); // target=0, backlog>0

        Action act = () => l2Pricing.CalcMultiGasConstraintsExponents();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*divisor is zero*");
    }

    [Test]
    public void UpdatePricingModelMultiConstraints_DivisorZero_ThrowsException()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a constraint with target=0 and backlog>0 which will cause divisor to be zero
        // divisor = inertia * target = 60 * 0 = 0
        l2Pricing.AddConstraint(0, 60, 1000); // target=0, backlog>0

        Action act = () => l2Pricing.UpdatePricingModelMultiConstraints(0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*divisor is zero*");
    }

    [Test]
    public void SetMultiGasConstraintsFromSingleGasConstraints_WhenCalled_ConvertsCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add single-gas constraints
        l2Pricing.AddConstraint(1_000_000, 60, 5_000_000);
        l2Pricing.AddConstraint(2_000_000, 120, 10_000_000);

        l2Pricing.ConstraintsLength().Should().Be(2);
        l2Pricing.MultiGasConstraintsLength().Should().Be(0);

        // Migrate to multi-gas constraints
        l2Pricing.SetMultiGasConstraintsFromSingleGasConstraints();

        // Verify migration
        l2Pricing.MultiGasConstraintsLength().Should().Be(2);

        // First constraint
        MultiGasConstraint mc0 = l2Pricing.OpenMultiGasConstraintAt(0);
        mc0.Target.Should().Be(1_000_000);
        mc0.AdjustmentWindow.Should().Be(60);
        mc0.Backlog.Should().Be(5_000_000);
        mc0.MaxWeight.Should().Be(1);
        // All 6 resource kinds should have weight 1 (except Unknown and L1Calldata)
        mc0.GetResourceWeight(ResourceKind.Computation).Should().Be(1);
        mc0.GetResourceWeight(ResourceKind.HistoryGrowth).Should().Be(1);
        mc0.GetResourceWeight(ResourceKind.StorageAccessRead).Should().Be(1);
        mc0.GetResourceWeight(ResourceKind.StorageGrowth).Should().Be(1);
        mc0.GetResourceWeight(ResourceKind.L2Calldata).Should().Be(1);
        mc0.GetResourceWeight(ResourceKind.WasmComputation).Should().Be(1);
        mc0.GetResourceWeight(ResourceKind.Unknown).Should().Be(0);
        mc0.GetResourceWeight(ResourceKind.L1Calldata).Should().Be(0);

        // Second constraint
        MultiGasConstraint mc1 = l2Pricing.OpenMultiGasConstraintAt(1);
        mc1.Target.Should().Be(2_000_000);
        mc1.AdjustmentWindow.Should().Be(120);
        mc1.Backlog.Should().Be(10_000_000);
    }

    [Test]
    public void SetMultiGasConstraintsFromSingleGasConstraints_LargeAdjustmentWindow_ClampsToUInt32Max()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add constraint with adjustment window larger than uint.MaxValue
        l2Pricing.AddConstraint(1_000_000, ulong.MaxValue, 5_000_000);

        // Migrate
        l2Pricing.SetMultiGasConstraintsFromSingleGasConstraints();

        // Verify adjustment window is clamped to uint.MaxValue
        MultiGasConstraint mc = l2Pricing.OpenMultiGasConstraintAt(0);
        mc.AdjustmentWindow.Should().Be(uint.MaxValue);
    }

    [Test]
    public void SetMultiGasConstraintsFromSingleGasConstraints_WhenExistingConstraintsPresent_ClearsExisting()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add existing multi-gas constraints
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 5 },
        };
        l2Pricing.AddMultiGasConstraint(9_000_000, 999, 999_000, weights);
        l2Pricing.MultiGasConstraintsLength().Should().Be(1);

        // Add single-gas constraint
        l2Pricing.AddConstraint(1_000_000, 60, 5_000_000);

        // Migrate - should clear existing multi-gas constraints first
        l2Pricing.SetMultiGasConstraintsFromSingleGasConstraints();

        // Verify only the migrated constraint exists
        l2Pricing.MultiGasConstraintsLength().Should().Be(1);
        MultiGasConstraint mc = l2Pricing.OpenMultiGasConstraintAt(0);
        mc.Target.Should().Be(1_000_000);
        mc.MaxWeight.Should().Be(1); // Not 5 from the old constraint
    }

    [Test]
    public void GasPoolUpdateCost_ArbOS50_ReturnsCorrectCost()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // For ArbOS 50 (< 51): base (read+write) + MultiConstraintPricing overhead (read)
        // but no constraint iteration cost (that's only for >= 51)
        ulong baseCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;
        ulong withOverhead = baseCost + ArbosStorage.StorageReadCost;
        l2Pricing.GasPoolUpdateCost().Should().Be(withOverhead);

        // With constraints, cost should still be the same (no iteration for v50)
        l2Pricing.AddConstraint(7_000_000, 60, 0);
        l2Pricing.GasPoolUpdateCost().Should().Be(withOverhead);
    }

    [Test]
    public void GasPoolUpdateCost_ArbOS49_ReturnsBaseCostOnly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyNine)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // For ArbOS < 50: just base (read+write), no MultiConstraintPricing overhead
        ulong baseCost = ArbosStorage.StorageReadCost + ArbosStorage.StorageWriteCost;
        l2Pricing.GasPoolUpdateCost().Should().Be(baseCost);
    }
}
