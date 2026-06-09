// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Nethermind.Core;
using Nethermind.Core.Buffers;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Execution.Stateless;

/// <summary>
/// Overlay trie store for state reconstruction. Reconstructed trie nodes are held in a single
/// in-memory dirty-node map and fall back to the base store (main TrieStore dirty cache + disk)
/// for reads (except for HasRoot that falls back directly to disk).
/// BeginScope is a no-op to avoid acquiring the main TrieStore's scope/pruning locks
/// during potentially long-running state reconstruction.
/// </summary>
/// <remarks>
/// <para>
/// This is a port of go-ethereum/Nitro's <c>triedb/hashdb</c> dirty-node cache. Each node lives in
/// <see cref="_dirties"/> together with its reference count (<see cref="Node.ParentsRefCount"/>) and its
/// position in a doubly-linked <em>flush list</em> ordered by insertion (commit) time.
/// The blob itself is stored in the <see cref="Node"/> too, so there is a single map — reads are
/// served through a thin <see cref="OverlayKeyValueStore"/> adapter wrapped by <see cref="NodeStorage"/>.
/// </para>
/// <para>
/// Two independent mechanisms manage memory, exactly as in Nitro:
/// <list type="bullet">
/// <item><see cref="Cap"/> pages the oldest nodes out to disk by walking the flush list, stopping the
/// instant the in-memory budget is met. It evicts by <em>age</em> (ignores reference counts) because a
/// node written to disk is redundant in memory; this bounds both the eviction batch and total write
/// volume — a shared subtree sits once in the flush list and is written at most once.</item>
/// <item><see cref="Dereference"/> frees memory by reference counting: it descends from a state root,
/// decrements <see cref="Node.Parents"/>, and removes nodes whose count reaches zero, cascading into
/// children. Children are decoded on demand from the node RLP (no separate children table) including
/// for cross-tree children (state tree account leaf referencing storage tree root node).</item>
/// </list>
/// </para>
/// <para>
/// Only the reconstruction commit path (<see cref="BeginCommit"/> → <see cref="TrackingCommitter"/>)
/// writes to the overlay; witness generation is read-only against it.
/// </para>
/// </remarks>
public class ReconstructedStateTrieStore : ITrieStore, IReadOnlyTrieStore
{
    private readonly IReadOnlyTrieStore _baseStore;
    private readonly INodeStorage _nodeStorage;
    private readonly ILogger _logger;

    /// <summary>
    /// One entry per dirty (not-yet-persisted) reconstructed trie node, keyed by its half-path
    /// storage key. The single source of truth for the overlay: blob, reference count, and flush-list
    /// links all live on the <see cref="Node"/>. Protected by <see cref="_lock"/>.
    /// </summary>
    private readonly Dictionary<byte[], Node> _dirties = new(Bytes.EqualityComparer);

    /// <summary>Span-keyed view of <see cref="_dirties"/> for allocation-free reads from the adapter.</summary>
    private readonly Dictionary<byte[], Node>.AlternateLookup<ReadOnlySpan<byte>> _dirtiesSpan;

    /// <summary>Head of the flush list (oldest node), or null when the overlay is empty.</summary>
    private byte[]? _oldest;
    /// <summary>Tail of the flush list (newest node), or null when the overlay is empty.</summary>
    private byte[]? _newest;

    /// <summary>Protects <see cref="_dirties"/>, the flush-list cursors, and <see cref="_dirtiesBytes"/>.</summary>
    private readonly Lock _lock = new();

    /// <summary>Tracked "useful data" size of the overlay: the key and blob bytes of every entry
    /// (mirrors Nitro's <c>dirtiesSize</c>). The fixed per-node metadata overhead is added on read via
    /// <see cref="CurrentTotalFootprint"/>, not accumulated here. Mutated under <see cref="_lock"/>.</summary>
    private long _dirtiesBytes;

    private static readonly AccountDecoder _accountDecoder = AccountDecoder.Instance;

    /// <summary>Fixed per-node metadata overhead: the <see cref="Node"/> object (~48B, including the two
    /// flush-list reference fields), the two array headers for the key and blob (~24B each), and the
    /// dictionary slot (~32B). The flush pointers are 8-byte references to other entries' keys, whose
    /// bytes are accounted on those entries, so only the references are counted here. Approximate (tune
    /// if needed); mirrors Nitro's <c>cachedNodeSize</c>.</summary>
    private const int NodeMemoryOverhead = 128;

