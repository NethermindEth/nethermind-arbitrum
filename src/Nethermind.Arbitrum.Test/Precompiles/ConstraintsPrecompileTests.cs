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

namespace Nethermind.Arbitrum.Test.Precompiles;

/// <summary>
/// Tests for multi-gas constraints precompile functionality.
/// </summary>
[TestFixture]
public class ConstraintsPrecompileTests
{
    [Test]
    public void SetMultiGasPricingConstraints_ValidInput_StoresCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        l2Pricing.ClearMultiGasConstraints();

        var weights = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 100 },
            { ResourceKind.StorageAccess, 200 }
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 5_000_000, weights);

        l2Pricing.MultiGasConstraintsLength().Should().Be(1);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Target.Should().Be(7_000_000);
        constraint.AdjustmentWindow.Should().Be(60);
        constraint.Backlog.Should().Be(5_000_000);
        constraint.GetResourceWeight(ResourceKind.Computation).Should().Be(100);
        constraint.GetResourceWeight(ResourceKind.StorageAccess).Should().Be(200);
    }

    [Test]
    public void EnableAndDisable_MultiConstraints_SwitchesGasModel()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Initially no multi-gas constraints - should use legacy or single
        l2Pricing.MultiGasConstraintsLength().Should().Be(0);
        l2Pricing.GetGasModelToUse().Should().Be(GasModel.Legacy);

        // Enable multi-gas constraints
        var weights = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 1 }
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);

        l2Pricing.GetGasModelToUse().Should().Be(GasModel.MultiGasConstraints);

        // Disable by clearing
        l2Pricing.ClearMultiGasConstraints();

        l2Pricing.GetGasModelToUse().Should().Be(GasModel.Legacy);
    }

    [Test]
    public void MultiGasConstraints_BacklogUpdate_WorksCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add constraint with known weights
        var weights = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 2 },
            { ResourceKind.StorageAccess, 3 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights);

        // Create gas usage
        MultiGas gasUsed = default;
        gasUsed.Increment(ResourceKind.Computation, 100);
        gasUsed.Increment(ResourceKind.StorageAccess, 200);

        l2Pricing.GrowBacklog(0, gasUsed);

        // Expected: 100*2 + 200*3 = 200 + 600 = 800
        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(800);
    }

    [Test]
    public void MultiGasConstraints_ExponentCalculation_MatchesFormula()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add constraint: target=1000, window=10, backlog=10000
        // Weight: Computation=1, maxWeight=1
        var weights = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 1 }
        };
        l2Pricing.AddMultiGasConstraint(1000, 10, 10000, weights);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // Formula: (backlog * weight * BipsMultiplier) / (adjustmentWindow * target * maxWeight)
        // = (10000 * 1 * 10000) / (10 * 1000 * 1)
        // = 100_000_000 / 10_000
        // = 10000 bips = 1.0 (100%)
        exponents[(int)ResourceKind.Computation].Should().Be(10000);
    }

    [Test]
    public void MultiGasConstraints_TargetFees_ComputedCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Enable multi-gas model by adding a constraint
        var weights = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccess, 1 }
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);

        // Set base fees for different resources and commit
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.Computation, 100);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.StorageAccess, 500);
        l2Pricing.CommitMultiGasFees();

        UInt256 computationFee = l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.Computation);
        UInt256 storageFee = l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.StorageAccess);

        computationFee.Should().Be(new UInt256(100));
        storageFee.Should().Be(new UInt256(500));
    }

    [Test]
    public void MultiGasConstraints_ExponentCalculation_NoCapAtStorageLevel()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        var weights = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 1 }
        };

        // backlog = 1_000_000, target = 100, window = 1, weight = 1, maxWeight = 1
        // Exponent = (1_000_000 * 1 * 10000) / (1 * 100 * 1) = 10_000_000_000 / 100 = 100_000_000
        // This exceeds MaxPricingExponentBips of 85000
        l2Pricing.AddMultiGasConstraint(100, 1, 1_000_000, weights);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // Exact expected value
        exponents[(int)ResourceKind.Computation].Should().Be(100_000_000);
    }

    [Test]
    public void GetMultiGasPricingConstraints_WhenCalled_ReturnsAllConstraints()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add multiple constraints
        var weights1 = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccess, 2 }
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 100, weights1);

        var weights2 = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.HistoryGrowth, 3 }
        };
        l2Pricing.AddMultiGasConstraint(2_000_000, 120, 200, weights2);

        ulong count = l2Pricing.MultiGasConstraintsLength();
        var results = new List<(ulong Target, uint Window, ulong Backlog, Dictionary<ResourceKind, ulong> Weights)>();

        for (ulong i = 0; i < count; i++)
        {
            MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(i);
            results.Add((
                constraint.Target,
                constraint.AdjustmentWindow,
                constraint.Backlog,
                constraint.GetResourcesWithWeights()
            ));
        }

        results.Should().HaveCount(2);

        results[0].Target.Should().Be(1_000_000);
        results[0].Window.Should().Be(60);
        results[0].Backlog.Should().Be(100);
        results[0].Weights.Should().HaveCount(2);
        results[0].Weights[ResourceKind.Computation].Should().Be(1);
        results[0].Weights[ResourceKind.StorageAccess].Should().Be(2);

        results[1].Target.Should().Be(2_000_000);
        results[1].Window.Should().Be(120);
        results[1].Backlog.Should().Be(200);
        results[1].Weights.Should().HaveCount(1);
        results[1].Weights[ResourceKind.HistoryGrowth].Should().Be(3);
    }

    [Test]
    public void MultiGasConstraints_MultipleConstraints_CombineExponents()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add two constraints affecting the same resource
        // Constraint 1: target=1000, window=10, backlog=1000, Computation=1
        // Exponent: (1000 * 1 * 10000) / (10 * 1000 * 1) = 1000
        var weights1 = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 1 }
        };
        l2Pricing.AddMultiGasConstraint(1000, 10, 1000, weights1);

        // Constraint 2: target=1000, window=10, backlog=2000, Computation=1
        // Exponent: (2000 * 1 * 10000) / (10 * 1000 * 1) = 2000
        var weights2 = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 1 }
        };
        l2Pricing.AddMultiGasConstraint(1000, 10, 2000, weights2);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // Total Computation exponent: 1000 + 2000 = 3000
        exponents[(int)ResourceKind.Computation].Should().Be(3000);
    }

    [Test]
    public void MultiGasConstraints_DifferentResources_IndependentExponents()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add constraint with multiple resources having different weights
        // target=1000, window=10, backlog=5000
        // Computation=1, StorageAccess=2, maxWeight=2
        var weights = new Dictionary<ResourceKind, ulong>
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccess, 2 }
        };
        l2Pricing.AddMultiGasConstraint(1000, 10, 5000, weights);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // Computation: (5000 * 1 * 10000) / (10 * 1000 * 2) = 50_000_000 / 20_000 = 2500
        // StorageAccess: (5000 * 2 * 10000) / (10 * 1000 * 2) = 100_000_000 / 20_000 = 5000
        exponents[(int)ResourceKind.Computation].Should().Be(2500);
        exponents[(int)ResourceKind.StorageAccess].Should().Be(5000);

        // Resources not in the constraint should have 0 exponent
        exponents[(int)ResourceKind.Unknown].Should().Be(0);
        exponents[(int)ResourceKind.HistoryGrowth].Should().Be(0);
    }
}
