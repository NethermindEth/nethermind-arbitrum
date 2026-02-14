// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
    IReadOnlyTrieStore readOnlyTrieStore,
    IDbProvider dbProvider,
    ILogManager logManager) : IArbitrumWitnessGeneratingBlockProcessingEnvFactory
{
    // To force processing in BlockchainProcessor even though block is not better than head (already existing block)
    private static BlocksConfig CreateWitnessBlocksConfig(IBlocksConfig blocksConfig)
        => new()
        {
            TargetBlockGasLimit = blocksConfig.TargetBlockGasLimit,
            MinGasPrice = blocksConfig.MinGasPrice,
            RandomizedBlocks = blocksConfig.RandomizedBlocks,
            ExtraData = blocksConfig.ExtraData,
            SecondsPerSlot = blocksConfig.SecondsPerSlot,
            SingleBlockImprovementOfSlot = blocksConfig.SingleBlockImprovementOfSlot,
            PreWarmStateOnBlockProcessing = blocksConfig.PreWarmStateOnBlockProcessing,
            CachePrecompilesOnBlockProcessing = blocksConfig.CachePrecompilesOnBlockProcessing,
            PreWarmStateConcurrency = blocksConfig.PreWarmStateConcurrency,
            BlockProductionTimeoutMs = blocksConfig.BlockProductionTimeoutMs,
            GenesisTimeoutMs = blocksConfig.GenesisTimeoutMs,
            BlockProductionMaxTxKilobytes = blocksConfig.BlockProductionMaxTxKilobytes,
            GasToken = blocksConfig.GasToken,
            BlockProductionBlobLimit = blocksConfig.BlockProductionBlobLimit,
            BuildBlocksOnMainState = false,
        };

    private ITransactionProcessor CreateTransactionProcessor(
        IArbitrumSpecHelper arbitrumSpecHelper,
        IWasmStore wasmStore,
        ISpecProvider specProvider,
        IArbosVersionProvider arbosVersionProvider,
        IWorldState state,
        IHeaderFinder witnessGeneratingHeaderFinder,
        ArbitrumUserWasmsRecorder wasmsRecorder)
    {
        BlockhashProvider blockhashProvider = new(new BlockhashCache(witnessGeneratingHeaderFinder, logManager), state, logManager);
        // We don't give any l1BlockCache to the vm so that it forces querying the world state
        ArbitrumVirtualMachine vm = new(arbitrumSpecHelper, blockhashProvider, wasmStore, specProvider, logManager, enableWitnessGeneration: true, wasmsRecorder: wasmsRecorder);

        return new ArbitrumTransactionProcessor(
            BlobBaseFeeCalculator.Instance, specProvider, state, wasmStore, vm, logManager,
            new ArbitrumCodeInfoRepository(new CodeInfoRepository(state, new EthereumPrecompileProvider()),
            arbosVersionProvider, state as WitnessGeneratingWorldState));
    }

    // TODO: check debug endpoint exec later (compare with nitro) -- Not priority for now
    public IWitnessGeneratingBlockProcessingEnvScope CreateScope() => CreateScope(null);

    public IWitnessGeneratingBlockProcessingEnvScope CreateScope(string[]? wasmTargets)
    {
        IReadOnlyDbProvider readOnlyDbProvider = new ReadOnlyDbProvider(dbProvider, true);
        WitnessCapturingTrieStore trieStore = new(readOnlyDbProvider.StateDb, readOnlyTrieStore);
        IStateReader stateReader = new StateReader(trieStore, readOnlyDbProvider.CodeDb, logManager);
        WorldState worldState = new(new TrieStoreScopeProvider(trieStore, readOnlyDbProvider.CodeDb, logManager), logManager);

        ArbitrumUserWasmsRecorder wasmsRecorder = new();
        IBlocksConfig blocksConfig = rootLifetimeScope.Resolve<IBlocksConfig>();

        ILifetimeScope envLifetimeScope = rootLifetimeScope.BeginLifetimeScope((builder) =>
        {
            if (wasmTargets is not null)
            {
                builder.AddScoped<IStylusTargetConfig>(_ => new StylusTargetConfig() { OverrideWasmTargets = wasmTargets });
                // Need to redeclare IWasmStore because it was originally declared as a singleton and therefore was cached with original IStylusTargetConfig
                builder.AddScoped<IWasmStore>(ctx => new WasmStore(ctx.Resolve<IWasmDb>(), ctx.Resolve<IStylusTargetConfig>(), cacheTag: 1));
            }

            builder
                .AddScoped<IStateReader>(stateReader)

                .AddScoped<IHeaderFinder>(builder => new WitnessGeneratingHeaderFinder(builder.Resolve<IHeaderStore>()))
                .AddScoped<IWorldState>(builder => new WitnessGeneratingWorldState(worldState, stateReader, trieStore, (builder.Resolve<IHeaderFinder>() as WitnessGeneratingHeaderFinder)!))

                .AddScoped<IBlocksConfig>(_ => CreateWitnessBlocksConfig(blocksConfig))

                .AddScoped<ITransactionProcessor>(builder => CreateTransactionProcessor(
                    builder.Resolve<IArbitrumSpecHelper>(),
                    builder.Resolve<IWasmStore>(),
                    builder.Resolve<ISpecProvider>(),
                    builder.Resolve<IArbosVersionProvider>(),
                    builder.Resolve<IWorldState>(),
                    builder.Resolve<IHeaderFinder>(),
                    wasmsRecorder))

                // 1st: add the tx executor
                .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ArbitrumBlockProductionTransactionsExecutor>()

                // 2nd: add block processor
                .AddScoped<IReceiptStorage>(NullReceiptStorage.Instance)
                .AddScoped(BlockchainProcessor.Options.NoReceipts)
                .AddScoped<IBlockProcessor, ArbitrumBlockProcessor>()

                // 3rd: configure the builder for block production (like ArbitrumBlockProducerEnvFactory but with my own witness capturing world state)
                .AddScoped<ITxSource>(builder => builder.Resolve<IBlockProducerTxSourceFactory>().Create())
                .AddScoped<ITransactionProcessorAdapter, BuildUpTransactionProcessorAdapter>()
                .AddDecorator<IWithdrawalProcessor, BlockProductionWithdrawalProcessor>()
                .AddDecorator<IBlockchainProcessor, OneTimeChainProcessor>()
                .AddScoped<IBlockProducerEnv, BlockProducerEnv>()

                .AddScoped<IWitnessGeneratingBlockProcessingEnv>(builder =>
                    new ArbitrumWitnessGeneratingBlockProcessingEnv(
                        builder.Resolve<ITxSource>(),
                        builder.Resolve<IBlockchainProcessor>(),
                        builder.Resolve<IReadOnlyBlockTree>(),
                        (builder.Resolve<IWorldState>() as WitnessGeneratingWorldState)!,
                        builder.Resolve<IBlocksConfig>(),
                        builder.Resolve<ISpecProvider>(),
                        builder.Resolve<IArbitrumSpecHelper>(),
                        wasmsRecorder,
                        logManager));
        });

        return new ExecutionRecordingScope(envLifetimeScope);
    }
}
