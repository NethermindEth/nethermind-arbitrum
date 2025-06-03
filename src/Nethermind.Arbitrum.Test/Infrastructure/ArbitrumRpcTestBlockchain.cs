using Autofac;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class ArbitrumRpcTestBlockchain : ArbitrumTestBlockchainBase
{
    private ArbitrumRpcTestBlockchain()
    {
    }

    public IArbitrumRpcModule ArbitrumRpcModule { get; private set; } = null!;

    public static Builder Customize()
    {
        return new Builder(new ArbitrumRpcTestBlockchain());
    }

    public static ArbitrumRpcTestBlockchain CreateDefault(Action<ContainerBuilder>? configurer = null)
    {
        return CreateInternal(new ArbitrumRpcTestBlockchain(), configurer);
    }

    private static ArbitrumRpcTestBlockchain CreateInternal(ArbitrumRpcTestBlockchain chain, Action<ContainerBuilder>? configurer)
    {
        chain.Build(configurer);

        chain.ArbitrumRpcModule = new ArbitrumRpcModuleFactory(
            chain.Container.Resolve<ArbitrumBlockTreeInitializer>(),
            chain.BlockTree,
            chain.BlockProductionTrigger,
            chain.ArbitrumRpcTxSource,
            chain.ChainSpec,
            chain.ArbitrumConfig,
            chain.LogManager.GetClassLogger<ArbitrumRpcModule>())
            .Create();

        return chain;
    }

    public class Builder(ArbitrumRpcTestBlockchain chain)
    {
        public Builder WithConfig(Action<ArbitrumConfig> configurer)
        {
            configurer.Invoke(chain.ArbitrumConfig);
            return this;
        }

        public ArbitrumRpcTestBlockchain Build(Action<ContainerBuilder>? configurer = null)
        {
            return CreateInternal(chain, configurer);
        }
    }
}
