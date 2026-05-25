// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Arbos.L2Pricing;

[TestFixture]
public class MultiGasConstraintTests
{
    [Test]
    public void SetAndGet_Target_ReturnsCorrectValue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a multi-gas constraint
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 100 },
            { ResourceKind.StorageAccessRead, 200 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 5_000_000, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);

        constraint.Target.Should().Be(1_000_000);
        constraint.AdjustmentWindow.Should().Be(60);
        constraint.Backlog.Should().Be(5_000_000);
        constraint.MaxWeight.Should().Be(200); // Max of 100 and 200
        constraint.GetResourceWeight(ResourceKind.Computation).Should().Be(100);
        constraint.GetResourceWeight(ResourceKind.StorageAccessRead).Should().Be(200);
        constraint.GetResourceWeight(ResourceKind.Unknown).Should().Be(0); // Not set
    }

    [Test]
    public void GrowBacklog_MultipleResources_AggregatesWeighted()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Weights: Computation=2, StorageAccessRead=3
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 2 },
            { ResourceKind.StorageAccessRead, 3 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(0);

        // Create MultiGas with: Computation=100, StorageAccessRead=200
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 100);
        gas.Increment(ResourceKind.StorageAccessRead, 200);

        constraint.GrowBacklog(gas);

        // Expected: 100*2 + 200*3 = 200 + 600 = 800
        constraint.Backlog.Should().Be(800);
    }

    [Test]
    public void GrowBacklog_MultipleCalls_AccumulatesBacklog()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 100, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(100);

        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 50);

        // grow multiple times
        constraint.GrowBacklog(gas);
        constraint.Backlog.Should().Be(150);

        constraint.GrowBacklog(gas);
        constraint.Backlog.Should().Be(200);

        constraint.GrowBacklog(gas);
        constraint.Backlog.Should().Be(250);
    }

    /// <summary>
    /// Validates weighted backlog accumulation:
    /// Tests weighted backlog accumulation across multiple GrowBacklog calls with different values.
    /// </summary>
    [Test]
    public void GrowBacklog_MultipleCallsWithDifferentValues_AccumulatesWeightedBacklog()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccessRead, 2 }
        };
        l2Pricing.AddMultiGasConstraint(10, 5, 0, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(0);

        MultiGas gas1 = default;
        gas1.Increment(ResourceKind.Computation, 10);
        gas1.Increment(ResourceKind.StorageAccessRead, 10);
        constraint.GrowBacklog(gas1);

        constraint.Backlog.Should().Be(30, "initial backlog: 1*10 + 2*10 = 30");

        MultiGas gas2 = default;
        gas2.Increment(ResourceKind.Computation, 5);
        gas2.Increment(ResourceKind.StorageAccessRead, 15);
        constraint.GrowBacklog(gas2);

        constraint.Backlog.Should().Be(65, "backlog should accumulate across calls");
    }

    /// <summary>
    /// Validates weighted backlog decay:
    /// Tests weighted backlog decay with underflow clamping to zero.
    /// </summary>
    [Test]
    public void ShrinkBacklog_MultipleCallsWithUnderflow_ClampsToZero()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccessRead, 2 }
        };
        l2Pricing.AddMultiGasConstraint(10, 5, 0, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);

        MultiGas growGas = default;
        growGas.Increment(ResourceKind.Computation, 10);
        growGas.Increment(ResourceKind.StorageAccessRead, 10);
        constraint.GrowBacklog(growGas);
        constraint.Backlog.Should().Be(30);

        MultiGas decayGas1 = default;
        decayGas1.Increment(ResourceKind.Computation, 3);
        decayGas1.Increment(ResourceKind.StorageAccessRead, 4);
        constraint.ShrinkBacklog(decayGas1);

        constraint.Backlog.Should().Be(19, "30 - (1*3 + 2*4) = 19");

        MultiGas decayGas2 = default;
        decayGas2.Increment(ResourceKind.Computation, 50);
        decayGas2.Increment(ResourceKind.StorageAccessRead, 50);
        constraint.ShrinkBacklog(decayGas2);

        constraint.Backlog.Should().Be(0, "backlog must clamp to zero on underflow");
    }

    [Test]
    public void ShrinkBacklog_Underflow_ClampsToZero()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 100, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(100);

        // Try to shrink by more than available
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 500);

        constraint.ShrinkBacklog(gas);

        // should clamp to 0, not underflow
        constraint.Backlog.Should().Be(0);
    }

    [Test]
    public void GetUsedResources_WithMixedWeights_ReturnsOnlyNonZeroWeights()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 10 },
            { ResourceKind.StorageGrowth, 20 },
            { ResourceKind.HistoryGrowth, 30 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);

        ResourceKind[] usedResources = constraint.UsedResources().ToArray();

        usedResources.Should().HaveCount(3);
        usedResources.Should().Contain(ResourceKind.Computation);
        usedResources.Should().Contain(ResourceKind.StorageGrowth);
        usedResources.Should().Contain(ResourceKind.HistoryGrowth);
        usedResources.Should().NotContain(ResourceKind.Unknown);
        usedResources.Should().NotContain(ResourceKind.StorageAccessRead);
        usedResources.Should().NotContain(ResourceKind.StorageAccessWrite);
    }

    [Test]
    public void GetResourcesWithWeights_WithSetWeights_ReturnsCorrectMapping()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 100 },
            { ResourceKind.StorageAccessRead, 200 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);

        Dictionary<ResourceKind, ulong> result = constraint.GetResourcesWithWeights();

        result.Should().HaveCount(2);
        result[ResourceKind.Computation].Should().Be(100);
        result[ResourceKind.StorageAccessRead].Should().Be(200);
    }

    [Test]
    public void Clear_WhenCalled_RemovesAllData()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 100 },
            { ResourceKind.StorageAccessRead, 200 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 5_000_000, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);

        constraint.Clear();

        constraint.Target.Should().Be(0);
        constraint.AdjustmentWindow.Should().Be(0);
        constraint.Backlog.Should().Be(0);
        constraint.MaxWeight.Should().Be(0);
        constraint.GetResourceWeight(ResourceKind.Computation).Should().Be(0);
        constraint.GetResourceWeight(ResourceKind.StorageAccessRead).Should().Be(0);
    }
}
