// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Concurrent;
using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class StateReconstructor : IStateReconstructor
{
    private readonly ReconstructedStateTrieStore _trieStore;
    private readonly IBlockTree _blockTree;
    private readonly ArbitrumStateReconstructionBlockProcessingEnvFactory _envFactory;
    private readonly IReceiptStorage _receiptStorage;
    private readonly IEthereumEcdsa _ecdsa;
    private readonly ILogger _logger;
    private readonly long _genesisBlockNumber;
    private readonly object _reconstructionLock = new();

    /// <summary>
    /// Maximum number of state roots to keep pinned in the MemDb overlay simultaneously.
    /// When exceeded, the oldest entries are evicted (their nodes dereferenced and potentially deleted).
    /// </summary>
    private readonly int _maxStateRootsInMem;

    /// <summary>FIFO queue of pinned state roots; oldest entries are evicted when the queue exceeds <see cref="_maxStateRootsInMem"/>.</summary>
    private readonly ConcurrentQueue<(Hash256, ulong)> _preparedQueue = new();

    public StateReconstructor(
        ReconstructedStateTrieStore trieStore,
        IBlockTree blockTree,
        ArbitrumStateReconstructionBlockProcessingEnvFactory envFactory,
        IReceiptStorage receiptStorage,
        IEthereumEcdsa ecdsa,
        IArbitrumSpecHelper specHelper,
        IArbitrumConfig arbitrumConfig,
        ILogManager logManager)
    {
        _trieStore = trieStore;
        _blockTree = blockTree;
        _envFactory = envFactory;
        _receiptStorage = receiptStorage;
        _ecdsa = ecdsa;
        _logger = logManager.GetClassLogger();
        _genesisBlockNumber = (long)specHelper.GenesisBlockNum;
        _maxStateRootsInMem = arbitrumConfig.ValidatorMaxStateRootsInMem;
    }

    /// <summary>
    /// Ensures the state for the given parent header is available in the ReconstructedStateTrieStore.
    /// If unavailable, walks backward to find the nearest available state and re-executes blocks forward.
    /// After this call, the state root is pinned in the prepared queue (if MemDb-resident) and
    /// will be kept alive until evicted by later calls.
    /// </summary>
    public void EnsureStateAvailable(BlockHeader targetParent)
    {
        Console.WriteLine($"--- In EnsureStateAvailable with target parent: {targetParent.Number}, state root: {targetParent.StateRoot} ---");
        Hash256 stateRoot = targetParent.StateRoot!;

        lock (_reconstructionLock)
        {
            // Re-check after acquiring the lock: another thread may have reconstructed while we waited.
            if (_trieStore.HasRoot(stateRoot))
            {
                // Pin the state root if it lives in the MemDb overlay
                _trieStore.Reference(stateRoot);

                if (_logger.IsDebug)
                    _logger.Debug($"State already available for block {targetParent.Number} (root {stateRoot})");

                return;
            }
            Console.WriteLine($"--- Ensuring state available for block {targetParent.Number} (root {targetParent.StateRoot})");

            if (_logger.IsInfo)
                _logger.Info($"State not available for block {targetParent.Number} (root {stateRoot}), reconstructing...");

            BlockHeader lastAvailable = FindLastAvailableState(targetParent);
            // Pin the lastAvailable's state root if it lives in the MemDb overlay
            _trieStore.Reference(lastAvailable.StateRoot!);

            if (_logger.IsInfo)
                _logger.Info($"Found available state at block {lastAvailable.Number} (root {lastAvailable.StateRoot}), re-executing {targetParent.Number - lastAvailable.Number} blocks forward");

            ReExecuteBlocks(lastAvailable, targetParent);

            Console.WriteLine($"--- Re-executed blocks from {lastAvailable.Number + 1} to {targetParent.Number}, overlay size: {_trieStore.OverlaySizeBytes / 1_048_576.0:F1} MB");

            if (!_trieStore.HasRoot(stateRoot))
                throw new InvalidOperationException($"State reconstruction failed: root {stateRoot} not available after re-execution");
        }
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

        // Not necessary to write codeDB in read only given writes to it are idempotent
        using ArbitrumStateReconstructionBlockProcessingEnvScope env = _envFactory.CreateScope();
        IBlockProcessor blockProcessor = env.BlockProcessor;
        IWorldState worldState = env.WorldState;
        ISpecProvider specProvider = env.SpecProvider;

        using (worldState.BeginScope(lastAvailable))
        {
            Hash256 expectedParentHash = lastAvailable.Hash!;
            Hash256 prevStateRoot = lastAvailable.StateRoot!;

            for (long blockNumber = startBlock; blockNumber <= endBlock; blockNumber++)
            {
                Block? block = _blockTree.FindBlock(blockNumber, BlockTreeLookupOptions.RequireCanonical);
                if (block is null)
                    throw new InvalidOperationException($"Cannot find block {blockNumber} during state reconstruction");

                if (block.ParentHash != expectedParentHash)
                    throw new InvalidOperationException(
                        $"Parent hash mismatch at block {blockNumber}: expected {expectedParentHash}, got {block.ParentHash}");

                // SenderAddress is not persisted in block RLP — recover from receipts (fast path for
                // Arbitrum internal txs which have no ECDSA signature) or from ECDSA signature.
                RecoverTxSenders(block);

                Hash256 expectedBlockHash = block.Hash!;
                IReleaseSpec spec = specProvider.GetSpec(block.Header);
                Block processedBlock = null!;
                try
                {
                    (processedBlock, _) = blockProcessor.ProcessOne(block, ProcessingOptions.ForceProcessing, NullBlockTracer.Instance, spec);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"--- Exception during processing block {blockNumber} ---");
                    throw new InvalidOperationException($"Failed to process block {blockNumber} during state reconstruction: {ex.Message}", ex);
                }

                if (processedBlock.Hash != expectedBlockHash)
                    throw new InvalidOperationException(
                        $"Block hash mismatch after re-execution of block {blockNumber}: expected {expectedBlockHash}, got {processedBlock.Hash}");

                worldState.CommitTree(block.Number);

                Hash256 currentStateRoot = processedBlock.Header.StateRoot!;

                // Pin the newly reconstructed state
                _trieStore.Reference(currentStateRoot);
                // Dereference the previous block's state (temporary reference only)
                _trieStore.Dereference(prevStateRoot);

                prevStateRoot = currentStateRoot;

                worldState.Reset();

                expectedParentHash = processedBlock.Hash!;

                if (_logger.IsDebug && blockNumber % 100 == 0)
                    _logger.Debug($"State reconstruction progress: {blockNumber - startBlock + 1}/{endBlock - startBlock + 1} blocks");
            }
        }

        if (_logger.IsInfo)
            _logger.Info($"State reconstruction complete: re-executed {endBlock - startBlock + 1} blocks ({startBlock} to {endBlock})");
    }

    public void DereferenceRoot(Hash256 parentStateRoot)
    {
        lock (_reconstructionLock)
            _trieStore.Dereference(parentStateRoot);
    }

    public void PreparedAddTrim(List<(Hash256, ulong)> stateRoots)
    {
        Console.WriteLine("--- In PreparedAddTrim ---");
        lock (_reconstructionLock)
        {
            foreach ((Hash256 stateRoot, ulong blockNumber) in stateRoots)
                _preparedQueue.Enqueue((stateRoot, blockNumber));

            if (_preparedQueue.Count > _maxStateRootsInMem)
            {
                int toEvict = _preparedQueue.Count - _maxStateRootsInMem;
                for (int i = 0; i < toEvict; i++)
                {
                    if (_preparedQueue.TryDequeue(out (Hash256 oldStateRoot, ulong oldBlockNumber) dequeued))
                    {
                        Console.WriteLine($"--- index: {i}, oldStateRoot: {dequeued.oldStateRoot}, oldBlockNumber: {dequeued.oldBlockNumber} ---");
                        _trieStore.Dereference(dequeued.oldStateRoot);
                    }
                }
            }
        }
    }

    private void RecoverTxSenders(Block block)
    {
        TxReceipt[] receipts = _receiptStorage.Get(block);
        if (block.Transactions.Length == receipts.Length)
        {
            for (int i = 0; i < block.Transactions.Length; i++)
                block.Transactions[i].SenderAddress ??= receipts[i].Sender ?? _ecdsa.RecoverAddress(block.Transactions[i]);
        }
        else
        {
            for (int i = 0; i < block.Transactions.Length; i++)
                block.Transactions[i].SenderAddress ??= _ecdsa.RecoverAddress(block.Transactions[i]);
        }
    }

}
