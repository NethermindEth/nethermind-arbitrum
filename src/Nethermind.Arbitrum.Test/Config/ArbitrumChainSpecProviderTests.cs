using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Modules;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

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

        ArbitrumModule module = new ArbitrumModule(chainSpec);

        var containerBuilder = new ContainerBuilder();
        containerBuilder.AddModule(new TestNethermindModule());
        containerBuilder.AddModule(module);
        IContainer rootContainer = containerBuilder.Build();

        (IWorldState worldState, _) = ArbOSInitialization.Create();
        ArbosState state = ArbosState.OpenArbosState(worldState, new SystemBurner(), NullLogger.Instance);
        ILifetimeScope scope = rootContainer.BeginLifetimeScope(builder => builder.AddSingleton<ArbosState>(state));

        // In the root spec provider, the spec uses arbos version 10 (from engine parameters)
        var specProviderFromRootContainer = rootContainer.Resolve<ISpecProvider>();
        var specProvider1 = (ArbitrumChainSpecBasedSpecProvider)specProviderFromRootContainer;
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

        // In the scope spec provider, the spec uses arbos version 32 (from arbos state)
        var specProviderFromScope = scope.Resolve<ISpecProvider>();
        var specProvider2 = (ArbitrumChainSpecBasedSpecProvider)specProviderFromScope;
        IReleaseSpec spec2 = specProvider2.GetSpec(new ForkActivation(blockNumber: 100));

        // shanghai
        spec2.IsEip4895Enabled.Should().BeTrue();
        spec2.IsEip3651Enabled.Should().BeTrue();
        spec2.IsEip3855Enabled.Should().BeTrue();
        spec2.IsEip3860Enabled.Should().BeTrue();

        // cancun
        spec2.IsEip4844Enabled.Should().BeTrue();
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
