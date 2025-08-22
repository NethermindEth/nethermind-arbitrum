using Autofac;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public class ArbitrumRpcTestBlockchain : ArbitrumTestBlockchainBase
{
    private ArbitrumRpcTestBlockchain()
    {
    }

    public IArbitrumRpcModule ArbitrumRpcModule { get; private set; } = null!;
    public IArbitrumSpecHelper SpecHelper => Dependencies.SpecHelper;

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
            chain.WorldStateManager,
            chain.BlockTree,
            chain.BlockProductionTrigger,
            chain.ArbitrumRpcTxSource,
            chain.ChainSpec,
            chain.Dependencies.SpecHelper,
            chain.LogManager,
            chain.Dependencies.CachedL1PriceData,
            chain.BlockProcessingQueue,
            chain.Container.Resolve<IArbitrumConfig>())
            .Create();

        return chain;
    }

    public class Builder(ArbitrumRpcTestBlockchain chain)
    {
        public ArbitrumRpcTestBlockchain Build(Action<ContainerBuilder>? configurer = null)
        {
            return CreateInternal(chain, configurer);
        }
    }
}
