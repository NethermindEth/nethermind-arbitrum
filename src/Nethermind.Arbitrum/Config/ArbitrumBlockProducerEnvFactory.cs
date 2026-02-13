// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using Nethermind.Arbitrum.Execution;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;
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
    private readonly IArbitrumConfig _arbitrumConfig;

    public ArbitrumGlobalWorldStateBlockProducerEnvFactory(
        ILifetimeScope rootLifetime,
        IWorldStateManager worldStateManager,
        IBlockProducerTxSourceFactory txSourceFactory,
        IBlocksConfig blocksConfig,
        IArbitrumConfig arbitrumConfig) : base(rootLifetime, worldStateManager, txSourceFactory)
    {
        _blocksConfig = blocksConfig;
        _arbitrumConfig = arbitrumConfig;
    }

    protected override ContainerBuilder ConfigureBuilder(ContainerBuilder builder)
    {
        ContainerBuilder baseBuilder = base.ConfigureBuilder(builder)
            .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ArbitrumBlockProductionTransactionsExecutor>();

        if (_arbitrumConfig.DigestMessagePrefetchEnabled)
        {
            return baseBuilder
                .AddSingleton<NodeStorageCache>()
                // Singleton so that all child env share the same caches. Note: this module is applied per-processing
                // module, so singleton here is like scoped but exclude inner prewarmer lifetime.
                //.AddSingleton<DoublePreBlockCaches>()
                .AddSingleton<IPreBlockCachesInner, DoublePreBlockCaches>()
                //.AddScoped<IBlockCachePreWarmer, IPrewarmerEnvFactory, NodeStorageCache, DoublePreBlockCaches, ILogManager>((envFactory, nodeStorage,
                //    blockCaches, logManager) =>
                //{
                //    return new ArbitrumBlockCachePreWarmer(envFactory, _blocksConfig, nodeStorage, blockCaches, logManager);
                //})
                .AddScoped<ArbitrumBlockCachePreWarmer>()
                .Add<IPrewarmerEnvFactory, ArbitrumPrewarmerEnvFactory>()

                // These are the actual decorated component that provide cached result
                .AddDecorator<IWorldStateScopeProvider>((ctx, worldStateScopeProvider) =>
                {
                    if (worldStateScopeProvider is ArbitrumPrewarmerScopeProvider)
                        return worldStateScopeProvider; // Inner world state

                    IPreBlockCachesInner doubleCaches = ctx.Resolve<IPreBlockCachesInner>();

                    return new ArbitrumPrewarmerScopeProvider(
                        worldStateScopeProvider,
                        doubleCaches,
                        populatePreBlockCache: false,
                        ctx.Resolve<ILogManager>());
                })
                .AddDecorator<ICodeInfoRepository>((ctx, originalCodeInfoRepository) =>
                {
                    IBlocksConfig blocksConfig = ctx.Resolve<IBlocksConfig>();
                    IPreBlockCachesInner doubleCaches = ctx.Resolve<IPreBlockCachesInner>();
                    PreBlockCaches? preBlockCaches = null;
                    if (doubleCaches is DoublePreBlockCaches caches)
                        preBlockCaches = caches.Front;

                    IPrecompileProvider precompileProvider = ctx.Resolve<IPrecompileProvider>();
                    // Note: The use of FrozenDictionary means that this cannot be used for other processing env also due to risk of memory leak.
                    return new CachedCodeInfoRepository(precompileProvider, originalCodeInfoRepository,
                        blocksConfig.CachePrecompilesOnBlockProcessing ? preBlockCaches?.PrecompileCache : null);
                })
                .AddSingleton<IPrefetchManager, ArbitrumPrefetchManager>()
                .AddDecorator<IWorldState, PrefetchAwareWorldState>();
        }

        return baseBuilder;
    }
}