    public long DirtySize
    {
        get
        {
            lock (_lock)
                return CurrentTotalFootprint();
        }
    }

    public ReconstructedStateTrieStore(IReadOnlyTrieStore baseStore, ILogManager logManager)
    {
        _baseStore = baseStore;
        _dirtiesSpan = _dirties.GetAlternateLookup<ReadOnlySpan<byte>>();
        _nodeStorage = new NodeStorage(new OverlayKeyValueStore(this));
        _logger = logManager.GetClassLogger<ReconstructedStateTrieStore>();
    }

    public void Dispose() { }

    /// <summary>
    /// Atomically clears the entire overlay: removes all nodes and resets the flush list.
    /// Called by <see cref="StateReconstructor"/> after full pruning commits so that all surviving
    /// validator state is on disk in the new DB.
    /// </summary>
    public void ClearOverlay()
    {
        lock (_lock)
        {
            _dirties.Clear();
            _oldest = null;
            _newest = null;
            _dirtiesBytes = 0;
        }
    }

    public TrieNode FindCachedOrUnknown(Hash256? address, in TreePath path, Hash256 hash)
        => _baseStore.FindCachedOrUnknown(address, in path, hash);

    public byte[]? LoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
    {
        byte[]? rlp = TryLoadRlp(address, in path, hash, flags);
        if (rlp is null)
            throw new MissingTrieNodeException("Missing RLP node", address, path, hash);
        return rlp;
    }

    public byte[]? TryLoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
        => _nodeStorage.Get(address, in path, hash, flags) ?? _baseStore.TryLoadRlp(address, in path, hash, flags | ReadFlags.SkipDuplicateRead);

    /// <summary>
    /// Checks the local overlay first, then falls back to reading from the base store's persistent
    /// node storage (disk). We intentionally avoid <see cref="IReadOnlyTrieStore.HasRoot"/> because
    /// it checks the dirty node cache first, which is volatile: a TOCTOU race can occur where HasRoot
    /// returns true (state in dirty cache) but pruning evicts those nodes before reconstruction reads
    /// them. <see cref="IReadOnlyTrieStore.TryLoadRlp"/> bypasses the dirty cache and reads directly
    /// from the underlying persistent storage, so it is stable.
    /// </summary>
    public bool HasRoot(Hash256 stateRoot)
        => _nodeStorage.Get(null, TreePath.Empty, stateRoot) is not null
        || _baseStore.TryLoadRlp(null, TreePath.Empty, stateRoot, ReadFlags.SkipDuplicateRead) is not null;

    public IDisposable BeginScope(BlockHeader? baseBlock) => new Reactive.AnonymousDisposable(() => { });

    public IScopedTrieStore GetTrieStore(Hash256? address) => new ScopedTrieStore(this, address);

    public INodeStorage.KeyScheme Scheme => _baseStore.Scheme;

    public IBlockCommitter BeginBlockCommit(long blockNumber) => NullCommitter.Instance;

    public ICommitter BeginCommit(Hash256? address, TrieNode? root, WriteFlags writeFlags)
        => new TrackingCommitter(this, address);

