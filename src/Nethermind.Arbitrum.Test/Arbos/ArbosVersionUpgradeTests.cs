using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.State;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Evm.State;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Arbos;

[TestFixture]
public class ArbosVersionUpgradeTests
{
    [Test]
    public void UpgradeArbosVersion_From32To41_EnablesNativeTokenManager()
    {
        // Initialize ArbOS state at version 32
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.ThirtyTwo)
            .WithReleaseSpec();

        // Verify initial version
        context.ArbosState.CurrentArbosVersion.Should().Be(ArbosVersion.ThirtyTwo);

        // Perform upgrade to v41
        context.ArbosState.UpgradeArbosVersion(ArbosVersion.FortyOne, false, worldState, London.Instance);

        // Verify the current version upgraded to v41
        context.ArbosState.CurrentArbosVersion.Should().Be(ArbosVersion.FortyOne);
    }

    [Test]
    public void UpgradeArbosVersion_From40To50_CapsMaxStackDepth()
    {
        // Initialize ArbOS state at version 40
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Forty)
            .WithReleaseSpec();

        // Set MaxStackDepth to a value above the v50 cap (22,000)
        StylusParams stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.SetMaxStackDepth(30000); // Set to 30,000 (above cap 22000)
        stylusParams.Save();

        // Verify the initial value is above cap
        stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.MaxStackDepth.Should().Be(30000, "Initial MaxStackDepth should be set to 30000 before upgrade");

        // Perform upgrade to v50
        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Fifty, false, worldState, London.Instance);

        // Verify MaxStackDepth is capped at exactly 22,000 after v50 upgrade
        stylusParams = context.ArbosState.Programs.GetParams();
        stylusParams.MaxStackDepth.Should().Be(22000, "MaxStackDepth should be capped at exactly 22000 after v50 upgrade");
    }

    [Test]
    public void UpgradeArbosVersion_From40To50_SetsPerTxGasLimit()
    {
        // Initialize ArbOS state at version 40
        IArbitrumWorldState worldState = TestArbitrumWorldState.CreateNewInMemory();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Forty)
            .WithReleaseSpec();

        // Get L2 pricing state
        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Verify PerTxGasLimit is not set before v50
        ulong perTxGasLimitBefore = l2Pricing.PerTxGasLimitStorage.Get();
        perTxGasLimitBefore.Should().Be(0);

        // Perform upgrade to v50
        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Fifty, false, worldState, London.Instance);

        // Verify PerTxGasLimit is set to 32 M after v50 upgrade
        ulong perTxGasLimitAfter = l2Pricing.PerTxGasLimitStorage.Get();
        perTxGasLimitAfter.Should().Be(L2PricingState.InitialPerTxGasLimit, "PerTxGasLimit should be set to 32M after v50 upgrade");
        perTxGasLimitAfter.Should().Be(32_000_000);
    }
}
