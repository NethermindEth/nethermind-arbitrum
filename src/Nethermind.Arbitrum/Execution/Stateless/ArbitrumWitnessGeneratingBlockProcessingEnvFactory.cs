// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Headers;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Stateless;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Consensus.Transactions;
using Nethermind.Consensus.Producers;
using Nethermind.Config;
using Nethermind.Evm;
using Nethermind.Arbitrum.Config;

namespace Nethermind.Arbitrum.Execution.Stateless;

public interface IArbitrumWitnessGeneratingBlockProcessingEnvFactory : IWitnessGeneratingBlockProcessingEnvFactory
{
    IWitnessGeneratingBlockProcessingEnvScope CreateScope(string[]? wasmTargets);
}

public class ArbitrumWitnessGeneratingBlockProcessingEnvFactory(
    ILifetimeScope rootLifetimeScope,
    ReconstructedStateTrieStore reconstructedStateTrieStore,
    IDbProvider dbProvider,
    ILogManager logManager) : IArbitrumWitnessGeneratingBlockProcessingEnvFactory
{
    // TODO: check debug endpoint exec later (compare with nitro) -- Not priority for now
    public IWitnessGeneratingBlockProcessingEnvScope CreateScope() => CreateScope(null);

    public IWitnessGeneratingBlockProcessingEnvScope CreateScope(string[]? wasmTargets)
    {
        IReadOnlyDbProvider readOnlyDbProvider = new ReadOnlyDbProvider(dbProvider, true);
        WitnessCapturingTrieStore trieStore = new(reconstructedStateTrieStore);
        // Execution and state-root recomputation must reach the capturing store through the
        // synchronized wrapper: BulkSet's parallel workers would otherwise corrupt its
        // single-writer collector. The concrete capturing store is still handed to
        // WitnessGeneratingWorldState below, which only drains it single-threaded in GetWitness.
        ITrieStore synchronizedTrieStore = new SynchronizedTrieStore(trieStore);
        IStateReader stateReader = new StateReader(synchronizedTrieStore, readOnlyDbProvider.CodeDb, logManager);
        WorldState worldState = new(new TrieStoreScopeProvider(synchronizedTrieStore, readOnlyDbProvider.CodeDb, logManager), logManager);

        IBlocksConfig blocksConfig = rootLifetimeScope.Resolve<IBlocksConfig>();

        ILifetimeScope envLifetimeScope = rootLifetimeScope.BeginLifetimeScope((builder) =>
        {
            if (wasmTargets is not null)
                // No need to redeclare IWasmStore because it is now declared as scoped
                // and therefore a new instance will be created in this child scope with the correct dependencies
                builder.AddScoped<IStylusTargetConfig>(_ => new StylusTargetConfig() { OverrideWasmTargets = wasmTargets });

            builder
                .AddScoped<IStateReader>(stateReader)
                .AddScoped<ArbitrumUserWasmsRecorder>()

                .AddScoped<WitnessGeneratingHeaderFinder, IHeaderStore>(headerStore => new WitnessGeneratingHeaderFinder(headerStore))
                .BindScoped<IHeaderFinder, WitnessGeneratingHeaderFinder>()

                .AddScoped<WitnessGeneratingWorldState, WitnessGeneratingHeaderFinder>(headerFinder =>
                    new WitnessGeneratingWorldState(worldState, stateReader, trieStore, headerFinder))
                .BindScoped<IWorldState, WitnessGeneratingWorldState>()

                .AddScoped<IBlocksConfig>(_ => ArbitrumStateReconstructionBlockProcessingEnvFactory.CreateReplayBlocksConfig(blocksConfig))

                // We give a NoOp l1BlockCache to the vm so that it forces querying
                // the world state to record state accesses.
                // The VM gets its own private BlockhashProvider backed by WitnessGeneratingHeaderFinder so that
                // blockhash lookups are recorded in the witness. We do NOT register IBlockhashProvider/IBlockhashCache
                // in the child scope so that BranchProcessor (which is AddScoped and calls Prefetch()) falls back to
                // the root scope's unrecorded provider and does not pollute the witness with prefetch header lookups.
                .AddScoped<ArbitrumVirtualMachine>(ctx =>
                {
                    ILogManager log = ctx.Resolve<ILogManager>();
                    BlockhashCache recordingCache = new(ctx.Resolve<WitnessGeneratingHeaderFinder>(), log);
                    BlockhashProvider recordingProvider = new(recordingCache, ctx.Resolve<IWorldState>(), log);
                    return new ArbitrumVirtualMachine(
                        ctx.Resolve<IArbitrumSpecHelper>(),
                        recordingProvider,
                        ctx.Resolve<IWasmStore>(),
                        ctx.Resolve<ISpecProvider>(),
                        log,
                        new NoOpL1BlockCache(),
                        enableWitnessGeneration: true,
                        wasmsRecorder: ctx.Resolve<ArbitrumUserWasmsRecorder>());
                })

                // Pass CodeInfoRepository, which does not cache anything, forcing querying the
                // the world state to record state accesses.
                .AddScoped<ICodeInfoRepository, IWorldState, IArbosVersionProvider>((state, versionProvider) =>
                    new ArbitrumCodeInfoRepository(
                        new CodeInfoRepository(state, new EthereumPrecompileProvider()),
                        versionProvider,
                        state as WitnessGeneratingWorldState))

                .AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>()

                // 1st: add the tx executor
                .AddScoped<ITransactionProcessorAdapter, BuildUpTransactionProcessorAdapter>()
                .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ArbitrumBlockProductionTransactionsExecutor>()

                // 2nd: add block processor
                .AddScoped<IReceiptStorage>(NullReceiptStorage.Instance)
                .AddScoped(BlockchainProcessor.Options.NoReceipts)
                .AddScoped<IBlockProcessor, ArbitrumBlockProcessor>()

                // 3rd: configure the builder for block production (like ArbitrumBlockProducerEnvFactory but with my own witness capturing world state)
                .AddScoped<ITxSource, IBlockProducerTxSourceFactory>(factory => factory.Create())
                .AddDecorator<IWithdrawalProcessor, BlockProductionWithdrawalProcessor>()
                .AddDecorator<IBlockchainProcessor, OneTimeChainProcessor>()
                .AddScoped<IBlockProducerEnv, BlockProducerEnv>()

                .AddScoped<IWitnessGeneratingBlockProcessingEnv, ArbitrumWitnessGeneratingBlockProcessingEnv>();
        });

        return new ExecutionRecordingScope(envLifetimeScope);
    }

    // Unlike the upstream factory, which pools and resets env entries across rents, this factory
    // builds a fresh env per scope — disposing the lifetime scope releases everything.
    private sealed class ExecutionRecordingScope(ILifetimeScope envLifetimeScope) : IWitnessGeneratingBlockProcessingEnvScope
    {
        public IWitnessGeneratingBlockProcessingEnv Env { get; } = envLifetimeScope.Resolve<IWitnessGeneratingBlockProcessingEnv>();

        public void Dispose() => envLifetimeScope.Dispose();
    }
}
