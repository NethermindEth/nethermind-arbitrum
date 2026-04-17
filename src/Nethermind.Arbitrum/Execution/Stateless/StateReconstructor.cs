// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using System.Collections.Concurrent;
using Nethermind.Api;
using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Db;
using Nethermind.Db.FullPruning;
using Nethermind.Blockchain.Receipts;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Logging;
using Nethermind.Trie;
using System.Diagnostics;
using Nethermind.Core.Extensions;
using Nethermind.Arbitrum.Math;

namespace Nethermind.Arbitrum.Execution.Stateless;

public class StateReconstructor : IDisposable
{
    private readonly ReconstructedStateTrieStore _trieStore;
    private readonly INodeStorage _mainNodeStorage;
    private readonly IDb _mainStateDb;
    private readonly IArbitrumConfig _arbitrumConfig;
    private readonly IBlockTree _blockTree;
    private readonly ArbitrumStateReconstructionBlockProcessingEnvFactory _envFactory;
    private readonly IReceiptStorage _receiptStorage;
    private readonly IEthereumEcdsa _ecdsa;
    private readonly IFullPruningDb? _fullPruningDb;
    private readonly string _validMarkerPath;
    private static readonly int MarkerSize = sizeof(long) + Hash256.Size;
    private readonly ILogger _logger;
    private readonly long _genesisBlockNumber;
    private CancellationTokenSource _fullPruningCts;
    private readonly Lock _reconstructionLock = new();

    /// <summary>
    /// Set by <see cref="CopyLastValidStateForFullPruning"/> after validator states are copied to the pruning DB.
    /// Blocks <see cref="WaitForPruningGateAsync"/> callers until <see cref="OnPruningFinished"/> fires,
    /// at which point MemDb is cleared and the gate is released.
    /// Carries the headers that were actually copied so they can be restored after MemDb is cleared.
    /// </summary>
    private sealed record PruningGate(BlockHeader ValidHeader, TaskCompletionSource Tcs);
    private volatile PruningGate? _pruningGate;

    /// <summary>
    /// Maximum number of state roots to keep pinned in the MemDb overlay simultaneously.
    /// When exceeded, the oldest entries are evicted (their nodes dereferenced and potentially deleted).
    /// </summary>
    private readonly int _maxStateRootsInMem;

    /// <summary>
    /// Max reconstructed state memDB size before spilling to disk.
    /// Capping is triggered by calls to <see cref="MaybeCap"/> during reconstruction progress.
    /// </summary>
    private readonly long _maxMemDbSize;

    /// <summary>
    /// Constant to use when capping memDb to keep memDb's size at _maxMemDbSize - BytesToEvictFromMemDb
    /// </summary>
    const long BytesToEvictFromMemDb = 100 * 1024; // 100 KiB

    /// <summary>FIFO queue of pinned headers; oldest entries are evicted when the queue exceeds <see cref="_maxStateRootsInMem"/>.</summary>
    private readonly Queue<BlockHeader> _preparedQueue = new();

    private readonly Lock _validHeaderLock = new();

    /// <summary>
    /// The oldest eligible candidate header whose MemDb nodes are pinned with an extra reference.
    /// Promoted to <see cref="_validHeader"/> by <see cref="TryPromoteValidCandidate"/>.
    /// </summary>
    private BlockHeader? _validHeaderCandidate;

    /// <summary>Last confirmed valid header, used to prevent regression in candidate selection.</summary>
    private BlockHeader? _validHeader;

