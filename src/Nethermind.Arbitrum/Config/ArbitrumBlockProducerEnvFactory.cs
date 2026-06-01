// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Logging;
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
            .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ArbitrumBlockProductionTransactionsExecutor>();
    }
}

public class ArbitrumGlobalWorldStateBlockProducerEnvFactory : GlobalWorldStateBlockProducerEnvFactory
{
    private readonly IBlocksConfig _blocksConfig;
    public ArbitrumGlobalWorldStateBlockProducerEnvFactory(
        ILifetimeScope rootLifetime,
        IWorldStateManager worldStateManager,
        IBlockProducerTxSourceFactory txSourceFactory,
        IBlocksConfig blocksConfig) : base(rootLifetime, worldStateManager, txSourceFactory)
    {
        _blocksConfig = blocksConfig;
    }

    protected override ContainerBuilder ConfigureBuilder(ContainerBuilder builder)
    {
        ContainerBuilder baseBuilder = base.ConfigureBuilder(builder)
            .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ArbitrumBlockProductionTransactionsExecutor>();

        if (_blocksConfig.PreWarmStateOnBlockProcessing)
        {
            return baseBuilder
                // Singleton so that all child env share the same caches. Note: this module is applied per-processing
                // module, so singleton here is like scoped but exclude inner prewarmer lifetime.
                .AddSingleton<PreBlockCaches>()
                .AddScoped<IBlockCachePreWarmer, BlockCachePreWarmer>()
                .Add<PrewarmerEnvFactory>()

                // These are the actual decorated component that provide cached result
                .AddDecorator<IWorldStateScopeProvider>((ctx, worldStateScopeProvider) =>
                {
                    if (worldStateScopeProvider is PrewarmerScopeProvider)
                        return worldStateScopeProvider; // Inner world state

                    return new PrewarmerScopeProvider(
                        worldStateScopeProvider,
                        ctx.Resolve<PreBlockCaches>(),
                        ctx.Resolve<ILogManager>(),
                        populatePreBlockCache: false);
                })
                .AddDecorator<ICodeInfoRepository>((ctx, originalCodeInfoRepository) =>
                {
                    IBlocksConfig blocksConfig = ctx.Resolve<IBlocksConfig>();
                    PreBlockCaches preBlockCaches = ctx.Resolve<PreBlockCaches>();
                    IPrecompileProvider precompileProvider = ctx.Resolve<IPrecompileProvider>();
                    IWorldState worldState = ctx.Resolve<IWorldState>();
                    // Note: The use of FrozenDictionary means that this cannot be used for other processing env also due to risk of memory leak.
                    return new PrecompileCachedCodeInfoRepository(worldState, precompileProvider, originalCodeInfoRepository,
                        blocksConfig.CachePrecompilesOnBlockProcessing ? preBlockCaches?.PrecompileCache : null);
                });
        }

        return baseBuilder;
    }
}
