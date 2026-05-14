// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Execution.Stateless;

public sealed class ArbitrumStateReconstructionBlockProcessingEnvScope(ILifetimeScope scope) : IDisposable
{
    public IWorldState WorldState { get; } = scope.Resolve<IWorldState>();
    public IBlockProcessor BlockProcessor { get; } = scope.Resolve<IBlockProcessor>();
    public ISpecProvider SpecProvider { get; } = scope.Resolve<ISpecProvider>();

    public void Dispose() => scope.Dispose();
}

public class ArbitrumStateReconstructionBlockProcessingEnvFactory(
    ILifetimeScope rootLifetimeScope,
    ReconstructedStateTrieStore trieStore,
    IDbProvider dbProvider,
    ILogManager logManager)
{
    /// <summary>
    /// To force processing in BlockchainProcessor even though block is not better than head (already existing block).
    /// Creates a blocks config suitable for replaying blocks in an isolated scope (state reconstruction, witness generation).
    /// </summary>
    /// <remarks>
    /// Pre-warming is disabled: the prewarming infrastructure is registered in the block producer scope, not in
    /// replay child scopes, so the flag would have no effect anyway.
    /// Building on main state is disabled because processing must not write to the canonical trie.
    /// </remarks>
    internal static BlocksConfig CreateReplayBlocksConfig(IBlocksConfig source) => new()
    {
        TargetBlockGasLimit = source.TargetBlockGasLimit,
        MinGasPrice = source.MinGasPrice,
        RandomizedBlocks = source.RandomizedBlocks,
        ExtraData = source.ExtraData,
        SecondsPerSlot = source.SecondsPerSlot,
        SingleBlockImprovementOfSlot = source.SingleBlockImprovementOfSlot,
        PreWarmStateOnBlockProcessing = false,
        CachePrecompilesOnBlockProcessing = source.CachePrecompilesOnBlockProcessing,
        PreWarmStateConcurrency = source.PreWarmStateConcurrency,
        BlockProductionTimeoutMs = source.BlockProductionTimeoutMs,
        GenesisTimeoutMs = source.GenesisTimeoutMs,
        BlockProductionMaxTxKilobytes = source.BlockProductionMaxTxKilobytes,
        GasToken = source.GasToken,
        BlockProductionBlobLimit = source.BlockProductionBlobLimit,
        BuildBlocksOnMainState = false,
    };

    public ArbitrumStateReconstructionBlockProcessingEnvScope CreateScope()
    {
        // Not necessary to wrap codeDB in read only given writes to it are idempotent
        WorldState worldState = new(
            new TrieStoreScopeProvider(trieStore, dbProvider.CodeDb, logManager),
            logManager);

        IBlocksConfig blocksConfig = rootLifetimeScope.Resolve<IBlocksConfig>();

        ILifetimeScope scope = rootLifetimeScope.BeginLifetimeScope(builder =>
        {
            builder
                .AddScoped<IWorldState>(_ => worldState)
                .AddScoped<IBlocksConfig>(_ => CreateReplayBlocksConfig(blocksConfig))

                .AddScoped<ICodeInfoRepository, IWorldState, IArbosVersionProvider>((state, versionProvider) =>
                    new ArbitrumCodeInfoRepository(
                        new CodeInfoRepository(state, new EthereumPrecompileProvider()),
                        versionProvider))

                // IBlockhashProvider and ArbitrumVirtualMachine are already registered as scoped in the root scope,
                // so a fresh instance for this child scope will be resolved with the correct IWorldState above.
                .AddScoped<ITransactionProcessor, ArbitrumTransactionProcessor>()

                .AddScoped<ITransactionProcessorAdapter, BuildUpTransactionProcessorAdapter>()
                .AddScoped<IBlockProcessor.IBlockTransactionsExecutor, BlockProcessor.BlockValidationTransactionsExecutor>()

                .AddScoped<IReceiptStorage>(_ => NullReceiptStorage.Instance)
                .AddScoped(BlockchainProcessor.Options.NoReceipts)
                .AddScoped<IBlockProcessor, ArbitrumBlockProcessor>();
        });

        return new ArbitrumStateReconstructionBlockProcessingEnvScope(scope);
    }
}
