using Autofac;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Evm.State;
using Nethermind.State;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;

namespace Nethermind.Arbitrum.Config;

public class ArbitrumBlockProducerEnvFactory : BlockProducerEnvFactory
{
    protected IBlockProducerTxSourceFactory TxSourceFactory;
    protected IWorldStateManager WorldStateManager;
    protected ILifetimeScope RootLifetime;

    public ArbitrumBlockProducerEnvFactory(
        ILifetimeScope rootLifetime,
        IWorldStateManager worldStateManager,
        IBlockProducerTxSourceFactory txSourceFactory) : base(rootLifetime, worldStateManager, txSourceFactory)
    {
        TxSourceFactory = txSourceFactory;
        WorldStateManager = worldStateManager;
        RootLifetime = rootLifetime;
    }

    protected override ContainerBuilder ConfigureBuilder(ContainerBuilder builder)
    {
        return base.ConfigureBuilder(builder)
            .AddScoped(BlockchainProcessor.Options.Default)
            .AddScoped<IReceiptStorage, PersistentReceiptStorage>()
            .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ArbitrumBlockProductionTransactionsExecutor>()
            .AddScoped<IBlockProducerEnv, BlockProducerEnv>();
    }

    public override IBlockProducerEnv Create()
    {
        IWorldState worldState = WorldStateManager.GlobalWorldState;
        ILifetimeScope lifetimeScope = RootLifetime.BeginLifetimeScope(builder =>
            ConfigureBuilder(builder)
                .AddScoped(worldState));

        RootLifetime.Disposer.AddInstanceForAsyncDisposal(lifetimeScope);

        return lifetimeScope.Resolve<IBlockProducerEnv>();
    }
}
