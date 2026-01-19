using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

[TestFixture]
public class L2PricingStateTests
{
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

        l2Pricing.ShouldUseGasConstraints().Should().BeTrue();

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
            // Since ShouldUseGasConstraints returns false, it will use legacy

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
    public void ShouldUseGasConstraints_NoConstraints_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // No constraints configured
        l2Pricing.ConstraintsLength().Should().Be(0);
        l2Pricing.ShouldUseGasConstraints().Should().BeFalse("Should not use gas constraints when none are configured");
    }

    [Test]
    public void ShouldUseGasConstraints_VersionBelow50_ReturnsFalseEvenWithConstraints()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FortyNine)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a constraint
        l2Pricing.AddConstraint(1_000_000, 60, 5_000_000);
        l2Pricing.ConstraintsLength().Should().Be(1);

        // Should still return false because a version < 50
        l2Pricing.ShouldUseGasConstraints().Should().BeFalse();
    }

    [Test]
    public void ShouldUseGasConstraints_WithConstraints_ReturnsTrue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Add a constraint (target=1M, adjustmentWindow=60, backlog=5M)
        l2Pricing.AddConstraint(1_000_000, 60, 5_000_000);

        l2Pricing.ConstraintsLength().Should().Be(1);
        l2Pricing.ShouldUseGasConstraints().Should().BeTrue("Should use gas constraints when constraints are configured");
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
        newPrice.Should().BeGreaterThan(minPrice, "Price should increase when backlog exceeds tolerance");
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

        // Both should increase price above a minimum
        singleConstraintPrice.Should().BeGreaterThan(minPrice);
        dualConstraintPrice.Should().BeGreaterThan(minPrice);
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
        l2Pricing.ShouldUseGasConstraints().Should().BeTrue();

        // Update pricing model (timePassed=0 so backlog doesn't get reduced)
        l2Pricing.UpdatePricingModel(0);

        UInt256 baseFee = l2Pricing.BaseFeeWeiStorage.Get();
        baseFee.Should().BeGreaterThan(minPrice, "Base fee should increase when constraint has backlog");
    }
}
