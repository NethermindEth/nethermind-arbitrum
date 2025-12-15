using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Modules;
using Nethermind.Evm.State;
using Nethermind.Init.Modules;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Test.Config;

public class ArbitrumChainSpecProviderTests
{
    [Test]
    public void ChainSpecProvider_SeveralLifetimeScopes_OneSpecProviderPerScope()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();

        ArbitrumChainSpecEngineParameters engineParameters =
            chainSpec.EngineChainSpecParametersProvider.GetChainSpecParameters<ArbitrumChainSpecEngineParameters>();
        engineParameters.InitialArbOSVersion = 10;

        ArbitrumModule module = new(chainSpec, new BlocksConfig());

        ContainerBuilder containerBuilder = new();
        containerBuilder.AddModule(new TestNethermindModule(new ConfigProvider(), chainSpec));
        //ArbitrumChainSpecBasedSpecProvider is now dependent on base spec provider instead directly deriving from ChainSpecBasedSpecProvider
        //therefore need to specifically register ChainSpecBasedSpecProvider to be used instead of TestSpecProvider used in TestNethermindModule
        containerBuilder.AddModule(module);
        IContainer rootContainer = containerBuilder.Build();

        // In the root spec provider, the spec uses arbos version 10 (from engine parameters)
        ISpecProvider specProviderFromRootContainer = rootContainer.Resolve<ISpecProvider>();
        ArbitrumDynamicSpecProvider specProvider1 = (ArbitrumDynamicSpecProvider)specProviderFromRootContainer;
        IReleaseSpec spec1 = specProvider1.GetSpec(new ForkActivation(blockNumber: 100));

        // shanghai
        spec1.IsEip4895Enabled.Should().BeFalse();
        spec1.IsEip3651Enabled.Should().BeFalse();
        spec1.IsEip3855Enabled.Should().BeFalse();
        spec1.IsEip3860Enabled.Should().BeFalse();

        // cancun
        spec1.IsEip4844Enabled.Should().BeFalse();
        spec1.IsEip1153Enabled.Should().BeFalse();
        spec1.IsEip4788Enabled.Should().BeFalse();
        spec1.IsEip5656Enabled.Should().BeFalse();
        spec1.IsEip6780Enabled.Should().BeFalse();

        // prague
        spec1.IsEip7702Enabled.Should().BeFalse();
        spec1.IsEip7251Enabled.Should().BeFalse();
        spec1.IsEip2537Enabled.Should().BeFalse();
        spec1.IsEip7002Enabled.Should().BeFalse();
        spec1.IsEip6110Enabled.Should().BeFalse();

        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        // In the scope spec provider, the spec uses arbos version 32 (from arbos state)
        _ = ArbOSInitialization.Create(worldState);
        ArbosState state = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        ILifetimeScope scope = rootContainer.BeginLifetimeScope(builder =>
        {
            builder.AddSingleton(state);
            builder.AddSingleton(worldState);
        });

        ISpecProvider specProviderFromScope = scope.Resolve<ISpecProvider>();
        ArbitrumDynamicSpecProvider specProvider2 = (ArbitrumDynamicSpecProvider)specProviderFromScope;
        IReleaseSpec spec2 = specProvider2.GetSpec(new ForkActivation(blockNumber: 100));

