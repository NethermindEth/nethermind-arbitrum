using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbGasInfoTests
{
    [Test]
    public void GetMaxTxGasLimit_AfterArbosV50_Returns32Million()
    {
        // Initialize ArbOS state at version 50
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Forty)
            .WithReleaseSpec();

        // Perform upgrade to v50, which sets PerTxGasLimit
        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Fifty, false, worldState, London.Instance);

        // Get max tx gas limit
        UInt256 maxTxGasLimit = ArbGasInfo.GetMaxTxGasLimit(context);

        // Verify it returns 32M
        maxTxGasLimit.Should().Be(32_000_000, "GetMaxTxGasLimit should return 32M after v50 upgrade");
        maxTxGasLimit.Should().Be(L2PricingState.InitialPerTxGasLimitV50);
    }

    [Test]
    public void GetMaxBlockGasLimit_ReturnsPerBlockGasLimit()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Get the expected value from the L2 pricing state
        ulong expectedLimit = context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get();

        // Get max block gas limit via precompile
        UInt256 maxBlockGasLimit = ArbGasInfo.GetMaxBlockGasLimit(context);

        // Verify it matches the storage value
        maxBlockGasLimit.Should().Be(expectedLimit, "GetMaxBlockGasLimit should return the per-block gas limit from storage");
    }

    [Test]
    public void GetGasPricingConstraints_WithNoConstraints_ReturnsEmptyArray()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Get gas pricing constraints (no constraints set initially)
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify it returns an empty array when no constraints are set
        constraints.Should().BeEmpty("No constraints are set initially");
    }

    [Test]
    public void GetGasPricingConstraints_WithSingleConstraint_ReturnsCorrectValues()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Add a single constraint
        ulong expectedTarget = 1_000_000;
        ulong expectedAdjustmentWindow = 3600;
        ulong expectedBacklog = 500_000;

        context.ArbosState.L2PricingState.AddConstraint(expectedTarget, expectedAdjustmentWindow, expectedBacklog);

        // Get gas pricing constraints
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify the returned constraint
        constraints.Should().HaveCount(1);
        constraints[0].Should().HaveCount(3);
        constraints[0][0].Should().Be(expectedTarget, "First element should be target gas per second");
        constraints[0][1].Should().Be(expectedAdjustmentWindow, "Second element should be adjustment window");
        constraints[0][2].Should().Be(expectedBacklog, "Third element should be backlog");
    }

    [Test]
    public void GetGasPricingConstraints_WithMultipleConstraints_ReturnsAllConstraintsInOrder()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Add multiple constraints
        ulong[][] expectedConstraints =
        [
            [1_000_000, 3600, 500_000],
            [2_000_000, 7200, 1_000_000],
            [3_000_000, 1800, 750_000]
        ];

        foreach (ulong[] constraint in expectedConstraints)
            context.ArbosState.L2PricingState.AddConstraint(constraint[0], constraint[1], constraint[2]);

        // Get gas pricing constraints
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify all constraints are returned in order
        constraints.Should().HaveCount(expectedConstraints.Length);

        for (int i = 0; i < expectedConstraints.Length; i++)
        {
            constraints[i].Should().HaveCount(3);
            constraints[i][0].Should().Be(expectedConstraints[i][0], $"Constraint {i} target should match");
            constraints[i][1].Should().Be(expectedConstraints[i][1], $"Constraint {i} adjustment window should match");
            constraints[i][2].Should().Be(expectedConstraints[i][2], $"Constraint {i} backlog should match");
        }
    }

    [Test]
    public void GetGasPricingConstraints_WithZeroValues_HandlesCorrectly()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Add constraint with zero values
        context.ArbosState.L2PricingState.AddConstraint(0, 0, 0);

        // Get gas pricing constraints
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify the constraint with zeros is returned correctly
        constraints.Should().HaveCount(1);
        constraints[0].Should().Equal([0UL, 0UL, 0UL], "Should handle zero values correctly");
    }
}
