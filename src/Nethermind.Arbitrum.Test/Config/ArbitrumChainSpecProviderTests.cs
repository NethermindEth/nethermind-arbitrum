using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Modules;
using Nethermind.Evm.State;
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

        ArbitrumModule module = new(chainSpec);

        ContainerBuilder containerBuilder = new();
        containerBuilder.AddModule(new TestNethermindModule());
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

        // shanghai
        spec2.IsEip4895Enabled.Should().BeFalse();
        spec2.IsEip3651Enabled.Should().BeTrue();
        spec2.IsEip3855Enabled.Should().BeTrue();
        spec2.IsEip3860Enabled.Should().BeTrue();

        // cancun
        spec2.IsEip4844Enabled.Should().BeFalse();
        spec2.IsEip1153Enabled.Should().BeTrue();
        spec2.IsEip4788Enabled.Should().BeTrue();
        spec2.IsEip5656Enabled.Should().BeTrue();
        spec2.IsEip6780Enabled.Should().BeTrue();

        // prague
        spec2.IsEip7702Enabled.Should().BeFalse();
        spec2.IsEip7251Enabled.Should().BeFalse();
        spec2.IsEip2537Enabled.Should().BeFalse();
        spec2.IsEip7002Enabled.Should().BeFalse();
        spec2.IsEip6110Enabled.Should().BeFalse();
    }
}
