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
    public void MultiGasConstraints_AddAndClear_WorksCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Initially no multi-gas constraints
        l2Pricing.MultiGasConstraintsLength().Should().Be(0);

        // Add 3 multi-gas constraints with different weights
        Dictionary<ResourceKind, ulong> weights1 = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccessRead, 2 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 5_000_000, weights1);

        Dictionary<ResourceKind, ulong> weights2 = new()
        {
            { ResourceKind.HistoryGrowth, 3 },
        };
        l2Pricing.AddMultiGasConstraint(2_000_000, 120, 10_000_000, weights2);

        Dictionary<ResourceKind, ulong> weights3 = new()
        {
            { ResourceKind.StorageGrowth, 1 },
            { ResourceKind.SingleDim, 4 },
        };
        l2Pricing.AddMultiGasConstraint(500_000, 30, 1_000_000, weights3);

        l2Pricing.MultiGasConstraintsLength().Should().Be(3);

        // Verify each constraint
        MultiGasConstraint constraint0 = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint0.Target.Should().Be(1_000_000);
        constraint0.AdjustmentWindow.Should().Be(60);
        constraint0.Backlog.Should().Be(5_000_000);
        constraint0.MaxWeight.Should().Be(2);
        constraint0.GetResourceWeight(ResourceKind.Computation).Should().Be(1);
        constraint0.GetResourceWeight(ResourceKind.StorageAccessRead).Should().Be(2);

        MultiGasConstraint constraint1 = l2Pricing.OpenMultiGasConstraintAt(1);
        constraint1.Target.Should().Be(2_000_000);
        constraint1.AdjustmentWindow.Should().Be(120);
        constraint1.Backlog.Should().Be(10_000_000);
        constraint1.MaxWeight.Should().Be(3);
        constraint1.GetResourceWeight(ResourceKind.HistoryGrowth).Should().Be(3);

        MultiGasConstraint constraint2 = l2Pricing.OpenMultiGasConstraintAt(2);
        constraint2.Target.Should().Be(500_000);
        constraint2.AdjustmentWindow.Should().Be(30);
        constraint2.Backlog.Should().Be(1_000_000);
        constraint2.MaxWeight.Should().Be(4);
        constraint2.GetResourceWeight(ResourceKind.StorageGrowth).Should().Be(1);
        constraint2.GetResourceWeight(ResourceKind.SingleDim).Should().Be(4);

        // Clear multi-gas constraints
        l2Pricing.ClearMultiGasConstraints();
        l2Pricing.MultiGasConstraintsLength().Should().Be(0);
    }

    [Test]
    public void CalcMultiGasConstraintsExponents_WhenBacklogExists_ReturnsCorrectValues()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a constraint: target=1000, window=10, backlog=5000
        // Weights: Computation=1, StorageAccess=2
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccessRead, 2 },
        };
        l2Pricing.AddMultiGasConstraint(1000, 10, 5000, weights);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // Formula: sum over constraints of: (backlog * weight * BipsMultiplier) / (adjustmentWindow * target * maxWeight)
        // For Computation: (5000 * 1 * 10000) / (10 * 1000 * 2) = 50_000_000 / 20_000 = 2500
        // For StorageAccess: (5000 * 2 * 10000) / (10 * 1000 * 2) = 100_000_000 / 20_000 = 5000
        exponents.Should().HaveCount(MultiGas.NumResourceKinds);
        exponents[(int)ResourceKind.Computation].Should().Be(2500);
        exponents[(int)ResourceKind.StorageAccessRead].Should().Be(5000);
        exponents[(int)ResourceKind.Unknown].Should().Be(0);
        exponents[(int)ResourceKind.HistoryGrowth].Should().Be(0);
    }

    [Test]
    public void CalcMultiGasConstraintsExponents_MultipleConstraints_SumsExponents()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add two constraints, both affecting Computation
        // Constraint 1: target=1000, window=10, backlog=2000, Computation=1, maxWeight=1
        // Exponent contribution: (2000 * 1 * 10000) / (10 * 1000 * 1) = 20_000_000 / 10_000 = 2000
        Dictionary<ResourceKind, ulong> weights1 = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(1000, 10, 2000, weights1);

        // Constraint 2: target=500, window=5, backlog=1000, Computation=2, maxWeight=2
        // Exponent contribution: (1000 * 2 * 10000) / (5 * 500 * 2) = 20_000_000 / 5_000 = 4000
        Dictionary<ResourceKind, ulong> weights2 = new()
        {
            { ResourceKind.Computation, 2 },
        };
        l2Pricing.AddMultiGasConstraint(500, 5, 1000, weights2);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // Total for Computation: 2000 + 4000 = 6000
        exponents[(int)ResourceKind.Computation].Should().Be(6000);
    }

    [Test]
    public void CalcMultiGasConstraintsExponents_SingleDimWeight_IsNotIncludedInExponent()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a constraint where SingleDim has a non-zero weight alongside Computation.
        // backlog=5000, target=1000, window=10
        // Computation weight=1, SingleDim weight=3, maxWeight=3
        // For Computation: (5000 * 1 * 10000) / (10 * 1000 * 3) = 50_000_000 / 30_000 ≈ 1666
        // SingleDim must be skipped and remain 0.
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.SingleDim, 3 },
        };
        l2Pricing.AddMultiGasConstraint(1000, 10, 5000, weights);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        exponents[(int)ResourceKind.SingleDim].Should().Be(0, "SingleDim must not contribute to the base fee exponent");
        exponents[(int)ResourceKind.Computation].Should().Be(1666, "Computation in the same constraint must still be computed normally");
    }

    [Test]
    public void GrowBacklog_MultiGasConstraints_UpdatesWeightedBacklog()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a multi-gas constraint with weights
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 2 },
            { ResourceKind.StorageAccessRead, 3 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(0);

        // Create MultiGas with usage
        MultiGas usedGas = default;
        usedGas.Increment(ResourceKind.Computation, 100);
        usedGas.Increment(ResourceKind.StorageAccessRead, 200);

        // Grow backlog
        l2Pricing.GrowBacklog(0, usedGas);

        // Expected weighted backlog: 100*2 + 200*3 = 200 + 600 = 800
        constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(800);
    }

    [Test]
    public void ShrinkBacklog_MultiGasConstraints_ReducesWeightedBacklog()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a multi-gas constraint with initial backlog
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 2 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 1000, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(1000);

        // Create MultiGas to shrink
        MultiGas paidGas = default;
        paidGas.Increment(ResourceKind.Computation, 200);

        // Shrink backlog
        l2Pricing.ShrinkBacklog(0, paidGas);

        // Expected: 1000 - (200*2) = 1000 - 400 = 600
        constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(600);
    }
}
