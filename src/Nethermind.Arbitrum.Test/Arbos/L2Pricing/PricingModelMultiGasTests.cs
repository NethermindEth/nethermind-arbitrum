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

namespace Nethermind.Arbitrum.Test.Arbos.L2Pricing;

[TestFixture]
public class PricingModelMultiGasTests
{
    [Test]
    public void LegacyModel_WithSameParams_MatchesSingleConstraintModel()
    {
        // Create two identical states, one using legacy, one using single constraint
        IWorldState worldState1 = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer1 = worldState1.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState1);

        PrecompileTestContextBuilder context1 = new PrecompileTestContextBuilder(worldState1, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyNine) // Pre-MultiConstraintPricing
            .WithReleaseSpec();

        IWorldState worldState2 = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer2 = worldState2.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState2);

        PrecompileTestContextBuilder context2 = new PrecompileTestContextBuilder(worldState2, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty) // MultiConstraintPricing
            .WithReleaseSpec();

        L2PricingState l2Pricing1 = context1.ArbosState.L2PricingState;
        L2PricingState l2Pricing2 = context2.ArbosState.L2PricingState;

        // Set same parameters for both
        ulong speedLimit = 7_000_000;
        uint adjustmentWindow = 60;

        l2Pricing1.SetSpeedLimitPerSecond(speedLimit);
        l2Pricing2.SetSpeedLimitPerSecond(speedLimit);

        // Add a single constraint to the second state (should behave same as legacy)
        l2Pricing2.AddConstraint(speedLimit, adjustmentWindow, 0);

        GasModel model1 = l2Pricing1.GetGasModelToUse();
        GasModel model2 = l2Pricing2.GetGasModelToUse();

        model1.Should().Be(GasModel.Legacy);
        model2.Should().Be(GasModel.SingleGasConstraints);

        // Both should price gas similarly when backlog is 0
        // Legacy uses SpeedLimitPerSecond directly, single constraint uses constraint target
        l2Pricing1.SpeedLimitPerSecondStorage.Get().Should().Be(speedLimit);
        l2Pricing2.OpenConstraintAt(0).Target.Should().Be(speedLimit);
    }

    [Test]
    public void SingleConstraint_MatchesMultiGasConstraint_WhenSingleResource()
    {
        // Compare single constraint vs multi-gas constraint with one resource
        IWorldState worldState1 = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer1 = worldState1.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState1);

        PrecompileTestContextBuilder context1 = new PrecompileTestContextBuilder(worldState1, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        IWorldState worldState2 = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer2 = worldState2.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState2);

        PrecompileTestContextBuilder context2 = new PrecompileTestContextBuilder(worldState2, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing1 = context1.ArbosState.L2PricingState;
        L2PricingState l2Pricing2 = context2.ArbosState.L2PricingState;

        ulong target = 7_000_000;
        uint adjustmentWindow = 60;
        ulong initialBacklog = 1_000_000;

        // Single-gas constraint
        l2Pricing1.AddConstraint(target, adjustmentWindow, initialBacklog);

        // Multi-gas constraint with single resource (weight=1)
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing2.AddMultiGasConstraint(target, adjustmentWindow, initialBacklog, weights);

        GasModel model1 = l2Pricing1.GetGasModelToUse();
        GasModel model2 = l2Pricing2.GetGasModelToUse();

        GasConstraint constraint1 = l2Pricing1.OpenConstraintAt(0);
        MultiGasConstraint constraint2 = l2Pricing2.OpenMultiGasConstraintAt(0);

        model1.Should().Be(GasModel.SingleGasConstraints);
        model2.Should().Be(GasModel.MultiGasConstraints);

        // Same parameters
        constraint1.Target.Should().Be(constraint2.Target);
        constraint1.AdjustmentWindow.Should().Be(constraint2.AdjustmentWindow);
        constraint1.Backlog.Should().Be(constraint2.Backlog);
    }

    [Test]
    public void CalcExponents_MultipleConstraints_ReturnsWeightedExponents()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Constraint 1: target=100000, window=10, backlog=20000, weights={Computation:1, StorageAccessRead:2}
        Dictionary<ResourceKind, ulong> weights1 = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccessRead, 2 },
        };
        l2Pricing.AddMultiGasConstraint(100000, 10, 20000, weights1);

        // Constraint 2: target=50000, window=5, backlog=15000, weights={StorageGrowth:1}
        Dictionary<ResourceKind, ulong> weights2 = new()
        {
            { ResourceKind.StorageGrowth, 1 },
        };
        l2Pricing.AddMultiGasConstraint(50000, 5, 15000, weights2);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // Computation: (20000 * 1 * 10000) / (10 * 100000 * 2) = 100 bips
        // StorageAccessRead: (20000 * 2 * 10000) / (10 * 100000 * 2) = 200 bips
        // StorageGrowth: (15000 * 1 * 10000) / (5 * 50000 * 1) = 600 bips
        exponents.Should().HaveCount(MultiGas.NumResourceKinds);
        exponents[(int)ResourceKind.Computation].Should().Be(100);
        exponents[(int)ResourceKind.StorageAccessRead].Should().Be(200);
        exponents[(int)ResourceKind.StorageGrowth].Should().Be(600);
        exponents[(int)ResourceKind.HistoryGrowth].Should().Be(0);
        exponents[(int)ResourceKind.L1Calldata].Should().Be(0);
        exponents[(int)ResourceKind.L2Calldata].Should().Be(0);
        exponents[(int)ResourceKind.WasmComputation].Should().Be(0);
    }

    [Test]
    public void MultiDimensionalPriceForRefund_WithMultipleResources_CalculatesCorrectly()
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
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
            { ResourceKind.StorageAccessRead, 1 },
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);

        // Set base fees for resources and commit
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.Computation, 100);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead, 200);
        l2Pricing.CommitMultiGasFees();

        // Create MultiGas with usage
        MultiGas gasUsed = default;
        gasUsed.Increment(ResourceKind.Computation, 50); // 50 computation gas
        gasUsed.Increment(ResourceKind.StorageAccessRead, 30); // 30 storage access gas

        UInt256 refundPrice = l2Pricing.MultiDimensionalPriceForRefund(gasUsed);

        // Expected: 50 * 100 + 30 * 200 = 5000 + 6000 = 11000
        refundPrice.Should().Be(new UInt256(11000));
    }

    [Test]
    public void PricingModel_SingleGasVsMultiGasConstraints_ProducesSamePrice()
    {
        ulong[] backlogs = [0];
        for (ulong i = 0; i < 9; i++)
        {
            backlogs = [.. backlogs, 1_000_000 * (1 + i)];
            backlogs = [.. backlogs, 10_000_000 * (1 + i)];
            backlogs = [.. backlogs, 100_000_000 * (1 + i)];
            backlogs = [.. backlogs, 1_000_000_000 * (1 + i)];
            backlogs = [.. backlogs, 10_000_000_000 * (1 + i)];
        }

        Array.Sort(backlogs);

        foreach (ulong backlog in backlogs)
        {
            IWorldState worldState = TestWorldStateFactory.CreateForTest();
            using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
            _ = ArbOSInitialization.Create(worldState);

            PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
                .WithArbosState()
                .WithArbosVersion(ArbosVersion.Sixty)
                .WithReleaseSpec();

            L2PricingState l2Pricing = context.ArbosState.L2PricingState;

            l2Pricing.SetSpeedLimitPerSecond(L2PricingState.InitialSpeedLimitPerSecondV6);
            ulong inertia = l2Pricing.PricingInertiaStorage.Get();
            ulong target = l2Pricing.SpeedLimitPerSecondStorage.Get();

            // Clear any existing constraints
            l2Pricing.ClearConstraints();
            l2Pricing.ClearMultiGasConstraints();

            l2Pricing.AddConstraint(target, (uint)inertia, backlog);

            // Transfer single-gas constraint to multi-gas constraint BEFORE any update
            l2Pricing.SetMultiGasConstraintsFromSingleGasConstraints();

            // Trigger single-constraint pricing update
            l2Pricing.UpdatePricingModelMultiConstraints(0);
            UInt256 singlePrice = l2Pricing.BaseFeeWeiStorage.Get();

            // Trigger multi-gas pricing update
            l2Pricing.UpdatePricingModelMultiGasConstraints(0);
            UInt256 multiPrice = l2Pricing.BaseFeeWeiStorage.Get();

            multiPrice.Should().Be(singlePrice,
                $"Prices should match for backlog={backlog}: single={singlePrice}, multi={multiPrice}");
        }
    }
}