    public StateReconstructor(
        ReconstructedStateTrieStore trieStore,
        INodeStorage mainNodeStorage,
        IBlockTree blockTree,
        ArbitrumStateReconstructionBlockProcessingEnvFactory envFactory,
        IReceiptStorage receiptStorage,
        IEthereumEcdsa ecdsa,
        IArbitrumSpecHelper specHelper,
        IArbitrumConfig arbitrumConfig,
        IInitConfig initConfig,
        ILogManager logManager,
        IPruningConfig pruningConfig,
        IDbProvider dbProvider)
    {
        _trieStore = trieStore;
        _mainNodeStorage = mainNodeStorage;
        _mainStateDb = dbProvider.StateDb;
        _arbitrumConfig = arbitrumConfig;
        _blockTree = blockTree;
        _envFactory = envFactory;
        _receiptStorage = receiptStorage;
        _ecdsa = ecdsa;
        _validMarkerPath = Path.Combine(initConfig.BaseDbPath.GetApplicationResourcePath(), "validator-last-valid");
        _logger = logManager.GetClassLogger<StateReconstructor>();
        _genesisBlockNumber = (long)specHelper.GenesisBlockNum;
        _maxStateRootsInMem = arbitrumConfig.ValidatorMaxStateRootsInMem;
        _maxMemDbSize = (long)_arbitrumConfig.ValidatorReconstructedStateMemDBMaxSizeMb * 1024 * 1024;

        if (_maxMemDbSize < BytesToEvictFromMemDb && _logger.IsWarn)
            _logger.Warn($"ValidatorReconstructedStateMemDBMaxSizeMb ({_arbitrumConfig.ValidatorReconstructedStateMemDBMaxSizeMb} MB) " +
                $"is below the eviction headroom ({BytesToEvictFromMemDb / 1024} KiB); " +
                "Capping the memDb is in aggressive mode as it will empty it on every trigger instead of giving it some breathing room to grow between caps.");

        Debug.Assert(trieStore.Scheme == INodeStorage.KeyScheme.HalfPath,
            "MemDb overlay uses HalfPath encoding; main state DB must use the same scheme");

        _fullPruningCts = new CancellationTokenSource();

        if (pruningConfig.Mode.IsFull() && dbProvider.StateDb is IFullPruningDb fullPruningDb)
        {
            _fullPruningDb = fullPruningDb;
            fullPruningDb.PruningStarted += OnPruningStarted;
            fullPruningDb.PruningFinished += OnPruningFinished;
        }

        RestoreValidHeader();
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
            // Atomically check and pin state if exists
            if (_trieStore.TryReference(stateRoot))
            {
                if (_logger.IsDebug)
                    _logger.Debug($"State already available for block {targetParent.Number} (root {stateRoot})");

                return;
            }

            if (_logger.IsInfo)
                _logger.Info($"State not available for block {targetParent.Number} (root {stateRoot}), reconstructing...");

            BlockHeader lastAvailable = FindLastAvailableState(targetParent);

            if (_logger.IsInfo)
                _logger.Info($"Found available state at block {lastAvailable.Number} (root {lastAvailable.StateRoot}), re-executing {targetParent.Number - lastAvailable.Number} blocks forward");

            ReExecuteBlocks(lastAvailable, targetParent);

            if (!_trieStore.HasRoot(stateRoot))
                throw new InvalidOperationException($"State reconstruction failed: root {stateRoot} not available after re-execution");
        }
    }

    /// <summary>
    /// Updates the valid candidate header, keeping the oldest eligible one pinned in the MemDb overlay.
    /// Should be called for each header prepared in <see cref="PrepareForRecord"/>.
    /// </summary>
    public void UpdateValidCandidateHeader(BlockHeader header)
    {
        lock (_validHeaderLock)
        {
            // Keep the oldest candidate — it will be validated first by the consensus layer.
            // Don't need a candidate that's newer than the current one.
            if (_validHeaderCandidate is not null && _validHeaderCandidate.Number <= header.Number)
                return;

            // Don't set a candidate older than the already-confirmed valid header.
            if (_validHeader is not null && _validHeader.Number >= header.Number)
                return;

            // Atomically check and pin the new candidate; warn if nodes are unavailable.
            if (!_trieStore.TryReference(header.StateRoot!))
            {
                if (_logger.IsWarn)
                    _logger.Warn($"UpdateValidCandidateHeader: state for block {header.Number} (root {header.StateRoot}) not available");
                return;
            }

            // Release the previous candidate's extra pin.
            if (_validHeaderCandidate is not null)
                _trieStore.Dereference(_validHeaderCandidate.StateRoot!);

            _validHeaderCandidate = header;
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
        lock (_validHeaderLock)
        {
            if (_validHeaderCandidate is null)
                return null;

            // Candidate must not be ahead of the validated position.
            if (_validHeaderCandidate.Number > validBlockNumber)
                return null;

            // Verify the candidate is still canonical. If not, clear it.
            Hash256? canonicalHash = _blockTree
                .FindHeader(_validHeaderCandidate.Number, BlockTreeLookupOptions.RequireCanonical)?.Hash;
            if (canonicalHash != _validHeaderCandidate.Hash)
            {
                if (_logger.IsError)
                    _logger.Error($"MarkValid: candidate at block {_validHeaderCandidate.Number} is no longer canonical " +
                                  $"(candidate={_validHeaderCandidate.Hash}, canonical={canonicalHash}), clearing");
                _trieStore.Dereference(_validHeaderCandidate.StateRoot!);
                _validHeaderCandidate = null;
                return null;
            }

            // Release the old valid header's MemDb pin before replacing it.
            // The candidate's reference is transferred to _validHeader
            if (_validHeader is not null)
                _trieStore.Dereference(_validHeader.StateRoot!);

            _validHeader = _validHeaderCandidate;
            _validHeaderCandidate = null;

            return _validHeader;
        }
    }

    public void DereferenceRoot(Hash256 parentStateRoot)
    {
        lock (_reconstructionLock)
            _trieStore.Dereference(parentStateRoot);
    }

    /// <summary>
    /// Called during full pruning to copy the validator states (<see cref="_validHeader"/>)
    /// to the pruning destination database via <paramref name="copyToNewDb"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Holds <see cref="_validHeaderLock"/> throughout so that <see cref="TryPromoteValidCandidate"/>
    /// cannot dereference <see cref="_validHeader"/> nodes while we are traversing them.
    /// </para>
    /// </remarks>
    public void CopyLastValidStateForFullPruning(long pruningBaseBlock, Action<BlockHeader> copyToNewDb)
    {
        // Lock _validHeaderLock so that validHeader's state can be safely traversed and copied
        // without risk of TryPromoteValidCandidate dereferencing it mid-copy.
        lock (_validHeaderLock)
        {
            BlockHeader? validHeader = _validHeader;

            // validHeader should be null only if FullPruning happens before the first ever! successful MarkValid
            // which should never happen if FullPruning's trigger is of reasonable size (not like a few Mbs).
            if (validHeader is null)
            {
                if (_logger.IsWarn)
                    _logger.Warn("CopyStatesForFullPruning: no confirmed valid header, skipping validator state copy.");
                return;
            }

            if (validHeader.Number >= pruningBaseBlock)
            {
                if (_logger.IsInfo)
                    _logger.Info($"CopyStatesForFullPruning: valid header {validHeader.Number} is not older than pruning base block {pruningBaseBlock}, skipping validator state copy.");
                return;
            }

            if (_logger.IsInfo)
                _logger.Info($"CopyStatesForFullPruning: copying validator states — validHeader={validHeader.Number}");

            copyToNewDb(validHeader);

            // Set the pruning gate. Validator methods will block on this gate until
            // OnPruningFinished fires (i.e. pruning.Commit() + context disposal complete).
            // We save the header that was actually copied so OnPruningFinished can restore it after
            // clearing MemDb — any advancement of _validHeader that occurs between now
            // and commit references MemDb nodes that we are about to discard.
            _pruningGate = new PruningGate(validHeader,
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        }
    }

    /// <summary>
    /// Returns a <see cref="Task"/> that completes once the pruning gate is released by
    /// <see cref="OnPruningFinished"/>. Returns a completed task when no pruning is in progress.
    /// Callers (PrepareForRecord / RecordBlockCreation) must await this before touching any
    /// MemDb state so they don't observe a partially-committed or already-cleared overlay.
    /// </summary>
    public Task WaitForPruningGateAsync() => _pruningGate?.Tcs.Task ?? Task.CompletedTask;

    /// <summary>Synchronous variant of <see cref="WaitForPruningGateAsync"/> for sync callers.</summary>
    public void WaitForPruningGate() => WaitForPruningGateAsync().GetAwaiter().GetResult();

    private void OnPruningStarted(object? sender, PruningEventArgs args)
    {
        _fullPruningCts.Cancel();
    }

    public void Dispose()
    {
        if (_fullPruningDb is not null)
        {
            _fullPruningDb.PruningStarted -= OnPruningStarted;
            _fullPruningDb.PruningFinished -= OnPruningFinished;
        }
        _fullPruningCts.Dispose();
        PersistOnShutdown();
    }

    public void PreparedAddTrim(List<BlockHeader> headers)
    {
        lock (_reconstructionLock)
        {
            foreach (BlockHeader header in headers)
                _preparedQueue.Enqueue(header);

            if (_preparedQueue.Count > _maxStateRootsInMem)
            {
                int toEvict = _preparedQueue.Count - _maxStateRootsInMem;
                for (int i = 0; i < toEvict; i++)
                {
                    if (_preparedQueue.TryDequeue(out BlockHeader? oldStateRoot))
                        _trieStore.Dereference(oldStateRoot.StateRoot!);
                }
            }
        }
    }

    public void ReorgTo(BlockHeader header)
    {
        lock (_validHeaderLock)
        {
            // Invalidate any in-flight pruning gate: OnPruningFinished must not restore
            // a stale (pre-reorg) _validHeader after we clear it below.
            // Completing the TCS unblocks any potential RecordBlockCreation/PrepareForRecord already waiting.
            PruningGate? gate = _pruningGate;
            if (gate is not null)
            {
                _pruningGate = null;
                gate.Tcs.SetResult();
            }

            if (_validHeader is not null && _validHeader.Number > header.Number)
            {
                if (_logger.IsWarn)
                    _logger.Warn($"ReorgTo: reorging to block older than previously-marked valid block: reorg target {header.Number} with hash {header.Hash} and header marked as last valid {_validHeader.Number} with hash {_validHeader.Hash}");
                _trieStore.Dereference(_validHeader.StateRoot!);
                _validHeader = null;
            }

            if (_validHeaderCandidate is not null && _validHeaderCandidate.Number > header.Number)
            {
                if (_logger.IsInfo)
                    _logger.Info($"ReorgTo: reorging to block older than valid candidate block: reorg target {header.Number} with hash {header.Hash} and candidate header {_validHeaderCandidate.Number} with hash {_validHeaderCandidate.Hash}");
                _trieStore.Dereference(_validHeaderCandidate.StateRoot!);
                _validHeaderCandidate = null;
            }
        }

        PreparedTrimBeyond(header);
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
            // Check whether state trie exists, and pin it if it lives in the MemDb overlay
            if (_trieStore.TryReference(current.StateRoot!))
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
                    if (!_trieStore.TryReference(currentStateRoot) && _logger.IsError)
                        _logger.Error($"Failed to reference state root {currentStateRoot} for block {blockNumber} just reconstructed");

                    // Dereference the previous block's state (temporary reference only)
                    _trieStore.Dereference(prevStateRoot);

                    prevStateRoot = currentStateRoot;

                    worldState.Reset();

                    expectedParentHash = processedBlock.Hash!;

                    MaybeCap();

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

    private void OnPruningFinished(object? sender, PruningEventArgs args)
    {
        if (!args.Success)
        {
            // Full pruning failed: Commit() was never called, old DB still active, MemDb still valid.
            // Just release the gate without touching anything.
            if (_logger.IsWarn)
                _logger.Warn("OnPruningFinished: full pruning failed — keeping MemDb overlay intact.");
            ResetFullPruningCts();
            PruningGate? gate = _pruningGate;
            _pruningGate = null;
            gate?.Tcs.SetResult();
            return;
        }

        // Hold both locks for the entire cleanup so that no validator operation
        // can observe a partially-cleared state.
        lock (_validHeaderLock)
            lock (_reconstructionLock)
            {
                // Read the gate without nulling it yet. Callers blocked on gate.Tcs.Task must not be
                // released until all cleanup below is done, and new callers must not bypass the gate
                // until then either. Both happen once we null _pruningGate and call SetResult at the end.
                PruningGate? gate = _pruningGate;
                if (gate is null)
                {
                    if (_logger.IsInfo)
                        _logger.Info("OnPruningFinished: full pruning succeeded but no validator-specific valid state got copied — skipping MemDb cleanup.");
                    ResetFullPruningCts();
                    return;
                }

                if (_logger.IsInfo)
                    _logger.Info($"OnPruningFinished: full pruning succeeded — clearing MemDb overlay and restoring validator headers: validHeader={gate.ValidHeader.Number}");

                // Restore _validHeader to the value confirmed written to the new DB.
                // Any (slight) advancement of this header between gate creation and now referenced MemDb nodes
                // are about to get deleted; rolling back ensures they point to available (on-disk) state.
                _validHeader = gate.ValidHeader;
                _validHeaderCandidate = null;

                // Wipe the entire MemDb overlay — all surviving validator state is now on disk.
                _trieStore.ClearOverlay();
                _preparedQueue.Clear();


                // Reset token after clearing memDB so that any pending capping logic
                // just cancels as memDB size is now 0 (under limit)
                ResetFullPruningCts();
                // Null the gate first: new callers arriving after this point return Task.CompletedTask
                // directly without waiting. Then unblock existing waiters with SetResult.
                _pruningGate = null;
                gate.Tcs.SetResult();
            }
    }

    private void PreparedTrimBeyond(BlockHeader header)
    {
        if (_logger.IsInfo)
            _logger.Info($"PreparedTrimBeyond: trimming prepared headers beyond block {header.Number} with hash {header.Hash} due to reorg");

        List<BlockHeader> invalidHeaders = [];
        lock (_reconstructionLock)
        {
            int count = _preparedQueue.Count;
            for (int i = 0; i < count; i++)
            {
                BlockHeader h = _preparedQueue.Dequeue();
                if (h.Number > header.Number)
                    invalidHeaders.Add(h);
                else
                    _preparedQueue.Enqueue(h); // keep (<= reorg target)
            }
        }

        foreach (BlockHeader invalidHeader in invalidHeaders)
            _trieStore.Dereference(invalidHeader.StateRoot!);
    }

    /// <summary>
    /// Replaces <see cref="_fullPruningCts"/> with a fresh, uncancelled token source and
    /// disposes the previous instance to release its registrations and handles.
    /// </summary>
    private void ResetFullPruningCts()
    {
        _fullPruningCts.Dispose();
        _fullPruningCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Cap memDb containing reconstructed state to stay at a max size defined in configuration
    /// </summary>
    /// <remarks>
    /// Executed under _reconstructionLock
    /// </remarks>
    private void MaybeCap()
    {
        if (_trieStore.DirtySize <= _maxMemDbSize)
            return;

        if (_fullPruningCts.IsCancellationRequested)
        {
            if (_logger.IsInfo)
                _logger.Info($"MemDb overlay (size {_trieStore.DirtySize / 1.MiB:F1}MB) " +
                $"exceeded max size {_maxMemDbSize / 1.MiB:F1}MB but full pruning is in progress, " +
                $"skipping capping to avoid redundant disk writes");
            return;
        }

        long startTime = Stopwatch.GetTimestamp();

        double targetSize = _maxMemDbSize.SaturateSub(BytesToEvictFromMemDb);

        if (_logger.IsInfo)
            _logger.Info($"MemDb overlay (size: {_trieStore.DirtySize / 1.MiB:F3}MB) " +
                $"exceeded {_maxMemDbSize / 1.MiB:F2}MB, capping to {targetSize / 1.MiB:F2}MB " +
                $"by spilling oldest state roots to disk");

        long totalCount = _preparedQueue.Count;
        long count = 0;
        double totalBytesFlushed = _trieStore.DirtySize;

        // Disposing the batch flushes it to disk.
        // MaybeCap is done under the reconstruction lock, so, the small window where
        // nodes got evicted from memDB but not yet on disk should not cause any issue.
        // Other potential validator-related operations (not under _reconstructionLock) are
        // safe to occur concurrently as they would just be no-op or not access evicted nodes.
        using (IWriteBatch rawBatch = _mainStateDb.StartWriteBatch())
        {
            while (_trieStore.DirtySize > targetSize)
            {
                if (_fullPruningCts.IsCancellationRequested)
                {
                    if (_logger.IsInfo)
                        _logger.Info($"Full pruning started while capping was in progress, aborting capping to avoid redundant disk writes");
                    break;
                }

                if (!_preparedQueue.TryDequeue(out BlockHeader? header))
                    break;

                _trieStore.DereferenceAndSpill(header.StateRoot!, rawBatch, _mainStateDb);
                count++;
            }
        }

        long stopTime = Stopwatch.GetTimestamp();
        totalBytesFlushed -= _trieStore.DirtySize;
        double elapsed = Stopwatch.GetElapsedTime(startTime, stopTime).TotalMilliseconds;
        if (_logger.IsInfo)
            _logger.Info($"Finished capping MemDb overlay: flushed {totalBytesFlushed / 1.MiB:F3}MB by spilling {count} of {totalCount} state roots to disk, new size {_trieStore.DirtySize / 1.MiB:F3}MB, elapsed time: {elapsed:F1}ms");
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

    private void RestoreValidHeader()
    {
        if (!File.Exists(_validMarkerPath))
        {
            if (_logger.IsWarn)
                _logger.Warn("StateReconstructor: no valid header marker found on startup, starting without valid header.");
            return;
        }

        byte[] data;
        try
        {
            data = File.ReadAllBytes(_validMarkerPath);
        }
        catch (IOException ex)
        {
            if (_logger.IsError)
                _logger.Error($"StateReconstructor: failed to read valid header marker: {ex.Message}, starting without valid header.");
            return;
        }

        if (data.Length != MarkerSize)
        {
            if (_logger.IsError)
                _logger.Error("StateReconstructor: valid header marker file is corrupt, starting without valid header.");
            return;
        }

        long blockNumber = BinaryPrimitives.ReadInt64BigEndian(data);
        Hash256 storedHash = new Hash256(data.AsSpan(sizeof(long)));

        BlockHeader? header = _blockTree.FindHeader(blockNumber, BlockTreeLookupOptions.RequireCanonical);
        if (header is null)
        {
            if (_logger.IsError)
                _logger.Error($"StateReconstructor: last valid block {blockNumber} not found in block tree on startup, starting without valid header.");
            return;
        }

        if (header.Hash != storedHash)
        {
            if (_logger.IsError)
                _logger.Error($"StateReconstructor: canonical block at {blockNumber} has hash {header.Hash} but marker has {storedHash} — block was reorged, starting without valid header.");
            return;
        }

        if (!_trieStore.HasRoot(header.StateRoot!))
        {
            if (_logger.IsError)
                _logger.Error($"StateReconstructor: state root {header.StateRoot} for last valid block {blockNumber} not found in trie store on startup, starting without valid header.");
            return;
        }

        _validHeader = header;
        if (_logger.IsInfo)
            _logger.Info($"StateReconstructor: restored last valid block {blockNumber} from marker file.");
    }

    private void PersistOnShutdown()
    {
        BlockHeader? validHeader;
        lock (_validHeaderLock)
            validHeader = _validHeader;

        if (validHeader is null)
        {
            if (_logger.IsWarn)
                _logger.Warn("StateReconstructor.Dispose: no confirmed valid header, skipping shutdown persistence.");
            return;
        }

        if (_logger.IsInfo)
            _logger.Info($"StateReconstructor.Dispose: persisting validator state for block {validHeader.Number}.");

        // If state root is present, the whole state should be there!
        if (_mainNodeStorage.Get(null, TreePath.Empty, validHeader.StateRoot!) is not null)
        {
            if (_logger.IsInfo)
                _logger.Info($"StateReconstructor.Dispose: state for block {validHeader.Number} already exists in main node storage, skipping copy.");
        }
        else
        {
            using IWriteBatch rawBatch = _mainStateDb.StartWriteBatch();
            _trieStore.TraverseTrieAndCopyTo(validHeader.StateRoot!, rawBatch);
        }

        PersistValidHeaderMarker(validHeader.Number, validHeader.Hash!);
    }

    private void PersistValidHeaderMarker(long blockNumber, Hash256 hash)
    {
        if (_logger.IsInfo)
            _logger.Info($"StateReconstructor: persisting valid header marker for block {blockNumber} with hash {hash}.");

        byte[] buffer = new byte[MarkerSize];
        BinaryPrimitives.WriteInt64BigEndian(buffer, blockNumber);
        hash.Bytes.CopyTo(buffer.AsSpan(sizeof(long)));
        File.WriteAllBytes(_validMarkerPath, buffer);
    }
}
