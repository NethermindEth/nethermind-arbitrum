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

[TestFixture]
public class L2PricingStateTests
{
    [Test]
    public void PerTxGasLimitStorage_SetAndGet_WorksCorrectly()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Test various values
        const ulong testValue1 = 10_000_000;
        const ulong testValue2 = 32_000_000;
        const ulong testValue3 = 50_000_000;

        // Set and verify the first value
        l2Pricing.SetMaxPerTxGasLimit(testValue1);
        ulong retrieved1 = l2Pricing.PerTxGasLimitStorage.Get();
        retrieved1.Should().Be(testValue1, "PerTxGasLimit should store and retrieve the first value correctly");

        // Set and verify the second value
        l2Pricing.SetMaxPerTxGasLimit(testValue2);
        ulong retrieved2 = l2Pricing.PerTxGasLimitStorage.Get();
        retrieved2.Should().Be(testValue2, "PerTxGasLimit should store and retrieve the second value correctly");

        // Set and verify a third value
        l2Pricing.SetMaxPerTxGasLimit(testValue3);
        ulong retrieved3 = l2Pricing.PerTxGasLimitStorage.Get();
        retrieved3.Should().Be(testValue3, "PerTxGasLimit should store and retrieve the third value correctly");
    }

    [Test]
    public void SetMaxPerTxGasLimit_ToV50Value_StoresCorrectly()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Verify initial value is 0 or default
        ulong initialValue = l2Pricing.PerTxGasLimitStorage.Get();

        // Set to v50 initial value (32M)
        l2Pricing.SetMaxPerTxGasLimit(L2PricingState.InitialPerTxGasLimit);

        // Verify it was stored correctly
        ulong storedValue = l2Pricing.PerTxGasLimitStorage.Get();
        storedValue.Should().Be(L2PricingState.InitialPerTxGasLimit, "SetMaxPerTxGasLimit should store the v50 initial value (32M) correctly");
        storedValue.Should().Be(32_000_000);
        storedValue.Should().NotBe(initialValue, "Stored value should be different from initial value");
    }

    [Test]
    public void CompareLegacyPricingModelWithMultiConstraints_EquivalentConstraint_ProducesSameBaseFee()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Set a speed limit
        l2Pricing.SetSpeedLimitPerSecond(L2PricingState.InitialSpeedLimitPerSecondV6); // 7_000_000

        // Test with various backlogs
        ulong[] backlogs = [0, 1_000_000, 10_000_000, 100_000_000, 1_000_000_000];

        foreach (ulong backlog in backlogs)
        {
            // Set the gas backlog
            l2Pricing.GasBacklogStorage.Set(backlog);

            // Create a constraint equivalent to a legacy model:
            // target = SpeedLimitPerSecond
            // adjustmentWindow = PricingInertia
            // constraintBacklog = backlog - (tolerance * target), saturating at 0
            ulong target = l2Pricing.SpeedLimitPerSecondStorage.Get();
            ulong adjustmentWindow = l2Pricing.PricingInertiaStorage.Get();
            ulong tolerance = l2Pricing.BacklogToleranceStorage.Get();
            ulong constraintBacklog = backlog > tolerance * target ? backlog - tolerance * target : 0;

            l2Pricing.ClearConstraints();
            l2Pricing.AddConstraint(target, adjustmentWindow, constraintBacklog);

            // Run legacy pricing model update (timePassed = 0 to avoid side effects)
            l2Pricing.ClearConstraints(); // Clear to force legacy mode
            l2Pricing.GasBacklogStorage.Set(backlog); // Reset backlog
            // Access a private method via reflection or call UpdatePricingModel, which routes based on constraints;
            // Since GetGasModelToUse() returns Legacy when no constraints exist, it will use legacy

            // For this test, we manually calculate expected values instead of calling private methods
            // This verifies the formula equivalence

            UInt256 minBaseFee = l2Pricing.MinBaseFeeWeiStorage.Get();

            // Legacy formula: if backlog > tolerance * speedLimit, excess = backlog - tolerance * speedLimit
            // exponentBips = excess * 10000 / (inertia * speedLimit)
            // baseFee = minBaseFee * ApproxExp(exponentBips) / 10000
            UInt256 legacyBaseFee;
            if (backlog > tolerance * target)
            {
                long excess = (long)(backlog - tolerance * target);
                long exponentBips = excess * L2PricingState.BipsMultiplier / (long)(adjustmentWindow * target);
                long multiplier = Math.Utils.ApproxExpBasisPoints(exponentBips, 4);
                legacyBaseFee = minBaseFee * (UInt256)multiplier / L2PricingState.BipsMultiplier;
            }
            else
                legacyBaseFee = minBaseFee;

            // Multi-constraint formula: exponent = constraintBacklog * 10000 / (inertia * target)
            // baseFee = minBaseFee * ApproxExp(exponent) / 10000
            UInt256 multiBaseFee;
            if (constraintBacklog > 0)
            {
                long exponentBips = (long)constraintBacklog * L2PricingState.BipsMultiplier / (long)(adjustmentWindow * target);
                long multiplier = Math.Utils.ApproxExpBasisPoints(exponentBips, 4);
                multiBaseFee = minBaseFee * (UInt256)multiplier / L2PricingState.BipsMultiplier;
            }
            else
                multiBaseFee = minBaseFee;

            multiBaseFee.Should().Be(legacyBaseFee,
                $"Legacy and multi-constraint models should produce same baseFee for backlog={backlog}");
        }
    }

    [Test]
    public void UpdatePricingModel_BacklogExceedsTolerance_IncreasesPrice()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        UInt256 minPrice = l2Pricing.MinBaseFeeWeiStorage.Get();
        UInt256 initialPrice = l2Pricing.BaseFeeWeiStorage.Get();

        // Initial price should equal minimum price
        initialPrice.Should().Be(minPrice);

        // Set a high backlog to trigger price increase
        // tolerance * speedLimit = 10 * 7_000_000 = 70_000_000
        // Set the backlog much higher
        l2Pricing.GasBacklogStorage.Set(100_000_000);

        // Update pricing model with 1 second passed (uses legacy mode since no constraints)
        l2Pricing.UpdatePricingModel(1);

        UInt256 newPrice = l2Pricing.BaseFeeWeiStorage.Get();
        // Formula: backlog after 1s = 100M - 7M = 93M
        // excess = 93M - tolerance*speedLimit = 93M - 70M = 23M
        // exponent = 23M * 10000 / (102 * 7M) = 322 bips
        // multiplier = ApproxExpBasisPoints(322, 4) = 10327
        // newPrice = 100M * 10327 / 10000 = 103_270_000
        newPrice.Should().Be(new UInt256(103_270_000), "Price should increase when backlog exceeds tolerance");
    }

    [Test]
    public void GasConstraints_AddAndClear_WorksCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Initially no constraints
        l2Pricing.ConstraintsLength().Should().Be(0);

        // Add 10 constraints
        const ulong n = 10;
        for (ulong i = 0; i < n; i++)
            l2Pricing.AddConstraint(100 * i + 1, 100 * i + 2, 100 * i + 3);

        l2Pricing.ConstraintsLength().Should().Be(n);

        // Verify each constraint
        for (ulong i = 0; i < n; i++)
        {
            GasConstraint constraint = l2Pricing.OpenConstraintAt(i);
            constraint.Target.Should().Be(100 * i + 1);
            constraint.AdjustmentWindow.Should().Be(100 * i + 2);
            constraint.Backlog.Should().Be(100 * i + 3);
        }

        // Clear constraints
        l2Pricing.ClearConstraints();
        l2Pricing.ConstraintsLength().Should().Be(0);
    }

    /// <summary>
    /// Tests multi-constraint pricing model update with backlog.
    /// Verifies that the base fee increases when the constraint has a backlog.
    /// </summary>
    [Test]
    public void UpdatePricingModelMultiConstraints_WithBacklog_IncreasesBaseFee()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        UInt256 minPrice = l2Pricing.MinBaseFeeWeiStorage.Get();

        // Add a constraint with significant backlog
        // target=7M (same as speed limit), adjustmentWindow=102 (same as inertia), backlog=100M
        l2Pricing.AddConstraint(7_000_000, 102, 100_000_000);

        // Verify multi-constraint mode is active
        l2Pricing.GetGasModelToUse().Should().Be(GasModel.SingleGasConstraints);

        // Update pricing model (timePassed=0 so backlog doesn't get reduced)
        l2Pricing.UpdatePricingModel(0);

        UInt256 baseFee = l2Pricing.BaseFeeWeiStorage.Get();
        // Formula: exponent = 100M * 10000 / (102 * 7M) = 1400 bips
        // multiplier = ApproxExpBasisPoints(1400, 4) = 11502
        // baseFee = 100M * 11502 / 10000 = 115_020_000
        baseFee.Should().Be(new UInt256(115_020_000), "Base fee should increase when constraint has backlog");
    }

    [Test]
    public void AddToGasPool_NoConstraints_UpdatesLegacyBacklog()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        l2Pricing.SetGasBacklog(1_000_000);
        ulong initialBacklog = l2Pricing.GasBacklogStorage.Get();
        initialBacklog.Should().Be(1_000_000);

        // Negative gas should increase the backlog (gas used)
        l2Pricing.AddToGasPool(-500_000);
        l2Pricing.GasBacklogStorage.Get().Should().Be(1_500_000);

        // Positive gas should decrease the backlog (gas paid off)
        l2Pricing.AddToGasPool(200_000);
        l2Pricing.GasBacklogStorage.Get().Should().Be(1_300_000);
    }

    [Test]
    public void AddToGasPool_WithConstraints_UpdatesAllConstraintBacklogs()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add constraints with different backlogs
        l2Pricing.AddConstraint(1_000_000, 100, 5_000_000);
        l2Pricing.AddConstraint(2_000_000, 200, 10_000_000);

        l2Pricing.GetGasModelToUse().Should().Be(GasModel.SingleGasConstraints);

        // Negative gas should increase all constraint backlogs
        l2Pricing.AddToGasPool(-1_000_000);

        GasConstraint constraint0 = l2Pricing.OpenConstraintAt(0);
        GasConstraint constraint1 = l2Pricing.OpenConstraintAt(1);

        constraint0.Backlog.Should().Be(6_000_000);
        constraint1.Backlog.Should().Be(11_000_000);

        // Positive gas should decrease all constraint backlogs
        l2Pricing.AddToGasPool(2_000_000);

        constraint0 = l2Pricing.OpenConstraintAt(0);
        constraint1 = l2Pricing.OpenConstraintAt(1);

        constraint0.Backlog.Should().Be(4_000_000);
        constraint1.Backlog.Should().Be(9_000_000);
    }

    [Test]
    public void AddToGasPool_PositiveGasSaturatesAtZero_DoesNotUnderflow()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        l2Pricing.SetGasBacklog(100);

        // Try to subtract more than available - should saturate at 0
        l2Pricing.AddToGasPool(1_000_000);
        l2Pricing.GasBacklogStorage.Get().Should().Be(0);
    }

    [Test]
    public void SetGasBacklog_WithValue_StoresCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        l2Pricing.SetGasBacklog(12345678);
        l2Pricing.GasBacklogStorage.Get().Should().Be(12345678);

        l2Pricing.SetGasBacklog(0);
        l2Pricing.GasBacklogStorage.Get().Should().Be(0);

        l2Pricing.SetGasBacklog(ulong.MaxValue);
        l2Pricing.GasBacklogStorage.Get().Should().Be(ulong.MaxValue);
    }

    [Test]
    public void UpdatePricingModel_MultipleConstraints_SumsExponents()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        UInt256 minPrice = l2Pricing.MinBaseFeeWeiStorage.Get();

        // Add single constraint and get price
        l2Pricing.AddConstraint(7_000_000, 102, 50_000_000);
        l2Pricing.UpdatePricingModel(0);
        UInt256 singleConstraintPrice = l2Pricing.BaseFeeWeiStorage.Get();

        // Clear and add two constraints with the same total effect
        l2Pricing.ClearConstraints();
        l2Pricing.BaseFeeWeiStorage.Set(minPrice);
        l2Pricing.AddConstraint(7_000_000, 102, 25_000_000);
        l2Pricing.AddConstraint(7_000_000, 102, 25_000_000);
        l2Pricing.UpdatePricingModel(0);
        UInt256 dualConstraintPrice = l2Pricing.BaseFeeWeiStorage.Get();

        // Both should produce the same price (exponents sum to 700 bips in both cases)
        // Single: 50M * 10000 / 714M = 700 bips
        // Dual: 2 * (25M * 10000 / 714M) = 2 * 350 = 700 bips
        // multiplier = ApproxExpBasisPoints(700, 4) = 10725
        // price = 100M * 10725 / 10000 = 107_250_000
        UInt256 expectedPrice = new UInt256(107_250_000);
        singleConstraintPrice.Should().Be(expectedPrice);
        dualConstraintPrice.Should().Be(expectedPrice);
    }

    [Test]
    public void UpdatePricingModel_TimePassed_ReducesConstraintBacklogs()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // target=1M means 1M gas per second is paid off
        l2Pricing.AddConstraint(1_000_000, 100, 10_000_000);

        GasConstraint constraint = l2Pricing.OpenConstraintAt(0);
        constraint.Backlog.Should().Be(10_000_000);

        // 5 seconds passed = 5M gas paid off
        l2Pricing.UpdatePricingModel(5);

        constraint = l2Pricing.OpenConstraintAt(0);
        constraint.Backlog.Should().Be(5_000_000);
    }

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
            { ResourceKind.StorageAccess, 2 },
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
            { ResourceKind.L1Calldata, 4 },
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
        constraint0.GetResourceWeight(ResourceKind.StorageAccess).Should().Be(2);

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
        constraint2.GetResourceWeight(ResourceKind.L1Calldata).Should().Be(4);

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
            { ResourceKind.StorageAccess, 2 },
        };
        l2Pricing.AddMultiGasConstraint(1000, 10, 5000, weights);

        long[] exponents = l2Pricing.CalcMultiGasConstraintsExponents();

        // Formula: sum over constraints of: (backlog * weight * BipsMultiplier) / (adjustmentWindow * target * maxWeight)
        // For Computation: (5000 * 1 * 10000) / (10 * 1000 * 2) = 50_000_000 / 20_000 = 2500
        // For StorageAccess: (5000 * 2 * 10000) / (10 * 1000 * 2) = 100_000_000 / 20_000 = 5000
        exponents.Should().HaveCount(MultiGas.NumResourceKinds);
        exponents[(int)ResourceKind.Computation].Should().Be(2500);
        exponents[(int)ResourceKind.StorageAccess].Should().Be(5000);
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
            { ResourceKind.StorageAccess, 3 },
        };
        l2Pricing.AddMultiGasConstraint(1_000_000, 60, 0, weights);

        MultiGasConstraint constraint = l2Pricing.OpenMultiGasConstraintAt(0);
        constraint.Backlog.Should().Be(0);

        // Create MultiGas with usage
        MultiGas usedGas = default;
        usedGas.Increment(ResourceKind.Computation, 100);
        usedGas.Increment(ResourceKind.StorageAccess, 200);

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
        mc0.GetResourceWeight(ResourceKind.StorageAccess).Should().Be(1);
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