        AssertArbosVersion32Spec(spec2);
    }

    [Test]
    public void ChainSpecProvider_WhenArbOSVersionChanges_ReturnsCorrectSpecAndClearEVMInstructions()
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create();

        ArbitrumModule module = new(chainSpec, new BlocksConfig());

        ContainerBuilder containerBuilder = new();
        //explicitly state we won't use TestSpecProvider
        containerBuilder.AddModule(new TestNethermindModule(new ConfigProvider(), chainSpec, false));
        containerBuilder.AddModule(module);
        IContainer rootContainer = containerBuilder.Build();

        ILifetimeScope scope = rootContainer.BeginLifetimeScope();

        //ok, resolve spec provider before world state is available; ISpecProvider will be resolved as one of the 1st dependencies due to construction of NethermindApi module
        //at this point, only engine parameters are available, but IWorldState is not instantiated yet
        ISpecProvider earlySpecProvider = scope.Resolve<ISpecProvider>();
        IReleaseSpec defaultSpec = earlySpecProvider.GetSpec(new ForkActivation(blockNumber: 100));

        AssertArbosVersion32Spec(defaultSpec);

        //now resolve main processing context to get world state
        var mpc = scope.Resolve<MainProcessingContext>();

        IWorldState worldState = mpc.WorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        //anything from main processing context will now have world state available
        //ArbitrumDynamicSpecProvider will now read ArbOS version from ArbOS state or default to engine parameters
        ISpecProvider worldStateAvailableSpecProvider = mpc.LifetimeScope.Resolve<ISpecProvider>();
        IReleaseSpec emptyArbosSpec = worldStateAvailableSpecProvider.GetSpec(new ForkActivation(blockNumber: 100));

        AssertArbosVersion32Spec(emptyArbosSpec);

        // initialize and update ArbOS state to version 40 - check if spec provider reflects the change
        _ = ArbOSInitialization.Create(worldState);
        ArbosState state = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        state.UpgradeArbosVersion(ArbosVersion.Forty, false, worldState, emptyArbosSpec);

        IReleaseSpec upgradedArbosSpec = worldStateAvailableSpecProvider.GetSpec(new ForkActivation(blockNumber: 100));

        upgradedArbosSpec.IsEip7702Enabled.Should().BeTrue();
        upgradedArbosSpec.IsEip2537Enabled.Should().BeFalse();

        //clear EVM instruction caches to force regeneration with updated spec
        upgradedArbosSpec.EvmInstructionsTraced.Should().BeNull();
        upgradedArbosSpec.EvmInstructionsNoTrace.Should().BeNull();
    }

    [Test]
    [TestCase(10UL, false, false, false, false, TestName = "ArbOS v10 (Pre-Shanghai)")]
    [TestCase(11UL, true, false, false, false, TestName = "ArbOS v11 (Shanghai)")]
    [TestCase(20UL, true, true, false, false, TestName = "ArbOS v20 (Cancun)")]
    [TestCase(30UL, true, true, false, true, TestName = "ArbOS v30 (Stylus + RIP-7212)")]
    [TestCase(40UL, true, true, true, true, TestName = "ArbOS v40 (Prague)")]
    public void SpecProvider_WithDifferentInitialArbOSVersions_ReturnsDynamicSpecsMatchingEachVersion(
        ulong arbOsVersion,
        bool shouldHaveShanghai,
        bool shouldHaveCancun,
        bool shouldHavePrague,
        bool shouldHaveRip7212)
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create(initialArbOsVersion: arbOsVersion);

        Action<ContainerBuilder> configurer = builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                L1BaseFee = 92
            });
        };

        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configurer: configurer,
            chainSpec: chainSpec);

        blockchain.SpecProvider.Should().BeOfType<ArbitrumDynamicSpecProvider>(
            "test infrastructure should use production spec provider");

        IReleaseSpec spec = blockchain.SpecProvider.GenesisSpec;

        AssertForkFeatures("Shanghai", shouldHaveShanghai,
            () => spec.IsEip3651Enabled,
            () => spec.IsEip3855Enabled,
            () => spec.IsEip3860Enabled);

        AssertForkFeatures("Cancun", shouldHaveCancun,
            () => spec.IsEip1153Enabled,
            () => spec.IsEip4788Enabled,
            () => spec.IsEip5656Enabled,
            () => spec.IsEip6780Enabled);

        AssertForkFeatures("Prague", shouldHavePrague,
            () => spec.IsEip7702Enabled,
            () => spec.IsEip2935Enabled);

        AssertForkFeatures("RIP-7212", shouldHaveRip7212,
            () => spec.IsRip7212Enabled);
    }

    [Test]
    [TestCase(29UL, false, TestName = "ArbOS v29 - RIP-7212 Disabled")]
    [TestCase(30UL, true, TestName = "ArbOS v30 (Stylus) - RIP-7212 Enabled")]
    [TestCase(31UL, true, TestName = "ArbOS v31+ - RIP-7212 Enabled")]
    public void SpecProvider_WithDifferentArbOSVersions_ReturnsCorrectRip7212Status(
        ulong arbOsVersion,
        bool shouldHaveRip7212)
    {
        ChainSpec chainSpec = FullChainSimulationChainSpecProvider.Create(initialArbOsVersion: arbOsVersion);

        Action<ContainerBuilder> configurer = builder =>
        {
            builder.AddScoped(new ArbitrumTestBlockchainBase.Configuration
            {
                SuggestGenesisOnStart = true,
                L1BaseFee = 92
            });
        };

        using ArbitrumRpcTestBlockchain blockchain = ArbitrumRpcTestBlockchain.CreateDefault(
            configurer: configurer,
            chainSpec: chainSpec);

        IReleaseSpec spec = blockchain.SpecProvider.GenesisSpec;

        spec.IsRip7212Enabled.Should().Be(shouldHaveRip7212,
            $"RIP-7212 should be {(shouldHaveRip7212 ? "enabled" : "disabled")} at ArbOS version {arbOsVersion}");
    }

    private static void AssertForkFeatures(string forkName, bool shouldBeEnabled, params Func<bool>[] featureChecks)
    {
        foreach (Func<bool> check in featureChecks)
        {
            check().Should().Be(shouldBeEnabled, $"{forkName} features should be {(shouldBeEnabled ? "enabled" : "disabled")}");
        }
    }

    private void AssertArbosVersion32Spec(IReleaseSpec spec)
    {
        // shanghai
        spec.IsEip4895Enabled.Should().BeFalse();
        spec.IsEip3651Enabled.Should().BeTrue();
        spec.IsEip3855Enabled.Should().BeTrue();
        spec.IsEip3860Enabled.Should().BeTrue();

        // cancun
        spec.IsEip4844Enabled.Should().BeFalse();
        spec.IsEip1153Enabled.Should().BeTrue();
        spec.IsEip4788Enabled.Should().BeTrue();
        spec.IsEip5656Enabled.Should().BeTrue();
        spec.IsEip6780Enabled.Should().BeTrue();

        // prague
        spec.IsEip7702Enabled.Should().BeFalse();
        spec.IsEip7251Enabled.Should().BeFalse();
        spec.IsEip2537Enabled.Should().BeFalse();
        spec.IsEip7002Enabled.Should().BeFalse();
        spec.IsEip6110Enabled.Should().BeFalse();

        // RIP-7212 (enabled from ArbOS v30+, so v32 should have it)
        spec.IsRip7212Enabled.Should().BeTrue();
    }
}
