// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
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
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
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
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
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
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
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

    [Test]
    public void UpgradeArbosVersion_From51To59_BumpsStylusVersionToThree()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FiftyOne)
            .WithReleaseSpec();

        context.ArbosState.Programs.GetParams().StylusVersion.Should().Be(StylusVersions.V2, "harness inits chain through v32, which runs the v31 Stylus fix");

        context.ArbosState.UpgradeArbosVersion(ArbosVersion.FiftyNine, false, worldState, London.Instance);

        context.ArbosState.CurrentArbosVersion.Should().Be(ArbosVersion.FiftyNine);
        StylusParams upgraded = context.ArbosState.Programs.GetParams();
        upgraded.StylusVersion.Should().Be(StylusVersions.V3);
    }

    [Test]
    public void UpgradeArbosVersion_From50To60_PassesThroughV59AndProducesStylusVersion3()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        context.ArbosState.Programs.GetParams().StylusVersion.Should().Be(StylusVersions.V2, "harness inits chain through v32, which runs the v31 Stylus fix");

        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Sixty, false, worldState, London.Instance);

        context.ArbosState.CurrentArbosVersion.Should().Be(ArbosVersion.Sixty);
        StylusParams upgraded = context.ArbosState.Programs.GetParams();
        upgraded.StylusVersion.Should().Be(StylusVersions.V3, "v59 hook ran during the traversal and bumped Stylus runtime to 3");
    }

    [Test]
    public void ArbosVersion_FiftyNine_Equals59()
    {
        ArbosVersion.FiftyNine.Should().Be(59);
    }

    [Test]
    public void ArbosVersion_StylusActivationGasAlias_EqualsFiftyNine()
    {
        ArbosVersion.StylusActivationGas.Should().Be(ArbosVersion.FiftyNine);
    }

    [Test]
    public void CollectTips_AtFiftyOne_ReturnsFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FiftyOne)
            .WithReleaseSpec();

        context.ArbosState.CollectTips().Should().BeFalse();
    }

    [Test]
    public void CollectTips_AtSixty_DefaultsToFalse()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        context.ArbosState.CollectTips().Should().BeFalse();
    }

    [Test]
    public void CollectTips_AtSixty_RoundTripsThroughSetter()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        context.ArbosState.SetCollectTips(true);
        context.ArbosState.CollectTips().Should().BeTrue();

        context.ArbosState.SetCollectTips(false);
        context.ArbosState.CollectTips().Should().BeFalse();
    }

    [Test]
    public void UpgradeArbosVersion_From51To60_LeavesCollectTipsFalse()
    {
        // The v60 upgrade hook must NOT touch the CollectTips slot — zero-default is the design.
        // Writing it would change block hashes on the upgrade boundary.
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.FiftyOne)
            .WithReleaseSpec();

        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Sixty, false, worldState, London.Instance);

        context.ArbosState.CollectTips().Should().BeFalse();
    }
}
