// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using System.Collections.Concurrent;
using Nethermind.Api;
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
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class StateReconstructor : IAdditionalRootsProvider
{
    private readonly ReconstructedStateTrieStore _trieStore;
    private readonly IBlockTree _blockTree;
    private readonly ArbitrumStateReconstructionBlockProcessingEnvFactory _envFactory;
    private readonly IReceiptStorage _receiptStorage;
    private readonly IEthereumEcdsa _ecdsa;
    private readonly string _validMarkerPath;
    private static readonly int MarkerSize = sizeof(long) + Hash256.Size;
    private readonly ILogger _logger;
    private readonly long _genesisBlockNumber;
    private readonly object _reconstructionLock = new();

    /// <summary>
    /// Maximum number of state roots to keep pinned in the MemDb overlay simultaneously.
    /// When exceeded, the oldest entries are evicted (their nodes dereferenced and potentially deleted).
    /// </summary>
    private readonly int _maxStateRootsInMem;

    /// <summary>FIFO queue of pinned state roots; oldest entries are evicted when the queue exceeds <see cref="_maxStateRootsInMem"/>.</summary>
    private readonly ConcurrentQueue<Hash256> _preparedQueue = new();

    private readonly object _validHdrLock = new();

    /// <summary>
    /// The oldest eligible candidate header whose MemDb nodes are pinned with an extra reference.
    /// Promoted to <see cref="_validHdr"/> by <see cref="TryPromoteValidCandidate"/>.
    /// </summary>
    private BlockHeader? _validHdrCandidate;

    /// <summary>Last confirmed valid header, used to prevent regression in candidate selection.</summary>
    private BlockHeader? _validHdr;

    public StateReconstructor(
        ReconstructedStateTrieStore trieStore,
        IBlockTree blockTree,
        ArbitrumStateReconstructionBlockProcessingEnvFactory envFactory,
        IReceiptStorage receiptStorage,
        IEthereumEcdsa ecdsa,
        IArbitrumSpecHelper specHelper,
        IArbitrumConfig arbitrumConfig,
        IInitConfig initConfig,
        ILogManager logManager)
    {
        _trieStore = trieStore;
        _blockTree = blockTree;
        _envFactory = envFactory;
        _receiptStorage = receiptStorage;
        _ecdsa = ecdsa;
        _validMarkerPath = Path.Combine(initConfig.BaseDbPath.GetApplicationResourcePath(), "validator-last-valid");
        _logger = logManager.GetClassLogger();
        _genesisBlockNumber = (long)specHelper.GenesisBlockNum;
        _maxStateRootsInMem = arbitrumConfig.ValidatorMaxStateRootsInMem;

        RestoreValidHdr();
    }

    /// <summary>
    /// Ensures the state for the given parent header is available in the ReconstructedStateTrieStore.
    /// If unavailable, walks backward to find the nearest available state and re-executes blocks forward.
    /// After this call, the state root is pinned in the prepared queue (if MemDb-resident) and
    /// will be kept alive until evicted by later calls.
    /// </summary>
    public void EnsureStateAvailable(BlockHeader targetParent)
    {
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

            if (_logger.IsInfo)
                _logger.Info($"State not available for block {targetParent.Number} (root {stateRoot}), reconstructing...");

            BlockHeader lastAvailable = FindLastAvailableState(targetParent);
            // Pin the lastAvailable's state root if it lives in the MemDb overlay
            _trieStore.Reference(lastAvailable.StateRoot!);

            if (_logger.IsInfo)
                _logger.Info($"Found available state at block {lastAvailable.Number} (root {lastAvailable.StateRoot}), re-executing {targetParent.Number - lastAvailable.Number} blocks forward");

            ReExecuteBlocks(lastAvailable, targetParent);

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

            try
            {
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
                    (Block processedBlock, _) = blockProcessor.ProcessOne(block, ProcessingOptions.ForceProcessing, NullBlockTracer.Instance, spec);

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
            catch
            {
                _trieStore.Dereference(prevStateRoot);
                throw; // Preserves stack trace
            }
        }

        if (_logger.IsInfo)
            _logger.Info($"State reconstruction complete: re-executed {endBlock - startBlock + 1} blocks ({startBlock} to {endBlock})");
    }

    /// <summary>
    /// Updates the valid candidate header, keeping the oldest eligible one pinned in the MemDb overlay.
    /// Should be called for each header prepared in <see cref="PrepareForRecord"/>.
    /// </summary>
    public void UpdateValidCandidateHdr(BlockHeader header)
    {
        lock (_validHdrLock)
        {
            // Keep the oldest candidate — it will be validated first by the consensus layer.
            // Don't need a candidate that's newer than the current one.
            if (_validHdrCandidate is not null && _validHdrCandidate.Number <= header.Number)
                return;

            // Don't set a candidate older than the already-confirmed valid header.
            if (_validHdr is not null && _validHdr.Number >= header.Number)
                return;

            // Pin the new candidate in the MemDb overlay; warn if nodes are unavailable.
            if (_trieStore.HasRoot(header.StateRoot!))
            {
                _trieStore.Reference(header.StateRoot!);
            }
            else if (_logger.IsWarn)
            {
                _logger.Warn($"UpdateValidCandidateHdr: state for block {header.Number} (root {header.StateRoot}) not available");
                return;
            }

            // Release the previous candidate's extra pin.
            if (_validHdrCandidate is not null)
                _trieStore.Dereference(_validHdrCandidate.StateRoot!);

            _validHdrCandidate = header;
        }
    }

    /// <summary>
    /// Attempts to promote the current candidate to the confirmed valid header.
    /// Releases the candidate's MemDb pin regardless of the outcome when the candidate is non-canonical.
    /// Returns the promoted header on success, <see langword="null"/> otherwise.
    /// Mirrors Nitro's <c>MarkValid</c> candidate promotion logic.
    /// </summary>
    public BlockHeader? TryPromoteValidCandidate(long validBlockNumber)
    {
        lock (_validHdrLock)
        {
            if (_validHdrCandidate is null)
                return null;

            // Candidate must not be ahead of the validated position.
            if (_validHdrCandidate.Number > validBlockNumber)
                return null;

            // Explicit regression guard: only advance _validHdr forward.
            // Nitro relies on UpdateValidCandidateHdr never setting a candidate older than _validHdr
            // to achieve this implicitly; we keep the check here for safety.
            // Not sure it's a good idea for reorgs -- to make sure
            // if (_validHdr is not null && _validHdr.Number >= validBlockNumber)
            //     return null;

            // Verify the candidate is still canonical. If not, clear it.
            Hash256? canonicalHash = _blockTree
                .FindHeader(_validHdrCandidate.Number, BlockTreeLookupOptions.RequireCanonical)?.Hash;
            if (canonicalHash != _validHdrCandidate.Hash)
            {
                if (_logger.IsError)
                    _logger.Error($"MarkValid: candidate at block {_validHdrCandidate.Number} is no longer canonical " +
                                  $"(candidate={_validHdrCandidate.Hash}, canonical={canonicalHash}), clearing");
                _trieStore.Dereference(_validHdrCandidate.StateRoot!);
                _validHdrCandidate = null;
                return null;
            }

            // Release the old valid header's MemDb pin before replacing it.
            // The candidate's reference is transferred to _validHdr
            if (_validHdr is not null)
                _trieStore.Dereference(_validHdr.StateRoot!);

            _validHdr = _validHdrCandidate;
            _validHdrCandidate = null;

            return _validHdr;
        }
    }

    public void DereferenceRoot(Hash256 parentStateRoot)
    {
        lock (_reconstructionLock)
            _trieStore.Dereference(parentStateRoot);
    }

    /// <inheritdoc/>
    public void CopyAdditionalStatesToNodeStorage(INodeStorage target, long? upToBlockNumberExclusive = null)
    {
        long? minBlock;
        Hash256? minBlockHash;
        lock (_validHdrLock)
        {
            minBlock = _validHdr?.Number;
            minBlockHash = _validHdr?.Hash;
        }

        if (minBlock is null)
        {
            if (_logger.IsWarn)
                _logger.Warn("CopyAdditionalStatesToNodeStorage: no confirmed valid header, skipping. Careful: might lead to state still used being pruned");
            return;
        }

        // When upToBlockNumberExclusive is null (shutdown mode), copy only the validHdr block.
        long endBlock = upToBlockNumberExclusive ?? (minBlock.Value + 1);

        if (_logger.IsInfo)
        {
            if (endBlock <= minBlock.Value)
                // _validHdr is newer than baseBlock: MemDb overlay nodes survive in-memory and unchanged
                // nodes are already covered by the regular CopyTree pass at baseBlock — nothing extra to copy.
                _logger.Info($"Full Pruning: valid block {minBlock.Value} is newer than pruning base {endBlock - 1}, no additional state copying needed.");
            else if (endBlock == minBlock.Value + 1)
                _logger.Info($"Persisting MemDb trie nodes for valid block {minBlock.Value}");
            else
                _logger.Info($"Full Pruning: preserving additional states from block {minBlock.Value} to {endBlock - 1}.");
        }

        for (long blockNum = minBlock.Value; blockNum < endBlock; blockNum++)
        {
            BlockHeader? header = _blockTree.FindHeader(blockNum, BlockTreeLookupOptions.RequireCanonical);
            if (header?.StateRoot is null)
            {
                if (_logger.IsWarn)
                    _logger.Warn($"CopyAdditionalStatesToNodeStorage: header or state root not found for block {blockNum}, skipping.");
                continue;
            }
            _trieStore.TraverseTrieAndCopyTo(header.StateRoot, target);
        }

        // Persist the marker only on shutdown (upToBlockNumberExclusive is null) so that on restart
        // _validHdr can be restored before full pruning may fire and MarkValid has not been called.
        // Written after the node copy so the marker and the persisted nodes are always in sync.
        if (upToBlockNumberExclusive is null)
            PersistValidHdrMarker(minBlock.Value, minBlockHash!);
    }

    public void PreparedAddTrim(List<Hash256> stateRoots)
    {
        lock (_reconstructionLock)
        {
            foreach (Hash256 stateRoot in stateRoots)
                _preparedQueue.Enqueue(stateRoot);

            if (_preparedQueue.Count > _maxStateRootsInMem)
            {
                int toEvict = _preparedQueue.Count - _maxStateRootsInMem;
                for (int i = 0; i < toEvict; i++)
                {
                    if (_preparedQueue.TryDequeue(out Hash256? oldStateRoot))
                        _trieStore.Dereference(oldStateRoot);
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

    private void RestoreValidHdr()
    {
        if (!File.Exists(_validMarkerPath))
        {
            if (_logger.IsWarn)
                _logger.Warn("StateReconstructor: no valid header marker found on startup, starting without valid header.");
            return;
        }

        byte[] data = File.ReadAllBytes(_validMarkerPath);
        if (data.Length != MarkerSize)
        {
            if (_logger.IsWarn)
                _logger.Warn("StateReconstructor: valid header marker file is corrupt, starting without valid header.");
            return;
        }

        long blockNumber = BinaryPrimitives.ReadInt64BigEndian(data);
        Hash256 storedHash = new Hash256(data.AsSpan(sizeof(long)));

        BlockHeader? header = _blockTree.FindHeader(blockNumber, BlockTreeLookupOptions.RequireCanonical);
        if (header is null)
        {
            if (_logger.IsWarn)
                _logger.Warn($"StateReconstructor: last valid block {blockNumber} not found in block tree on startup, starting without valid header.");
            return;
        }

        if (header.Hash != storedHash)
        {
            if (_logger.IsWarn)
                _logger.Warn($"StateReconstructor: canonical block at {blockNumber} has hash {header.Hash} but marker has {storedHash} — block was reorged, starting without valid header.");
            return;
        }

        if (!_trieStore.HasRoot(header.StateRoot!))
        {
            if (_logger.IsWarn)
                _logger.Warn($"StateReconstructor: state root {header.StateRoot} for last valid block {blockNumber} not found in trie store on startup, starting without valid header.");
            return;
        }

        _validHdr = header;
        if (_logger.IsInfo)
            _logger.Info($"StateReconstructor: restored last valid block {blockNumber} from marker file.");
    }

    private void PersistValidHdrMarker(long blockNumber, Hash256 hash)
    {
        if (_logger.IsInfo)
            _logger.Info($"StateReconstructor: persisting valid header marker for block {blockNumber} with hash {hash}.");
        byte[] buffer = new byte[MarkerSize];
        BinaryPrimitives.WriteInt64BigEndian(buffer, blockNumber);
        hash.Bytes.CopyTo(buffer.AsSpan(sizeof(long)));
        File.WriteAllBytes(_validMarkerPath, buffer);
    }
}
