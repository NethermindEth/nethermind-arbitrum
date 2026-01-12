// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Headers;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Stateless;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class ArbitrumWitnessGeneratingBlockProcessingEnvFactory(
    ILifetimeScope rootLifetimeScope,
    IReadOnlyTrieStore readOnlyTrieStore,
    IDbProvider dbProvider,
    ILogManager logManager) : IWitnessGeneratingBlockProcessingEnvFactory
{
    public IWitnessGeneratingBlockProcessingEnvScope CreateScope()
    {
        IReadOnlyDbProvider readOnlyDbProvider = new ReadOnlyDbProvider(dbProvider, true);
        WitnessCapturingTrieStore trieStore = new(readOnlyDbProvider.StateDb, readOnlyTrieStore);
        IStateReader stateReader = new StateReader(trieStore, readOnlyDbProvider.CodeDb, logManager);
        IWorldState worldState = new WorldState(new TrieStoreScopeProvider(trieStore, readOnlyDbProvider.CodeDb, logManager), logManager);

        ILifetimeScope envLifetimeScope = rootLifetimeScope.BeginLifetimeScope((builder) => builder
            .AddScoped<IStateReader>(stateReader)
            .AddScoped<IWorldState>(worldState)
            .AddScoped<IWitnessGeneratingBlockProcessingEnv>(builder =>
                new ArbitrumWitnessGeneratingBlockProcessingEnv(
                    builder.Resolve<ISpecProvider>(),
                    (builder.Resolve<IWorldState>() as WorldState)!,
                    builder.Resolve<IStateReader>(),
                    // (builder.Resolve<IBlockProcessor.IBlockTransactionsExecutor>() as ArbitrumBlockProductionTransactionsExecutor)!,
                    trieStore,
                    builder.Resolve<IReadOnlyBlockTree>(),
                    builder.Resolve<ISealValidator>(),
                    builder.Resolve<IRewardCalculator>(),
                    builder.Resolve<IHeaderStore>(),
                    builder.Resolve<IWasmStore>(),
                    builder.Resolve<IArbosVersionProvider>(),
                    logManager)));

        return new ExecutionRecordingScope(envLifetimeScope);
    }
}