    /// <summary>
    /// Atomically checks whether the state root is available and, if overlay-resident, increments
    /// its reference count (O(1)). No increment if the root is not in the overlay (disk-resident
    /// roots need no tracking). Call when wanting to pin a state root in the alive set.
    /// Returns <see langword="true"/> if the root is available (pinned if overlay-resident, no-op if
    /// disk-resident), <see langword="false"/> if not found in either the overlay or the base store.
    /// </summary>
    public bool TryReference(Hash256 stateRoot)
    {
        byte[] key = NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, stateRoot);
        lock (_lock)
        {
            if (_dirties.TryGetValue(key, out Node? node))
            {
                node.ParentsRefCount++;
                return true;
            }
        }
        // Not in overlay — check disk (disk-resident roots need no reference tracking).
        return _baseStore.TryLoadRlp(null, TreePath.Empty, stateRoot, ReadFlags.SkipDuplicateRead) is not null;
    }

    /// <summary>
    /// Decrements the reference count for the given state root and cascades through children, evicting
    /// nodes from the overlay whose count reaches zero. Call when wanting to unpin a state tree from the alive set.
    /// </summary>
    /// <remarks>
    /// Descends from the root accumulating the trie path, so child keys are recomputed from the parent
    /// RLP — including the cross-trie edge from an account leaf to its storage-trie root, handled by
    /// <see cref="PushChildren{TSink}"/>.
    /// </remarks>
    public void Dereference(Hash256 stateRoot)
    {
        lock (_lock)
        {
            Stack<(Hash256? address, TreePath path, Hash256 hash)> stack = new();
            stack.Push((null, TreePath.Empty, stateRoot));

            while (stack.TryPop(out (Hash256? address, TreePath path, Hash256 hash) item))
            {
                byte[] key = NodeStorage.GetHalfPathNodeStoragePath(item.address, in item.path, item.hash);
                if (!_dirties.TryGetValue(key, out Node? node))
                    // On disk (or already evicted by Cap) — nothing to free, and its subtree is gone too.
                    continue;

                if (node.ParentsRefCount > 1)
                {
                    // Still referenced by another root; stop descending — the subtree stays alive.
                    node.ParentsRefCount--;
                    continue;
                }

                // Last reference: free this node and dereference its children.
                SpliceFromFlushList(node);
                _dirties.Remove(key);
                _dirtiesBytes -= NodeDataSize(key, node);

                TupleStackSink sink = new(stack);
                PushChildren(node.Rlp, item.address, item.path, ref sink);
            }
        }
    }

    /// <summary>
    /// Pages the oldest overlay nodes out to the main state DB, walking the flush list head-first and
    /// stopping the moment the overlay's in-memory size drops to <paramref name="targetSize"/>. Eviction
    /// is by node age, independent of reference counts: a node written to disk is redundant in memory,
    /// and a later <see cref="Dereference"/> that reaches an already-evicted node simply stops (it is on
    /// disk, and by commit order its subtree is too).
    /// </summary>
    /// <remarks>
    /// <para>Two passes, mirroring go-ethereum/Nitro's <c>hashdb.Database.Cap</c>: pass one writes the
    /// oldest nodes into a batch and flushes it to disk; pass two removes those nodes from the overlay
    /// only <em>after</em> the disk write is durable, so an external observer never sees a node missing
    /// from both memory and disk. Pass one tracks a local <c>size</c> (not <see cref="_memDbBytes"/>,
    /// which is only decremented in pass two) to decide when the budget is met.</para>
    /// <para>Because it stops as soon as the budget is met, a single pass typically evicts only the small
    /// overshoot, so the batch stays small and total write volume is bounded — each node is in the flush
    /// list once and written at most once.</para>
    /// </remarks>
    public void Cap(double targetSize, IKeyValueStoreWithBatching mainStateDb)
    {
        CapStats stats = default;

        long beforeLock = Stopwatch.GetTimestamp();
        lock (_lock)
        {
            // Pass 1: write oldest-first into the batch until the projected footprint meets the target.
            // Use a local size since _memDbBytes is only decremented in pass 2; the per-node decrement
            // includes the metadata overhead so it matches CurrentFootprint()'s accounting.
            IWriteBatch batch = mainStateDb.StartWriteBatch();

            stats.DirtiesCountBefore = _dirties.Count;
            stats.DirtiesSizeBefore = _dirtiesBytes;
            stats.TotalMemSizeBefore = CurrentTotalFootprint();

            long size = stats.TotalMemSizeBefore;
            byte[]? cursor = _oldest;

            while (size > targetSize && cursor is not null)
            {
                Node node = _dirties[cursor];
                batch.PutSpan(cursor, node.Rlp);
                size -= NodeDataSize(cursor, node) + NodeMemoryOverhead;
                cursor = node.FlushNext;
            }

            // Flush to disk BEFORE removing from memory.
            long beforeDispose = Stopwatch.GetTimestamp();
            batch.Dispose();
            stats.FlushTimeMs = Stopwatch.GetElapsedTime(beforeDispose, Stopwatch.GetTimestamp()).TotalMilliseconds;

            // Pass 2: writes are now durable on disk, clear out the flushed data from memory.
            while (_oldest is not null && !ReferenceEquals(_oldest, cursor))
            {
                byte[] key = _oldest;
                Node node = _dirties[key];
                _dirties.Remove(key);
                _dirtiesBytes -= NodeDataSize(key, node);
                _oldest = node.FlushNext;
            }

            if (_oldest is null)
                _newest = null;
            else
                _dirties[_oldest].FlushPrev = null;

            stats.DirtiesCountAfter = _dirties.Count;
            stats.DirtiesSizeAfter = _dirtiesBytes;
            stats.TotalMemSizeAfter = CurrentTotalFootprint();
            stats.TotalTimeMs = Stopwatch.GetElapsedTime(beforeLock, Stopwatch.GetTimestamp()).TotalMilliseconds;
        }

        if (_logger.IsInfo)
            _logger.Info($"{stats}");
    }

    /// <summary>
    /// Traverses all trie nodes reachable from <paramref name="stateRoot"/> that live in the overlay
    /// and writes them to <paramref name="rawBatch"/>. Nodes absent from the overlay are already on disk
    /// (and by the Merkle hash invariant their entire subtrees are too), so they are skipped entirely.
    /// The caller is responsible for disposing (flushing) the batch. Used for shutdown persistence only.
    /// </summary>
    public void TraverseTrieAndCopyTo(Hash256 stateRoot, IWriteBatch rawBatch)
    {
        lock (_lock)
        {
            Stack<(Hash256? address, TreePath path, Hash256 hash)> stack = new();
            stack.Push((null, TreePath.Empty, stateRoot));

            while (stack.TryPop(out (Hash256? address, TreePath path, Hash256 hash) item))
            {
                byte[] key = NodeStorage.GetHalfPathNodeStoragePath(item.address, in item.path, item.hash);
                if (!_dirties.TryGetValue(key, out Node? node))
                    // Not in the overlay — already on disk. By the Merkle hash invariant its entire
                    // subtree is also on disk, so we can skip both the write and child traversal.
                    continue;

                TupleStackSink stackSink = new(stack);
                PushChildren(node.Rlp, item.address, item.path, ref stackSink);
                rawBatch.PutSpan(key, node.Rlp);
            }
        }
    }

    /// <summary>Tracked "useful data" size of one entry — its key and blob bytes (Nitro's <c>dirtiesSize</c>
    /// term). Accumulated in <see cref="_memDbBytes"/> on insert/remove.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long NodeDataSize(byte[] key, Node node) => key.Length + node.Rlp.Length;

    /// <summary>Approximate real in-memory footprint of the overlay: tracked key+blob bytes plus the fixed
    /// per-node metadata overhead (one <see cref="NodeMemoryOverhead"/> per entry), mirroring Nitro adding
    /// <c>len(dirties)*cachedNodeSize</c> on top of <c>dirtiesSize</c>. Must be called under <see cref="_lock"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private long CurrentTotalFootprint() => _dirtiesBytes + (long)_dirties.Count * NodeMemoryOverhead;

    /// <summary>
    /// Called by <see cref="TrackingCommitter"/> for each node committed to the overlay. Inserts the
    /// node (blob, zero reference count, appended to the flush-list tail) and increments the reference
    /// count of each overlay-resident child. Since Patricia tries commit bottom-up (children before
    /// parents — and a parent's commit awaits its children even under concurrent commit), children are
    /// already present when their parent is processed here, as are cross-trie storage roots (storage
    /// trees commit before the state tree). Idempotent: this is the sole insertion path, so presence in
    /// <see cref="_dirties"/> means the node — and its child links — were established by a prior commit
    /// (same hash ⇒ same children), so we skip to avoid double-counting.
    /// </summary>
    private void InsertCommittedNode(byte[] nodeKey, CappedArray<byte> fullRlp, Hash256? address, TreePath path)
    {
        lock (_lock)
        {
            // Idempotent guard: a commit only writes its dirty nodes, so these are almost always new
            // and this should rarely hit. It guards the genuine re-commit cases — a reorg replaying an
            // already-reconstructed block, an overlapping prepare range, or a value reverting to a prior
            // hash still in the overlay — where the same (address, path, hash) ⇒ same content ⇒ same
            // children, so skipping avoids double-counting.
            if (_dirties.ContainsKey(nodeKey))
                return;

            AddNodeLockHeld(nodeKey, fullRlp.AsSpan().ToArray());

            // Link children under the same lock so the whole insert is atomic: there is never a window
            // where the parent is in the overlay but its children's reference counts have not yet been
            // incremented — a window a concurrent dereference could otherwise observe and under-count.
            // Btw commits are single-threaded, so holding the lock across should be negligible.
            List<byte[]> childKeys = [];
            KeyListSink keySink = new(childKeys);
            PushChildren(fullRlp, address, path, ref keySink);
            foreach (byte[] childKey in childKeys)
            {
                if (_dirties.TryGetValue(childKey, out Node? child))
                    child.ParentsRefCount++;
                // else: child is disk-resident (not in the overlay) — no tracking needed.
            }
        }
    }

    /// <summary>Creates a node for <paramref name="rlp"/>, appends it to the tail of the flush list, adds it
    /// to <see cref="_dirties"/>, and accounts its data size. Must be called under <see cref="_lock"/> with
    /// <paramref name="key"/> not already present.</summary>
    private void AddNodeLockHeld(byte[] key, byte[] rlp)
    {
        Node node = new() { Rlp = rlp, ParentsRefCount = 0, FlushPrev = _newest, FlushNext = null };
        if (_newest is null)
            _oldest = key;
        else
            _dirties[_newest].FlushNext = key;
        _newest = key;
        _dirties[key] = node;
        _dirtiesBytes += NodeDataSize(key, node);
    }

    /// <summary>Removes a node from the flush list, fixing up neighbour links and the head/tail cursors.</summary>
    private void SpliceFromFlushList(Node node)
    {
        if (node.FlushPrev is null)
            _oldest = node.FlushNext;
        else
            _dirties[node.FlushPrev].FlushNext = node.FlushNext;

        if (node.FlushNext is null)
            _newest = node.FlushPrev;
        else
            _dirties[node.FlushNext].FlushPrev = node.FlushPrev;
    }

    private byte[]? GetNodeRlp(scoped ReadOnlySpan<byte> key)
    {
        lock (_lock)
            return _dirtiesSpan.TryGetValue(key, out Node? node) ? node.Rlp : null;
    }

    private static void PushChildren<TSink>(
        CappedArray<byte> rlp,
        Hash256? address,
        TreePath path,
        ref TSink sink) where TSink : struct, IChildSink
    {
        ValueRlpStream stream = new ValueRlpStream(rlp);
        stream.ReadSequenceLength();
        int items = stream.PeekNumberOfItemsRemaining(null, 3);

        if (items > 2)
        {
            // Branch node: up to 16 hash-referenced children
            for (int i = 0; i < 16; i++)
            {
                (int _, int contentLength) = stream.PeekPrefixAndContentLength();
                if (contentLength == 32)
                    sink.Add(address, path.Append(i), stream.DecodeKeccak()!);
                else
                    stream.SkipItem();
            }
            // Branch value slot (index 16) is not a trie node; skip it.
        }
        else if (items == 2)
        {
            ReadOnlySpan<byte> encodedPath = stream.DecodeByteArraySpan();
            (byte[] pathNibbles, bool isLeaf) = HexPrefix.FromBytes(encodedPath);

            if (isLeaf)
            {
                // State trie account leaf: decode account to follow the storage trie if non-empty.
                if (address is null)
                {
                    ReadOnlySpan<byte> accountRlp = stream.DecodeByteArraySpan();
                    Hash256? storageRoot = DecodeAccountStorageRoot(accountRlp);
                    if (storageRoot is not null)
                    {
                        // The full 64-nibble path (root → this leaf) equals Keccak(accountAddress),
                        // which is the address key used by NodeStorage for storage trie nodes.
                        TreePath fullPath = path.Append(pathNibbles);
                        Hash256 addressHash = new Hash256(in fullPath.Path);
                        sink.Add(addressHash, TreePath.Empty, storageRoot);
                    }
                }
                // Storage trie leaf: value is a storage slot — no child nodes.
            }
            else
            {
                // Extension node: single hash-referenced child, path extended by pathNibbles.
                (int _, int contentLength) = stream.PeekPrefixAndContentLength();
                if (contentLength == 32)
                    sink.Add(address, path.Append(pathNibbles), stream.DecodeKeccak()!);
                // Inline child (< 32 bytes) is embedded in the parent — not a separate overlay entry.
            }
        }
    }

    private static Hash256? DecodeAccountStorageRoot(ReadOnlySpan<byte> accountRlp)
    {
        Rlp.ValueDecoderContext ctx = new Rlp.ValueDecoderContext(accountRlp);
        Hash256 storageRoot = _accountDecoder.DecodeStorageRootOnly(ref ctx);
        return storageRoot == Keccak.EmptyTreeHash ? null : storageRoot;
    }

    /// <summary>One reconstructed dirty trie node: its blob, reference count, and flush-list links.</summary>
    private sealed class Node
    {
        /// <summary>The encoded node blob.</summary>
        public required byte[] Rlp;
        /// <summary>Number of live overlay parents referencing this node, plus one per external
        /// <see cref="TryReference"/> on a state root.</summary>
        public int ParentsRefCount;
        /// <summary>Previous (older) node in the flush list; null at the head.</summary>
        public byte[]? FlushPrev;
        /// <summary>Next (newer) node in the flush list; null at the tail.</summary>
        public byte[]? FlushNext;
    }

    /// <summary>
    /// Read adapter exposing the overlay's node blobs to <see cref="NodeStorage"/>. Writes are not
    /// supported: the overlay is populated exclusively through <see cref="BeginCommit"/>.
    /// </summary>
    private sealed class OverlayKeyValueStore(ReconstructedStateTrieStore store) : IKeyValueStore
    {
        public byte[]? Get(scoped ReadOnlySpan<byte> key, ReadFlags flags = ReadFlags.None) => store.GetNodeRlp(key);

        public void Set(ReadOnlySpan<byte> key, byte[]? value, WriteFlags flags = WriteFlags.None)
            => throw new NotSupportedException(
                "The reconstruction overlay is written only via BeginCommit; direct NodeStorage writes should not happen.");
    }

    private sealed class TrackingCommitter(ReconstructedStateTrieStore store, Hash256? address) : ICommitter
    {
        public void Dispose() { }

        public TrieNode CommitNode(ref TreePath path, TrieNode node)
        {
            if (!node.IsBoundaryProofNode)
            {
                if (node.Keccak is null)
                    ThrowUnknownHash(node);

                node.IsPersisted = true;
                byte[] nodeKey = NodeStorage.GetHalfPathNodeStoragePath(address, path, node.Keccak);
                store.InsertCommittedNode(nodeKey, node.FullRlp, address, path);
            }
            return node;
        }

        [DoesNotReturn, StackTraceHidden]
        private static void ThrowUnknownHash(TrieNode node) =>
            throw new TrieStoreException($"The hash of {node} should be known at the time of committing.");
    }

    private interface IChildSink
    {
        void Add(Hash256? address, TreePath path, Hash256 hash);
    }

    private struct KeyListSink(List<byte[]> keys) : IChildSink
    {
        public void Add(Hash256? address, TreePath path, Hash256 hash)
            => keys.Add(NodeStorage.GetHalfPathNodeStoragePath(address, path, hash));
    }

    private struct TupleStackSink(Stack<(Hash256? address, TreePath path, Hash256 hash)> stack) : IChildSink
    {
        public void Add(Hash256? address, TreePath path, Hash256 hash)
            => stack.Push((address, path, hash));
    }

    /// <summary>Accumulated diagnostics for one <see cref="Cap"/> pass.</summary>
    private struct CapStats
    {
        /// <summary>Dirties count before capping</summary>
        public long DirtiesCountBefore;
        /// <summary>Dirties count after capping</summary>
        public long DirtiesCountAfter;
        /// <summary>Dirties size before capping (excl metadata)</summary>
        public long DirtiesSizeBefore;
        /// <summary>Dirties size after capping (excl metadata)</summary>
        public long DirtiesSizeAfter;
        /// <summary>Dirties size before capping (incl metadata)</summary>
        public long TotalMemSizeBefore;
        /// <summary>Dirties size after capping (incl metadata)</summary>
        public long TotalMemSizeAfter;
        /// <summary>Time spent in <see cref="IWriteBatch.Dispose"/> (the disk flush)</summary>
        public double FlushTimeMs;
        /// <summary>Total time spent for capping</summary>
        public double TotalTimeMs;

        public override readonly string ToString() =>
            $"Capped reconstructed-state overlay to {(double)TotalMemSizeAfter / 1.MiB:F3}MB, " +
            $"evicted/flushed {DirtiesCountBefore - DirtiesCountAfter} nodes, " +
            $"flushed size {(double)(DirtiesSizeBefore - DirtiesSizeAfter) / 1.MiB:F3}MB, " +
            $"size freed from memory (incl metadata) {(double)(TotalMemSizeBefore - TotalMemSizeAfter) / 1.MiB:F3}MB, " +
            $"live in-mem {DirtiesCountAfter} nodes, " +
            $"flush time {FlushTimeMs:F1}ms, total capping time {TotalTimeMs:F1}ms";
    }
}
