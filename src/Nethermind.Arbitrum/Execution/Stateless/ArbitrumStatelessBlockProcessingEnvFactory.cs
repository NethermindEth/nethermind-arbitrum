// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Blockchain.BeaconBlockRoot;
using Nethermind.Blockchain.Blocks;
using Nethermind.Blockchain.Receipts;
using Nethermind.Consensus;
using Nethermind.Consensus.ExecutionRequests;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Rewards;
using Nethermind.Consensus.Stateless;
using Nethermind.Consensus.Validators;
using Nethermind.Consensus.Withdrawals;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Execution.Stateless;

public interface IArbitrumStatelessBlockProcessingEnvScope : IDisposable
{
    IWorldState WorldState { get; }
    IBlockProcessor BlockProcessor { get; }
}

public sealed class ArbitrumStatelessBlockProcessingEnvScope(ILifetimeScope scope) : IArbitrumStatelessBlockProcessingEnvScope
{
    public IWorldState WorldState { get; } = scope.Resolve<IWorldState>();
    public IBlockProcessor BlockProcessor { get; } = scope.Resolve<IBlockProcessor>();

    public void Dispose() => scope.Dispose();
}

public class ArbitrumStatelessBlockProcessingEnvFactory(ILifetimeScope rootLifetimeScope)
{
    public IArbitrumStatelessBlockProcessingEnvScope CreateScope(ArbitrumWitness witness)
    {
        StatelessBlockTree statelessBlockTree = new(witness.Witness.DecodeHeaders());

        ILifetimeScope scope = rootLifetimeScope.BeginLifetimeScope(builder =>
        {
            builder
                .AddScoped<StatelessBlockTree>(_ => statelessBlockTree)
                .BindScoped<IBlockTree, StatelessBlockTree>()
                .BindScoped<IBlockhashCache, StatelessBlockTree>()

                .AddScoped<IWorldState>(ctx => new WorldState(
                    new TrieStoreScopeProvider(
                        new RawTrieStore(witness.Witness.CreateNodeStorage()),
                        witness.Witness.CreateCodeDb(),
                        ctx.Resolve<ILogManager>()),
                    ctx.Resolve<ILogManager>()))

                .AddScoped<IWasmStore, IStylusTargetConfig>(stylusTargetConfig =>
                {
                    WasmDb wasmDb = new(new MemDb());
                    WasmStore store = new(wasmDb, stylusTargetConfig, cacheTag: 1);

                    // For info, pre-activation is not even needed !
                    // If we omit this, wasm store will load wasms from codeDB and lazily compile them to asms when needed during execution.
                    if (witness.UserWasms is not null)
                    {
                        foreach ((ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap) in witness.UserWasms)
                            store.ActivateWasm(in moduleHash, asmMap);
                    }

                    return store;
                })

                .AddScoped<IBlockhashProvider, BlockhashProvider>()

                // Use NoOpL1BlockCache so that L1 hash lookups always go through the witness world state,
                // ensuring the witness must be complete for stateless execution to succeed.
                .AddScoped<IL1BlockCache>(_ => new NoOpL1BlockCache())
                .AddScoped<ArbitrumVirtualMachine>()

                .AddScoped<ICodeInfoRepository, IWorldState, IArbosVersionProvider>((state, versionProvider) =>
                    new ArbitrumCodeInfoRepository(
                        new CodeInfoRepository(state, new EthereumPrecompileProvider()),
                        versionProvider))

                .AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>()

                .AddScoped<IHeaderValidator, IBlockTree, ISealValidator, ISpecProvider, ILogManager>(
                    (blockTree, sealValidator, specProvider, logManager) => new HeaderValidator(blockTree, sealValidator, specProvider, logManager))

                .AddScoped<IUnclesValidator, IBlockTree, IHeaderValidator, ILogManager>(
                    (blockTree, headerValidator, logManager) => new UnclesValidator(blockTree, headerValidator, logManager))

                .AddScoped<IBlockValidator, IHeaderValidator, IUnclesValidator, ISpecProvider, ILogManager>(
                    (headerValidator, unclesValidator, specProvider, logManager) =>
                        new BlockValidator(new TxValidator(specProvider.ChainId), headerValidator, unclesValidator, specProvider, logManager))

                .AddScoped<IRewardCalculator>(_ => NoBlockRewards.Instance)
                .AddScoped<IReceiptStorage>(_ => NullReceiptStorage.Instance)
                .AddScoped<CachedL1PriceData, ILogManager>(logManager => new CachedL1PriceData(logManager))
                .AddScoped<IBlockhashStore, IWorldState>(worldState => new BlockhashStore(worldState))

                .AddScoped<IBeaconBlockRootHandler, ITransactionProcessor, IWorldState>(
                    (txProcessor, worldState) => new BeaconBlockRootHandler(txProcessor, worldState))

                .AddScoped<IWithdrawalProcessor, IWorldState, ILogManager>(
                    (worldState, logManager) => new WithdrawalProcessor(worldState, logManager))

                .AddScoped<IExecutionRequestsProcessor, ITransactionProcessor>(
                    txProcessor => new ExecutionRequestsProcessor(txProcessor))

                .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, ITransactionProcessor, IWorldState>(
                    (txProcessor, worldState) => new BlockProcessor.BlockValidationTransactionsExecutor(
                        new ExecuteTransactionProcessorAdapter(txProcessor),
                        worldState))

                .AddScoped<IBlockProcessor, ArbitrumBlockProcessor>();
        });

        return new ArbitrumStatelessBlockProcessingEnvScope(scope);
    }
}
