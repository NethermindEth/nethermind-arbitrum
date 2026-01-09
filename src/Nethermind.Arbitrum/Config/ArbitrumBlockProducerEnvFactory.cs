using Autofac;
using Nethermind.Arbitrum.Evm;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.State;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumBlockProducerEnvFactory : BlockProducerEnvFactory
{
    public ArbitrumBlockProducerEnvFactory(
        ILifetimeScope rootLifetime,
        IWorldStateManager worldStateManager,
        IBlockProducerTxSourceFactory txSourceFactory) : base(rootLifetime, worldStateManager, txSourceFactory)
    {
    }

    protected override ContainerBuilder ConfigureBuilder(ContainerBuilder builder)
    {
        return base.ConfigureBuilder(builder)
            .AddScoped<BlockProcessor.IBlockProductionTransactionsExecutor<ArbitrumGas>, ArbitrumBlockProductionTransactionsExecutor<ArbitrumGas>>();
    }
}

public class ArbitrumGlobalWorldStateBlockProducerEnvFactory : GlobalWorldStateBlockProducerEnvFactory
{
    public ArbitrumGlobalWorldStateBlockProducerEnvFactory(
        ILifetimeScope rootLifetime,
        IWorldStateManager worldStateManager,
        IBlockProducerTxSourceFactory txSourceFactory) : base(rootLifetime, worldStateManager, txSourceFactory)
    {
    }

    protected override ContainerBuilder ConfigureBuilder(ContainerBuilder builder)
    {
        return base.ConfigureBuilder(builder)
            .AddScoped<BlockProcessor.IBlockProductionTransactionsExecutor<ArbitrumGas>, ArbitrumBlockProductionTransactionsExecutor<ArbitrumGas>>();
    }
}
