using Autofac;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Headers;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Tracing;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Db;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class StateReconstructor
{
    private readonly ReconstructedStateTrieStore _trieStore;
    private readonly IBlockTree _blockTree;
    private readonly ILifetimeScope _rootLifetimeScope;
    private readonly ILogManager _logManager;
    private readonly ILogger _logger;
    private readonly long _genesisBlockNumber;

    public StateReconstructor(
        ReconstructedStateTrieStore trieStore,
        IBlockTree blockTree,
        ILifetimeScope rootLifetimeScope,
        IArbitrumSpecHelper specHelper,
        ILogManager logManager)
    {
        _trieStore = trieStore;
        _blockTree = blockTree;
        _rootLifetimeScope = rootLifetimeScope;
        _logManager = logManager;
        _logger = logManager.GetClassLogger();
        _genesisBlockNumber = (long)specHelper.GenesisBlockNum;
    }

    /// <summary>
    /// Ensures the state for the given parent header is available in the ReconstructedStateTrieStore.
    /// If unavailable, walks backward to find the nearest available state and re-executes blocks forward.
    /// </summary>
    public void EnsureStateAvailable(BlockHeader targetParent)
    {
        if (_trieStore.HasRoot(targetParent.StateRoot!))
        {
            if (_logger.IsDebug)
                _logger.Debug($"State already available for block {targetParent.Number} (root {targetParent.StateRoot})");
            return;
        }

        if (_logger.IsInfo)
            _logger.Info($"State not available for block {targetParent.Number} (root {targetParent.StateRoot}), reconstructing...");

        BlockHeader lastAvailable = FindLastAvailableState(targetParent);

        if (_logger.IsInfo)
            _logger.Info($"Found available state at block {lastAvailable.Number} (root {lastAvailable.StateRoot}), re-executing {targetParent.Number - lastAvailable.Number} blocks forward");

        ReExecuteBlocks(lastAvailable, targetParent);

        if (!_trieStore.HasRoot(targetParent.StateRoot!))
            throw new InvalidOperationException($"State reconstruction failed: root {targetParent.StateRoot} not available after re-execution");
    }

    /// <summary>
    // For recreating state, this method walks backwards from the target header until it finds a header
    // whose state root is available in the RecordingTrieStore or otherwise reaches genesis and throws there.
    /// </summary>
    private BlockHeader FindLastAvailableState(BlockHeader target)
    {
        BlockHeader current = target;

        while (true)
        {
            if (_trieStore.HasRoot(current.StateRoot!))
                return current;

            if (current.Number <= _genesisBlockNumber)
                throw new InvalidOperationException($"Reached genesis (block {_genesisBlockNumber}) without finding available state while looking for block {target.Number}");

            BlockHeader? parent = _blockTree.FindHeader(current.ParentHash!, BlockTreeLookupOptions.RequireCanonical, current.Number - 1);
            if (parent is null)
                throw new InvalidOperationException($"Cannot find header for block {current.Number - 1} during state reconstruction");

            current = parent;
        }
    }

    private void ReExecuteBlocks(BlockHeader lastAvailable, BlockHeader targetParent)
    {
        long startBlock = lastAvailable.Number + 1;
        long endBlock = targetParent.Number;

        IBlocksConfig blocksConfig = _rootLifetimeScope.Resolve<IBlocksConfig>();
        // Not necessary to write codeDB in read only given writes to it are idempotent
        WorldState worldState = new(
            new TrieStoreScopeProvider(_trieStore, _rootLifetimeScope.Resolve<IDbProvider>().CodeDb, _logManager),
            _logManager);

        using ILifetimeScope scope = _rootLifetimeScope.BeginLifetimeScope(builder =>
        {
            builder
                .AddScoped<IWorldState>(_ => worldState)
                .AddScoped<IBlocksConfig>(_ => CreateReconstructionBlocksConfig(blocksConfig))

                .AddScoped<ITransactionProcessor>(ctx => CreateTransactionProcessor(
                    ctx.Resolve<IArbitrumSpecHelper>(),
                    ctx.Resolve<IWasmStore>(),
                    ctx.Resolve<ISpecProvider>(),
                    ctx.Resolve<IArbosVersionProvider>(),
                    worldState,
                    ctx.Resolve<IHeaderFinder>()))

                .AddScoped<IBlockProcessor.IBlockTransactionsExecutor>(ctx => new BlockProcessor.BlockValidationTransactionsExecutor(
                    new BuildUpTransactionProcessorAdapter(ctx.Resolve<ITransactionProcessor>()),
                    worldState))

                .AddScoped<IReceiptStorage>(NullReceiptStorage.Instance)
                .AddScoped(BlockchainProcessor.Options.NoReceipts)
                .AddScoped<IBlockProcessor, ArbitrumBlockProcessor>();
        });

        IBlockProcessor blockProcessor = scope.Resolve<IBlockProcessor>();
        ISpecProvider specProvider = scope.Resolve<ISpecProvider>();

        using (worldState.BeginScope(lastAvailable))
        {
            Hash256 expectedParentHash = lastAvailable.Hash!;

            for (long blockNumber = startBlock; blockNumber <= endBlock; blockNumber++)
            {
                Block? block = _blockTree.FindBlock(blockNumber, BlockTreeLookupOptions.RequireCanonical);
                if (block is null)
                    throw new InvalidOperationException($"Cannot find block {blockNumber} during state reconstruction");

                if (block.ParentHash != expectedParentHash)
                    throw new InvalidOperationException(
                        $"Parent hash mismatch at block {blockNumber}: expected {expectedParentHash}, got {block.ParentHash}");

                Hash256 expectedBlockHash = block.Hash!;
                IReleaseSpec spec = specProvider.GetSpec(block.Header);
                (Block processedBlock, _) = blockProcessor.ProcessOne(block, ProcessingOptions.ForceProcessing, NullBlockTracer.Instance, spec);

                if (processedBlock.Hash != expectedBlockHash)
                    throw new InvalidOperationException(
                        $"Block hash mismatch after re-execution of block {blockNumber}: expected {expectedBlockHash}, got {processedBlock.Hash}");

                worldState.CommitTree(block.Number);
                worldState.Reset();

                expectedParentHash = processedBlock.Hash!;

                if (_logger.IsDebug && blockNumber % 100 == 0)
                    _logger.Debug($"State reconstruction progress: {blockNumber - startBlock + 1}/{endBlock - startBlock + 1} blocks");
            }
        }

        if (_logger.IsInfo)
            _logger.Info($"State reconstruction complete: re-executed {endBlock - startBlock + 1} blocks ({startBlock} to {endBlock})");
    }

    private ITransactionProcessor CreateTransactionProcessor(
        IArbitrumSpecHelper arbitrumSpecHelper,
        IWasmStore wasmStore,
        ISpecProvider specProvider,
        IArbosVersionProvider arbosVersionProvider,
        IWorldState state,
        IHeaderFinder headerFinder)
    {
        BlockhashProvider blockhashProvider = new(new BlockhashCache(headerFinder, _logManager), state, _logManager);
        ArbitrumVirtualMachine vm = new(arbitrumSpecHelper, blockhashProvider, wasmStore, specProvider, _logManager);

        return new ArbitrumTransactionProcessor(
            BlobBaseFeeCalculator.Instance, specProvider, state, wasmStore, vm, _logManager,
            new ArbitrumCodeInfoRepository(new CodeInfoRepository(state, new EthereumPrecompileProvider()), arbosVersionProvider));
    }

    private static BlocksConfig CreateReconstructionBlocksConfig(IBlocksConfig blocksConfig)
        => new()
        {
            TargetBlockGasLimit = blocksConfig.TargetBlockGasLimit,
            MinGasPrice = blocksConfig.MinGasPrice,
            RandomizedBlocks = blocksConfig.RandomizedBlocks,
            ExtraData = blocksConfig.ExtraData,
            SecondsPerSlot = blocksConfig.SecondsPerSlot,
            SingleBlockImprovementOfSlot = blocksConfig.SingleBlockImprovementOfSlot,
            PreWarmStateOnBlockProcessing = false,
            CachePrecompilesOnBlockProcessing = blocksConfig.CachePrecompilesOnBlockProcessing,
            PreWarmStateConcurrency = blocksConfig.PreWarmStateConcurrency,
            BlockProductionTimeoutMs = blocksConfig.BlockProductionTimeoutMs,
            GenesisTimeoutMs = blocksConfig.GenesisTimeoutMs,
            BlockProductionMaxTxKilobytes = blocksConfig.BlockProductionMaxTxKilobytes,
            GasToken = blocksConfig.GasToken,
            BlockProductionBlobLimit = blocksConfig.BlockProductionBlobLimit,
            BuildBlocksOnMainState = false,
        };
}
