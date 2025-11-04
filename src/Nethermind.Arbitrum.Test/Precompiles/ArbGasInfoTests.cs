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
    public void GetGasPricingConstraints_ReturnsStubValue()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Get gas pricing constraints
        ArbGasInfo.GasPricingConstraints constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify it returns the stub value (0, 0, 0)
        // Note: This is a stub implementation until multi-constraint pricing is fully implemented
        constraints.MaxTxGasLimit.Should().Be(0, "GetGasPricingConstraints is currently a stub");
        constraints.MaxBlockGasLimit.Should().Be(0, "GetGasPricingConstraints is currently a stub");
        constraints.Reserved.Should().Be(0, "GetGasPricingConstraints is currently a stub");
    }
}
