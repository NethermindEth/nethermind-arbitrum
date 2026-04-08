// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Buffers;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;
using Nethermind.Serialization.Rlp;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Execution.Stateless;

/// <summary>
/// Overlay trie store for state reconstruction. Reconstructed trie nodes are stored in a MemDb overlay
/// and fall back to the base store (main TrieStore dirty cache + disk) for reads.
/// BeginScope is a no-op to avoid acquiring the main TrieStore's scope/pruning locks during
/// potentially long-running state reconstruction.
/// </summary>
/// <remarks>
/// Only PrepareForRecord should write to the overlay to reconstruct the needed state,
/// witness generation is read only against the overlay.
/// </remarks>
public class ReconstructedStateTrieStore(MemDb memDb, IReadOnlyTrieStore baseStore) : ITrieStore, IReadOnlyTrieStore
{
    private readonly INodeStorage _nodeStorage = new NodeStorage(memDb);
    private readonly MemDb _memDb = memDb;

    /// <summary>How many references each MemDb node has: one per trie-parent that was committed referencing it, plus one per external <see cref="Reference"/> call on it (state roots only).</summary>
    private readonly Dictionary<byte[], int> _parents = new(Bytes.EqualityComparer);

    /// <summary>MemDb keys of each committed node's children that are also in MemDb. Used for cascade dereference without traversal.</summary>
    private readonly Dictionary<byte[], List<byte[]>> _children = new(Bytes.EqualityComparer);

    /// <summary>Protects <see cref="_parents"/>, <see cref="_children"/>, and MemDb deletions in <see cref="Reference"/>, <see cref="Dereference"/>, and <see cref="TrackCommittedNode"/>.</summary>
    private readonly Lock _refCountLock = new();

    private static readonly AccountDecoder _accountDecoder = AccountDecoder.Instance;

    public void Dispose()
    {
    }

    /// <summary>
    /// Atomically clears the entire MemDb overlay: removes all nodes and
    /// resets all reference counts. Called by <see cref="StateReconstructor"/> after
    /// full pruning commits so that all surviving validator state is on disk in the new DB.
    /// </summary>
    public void ClearOverlay()
    {
        lock (_refCountLock)
        {
            _parents.Clear();
            _children.Clear();
            _memDb.Clear();
        }
    }

    public TrieNode FindCachedOrUnknown(Hash256? address, in TreePath path, Hash256 hash)
        => baseStore.FindCachedOrUnknown(address, in path, hash);

    public byte[]? LoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
    {
        byte[]? rlp = TryLoadRlp(address, in path, hash, flags);
        if (rlp is null)
            throw new MissingTrieNodeException("Missing RLP node", address, path, hash);
        return rlp;
    }

    public byte[]? TryLoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
        => _nodeStorage.Get(address, in path, hash, flags) ?? baseStore.TryLoadRlp(address, in path, hash, flags);

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
        || baseStore.TryLoadRlp(null, TreePath.Empty, stateRoot) is not null;

    public IDisposable BeginScope(BlockHeader? baseBlock) => new Reactive.AnonymousDisposable(() => { });

    public IScopedTrieStore GetTrieStore(Hash256? address) => new ScopedTrieStore(this, address);

    public INodeStorage.KeyScheme Scheme => baseStore.Scheme;

    public IBlockCommitter BeginBlockCommit(long blockNumber) => NullCommitter.Instance;

    public ICommitter BeginCommit(Hash256? address, TrieNode? root, WriteFlags writeFlags)
        => new TrackingCommitter(this, new RawScopedTrieStore.Committer(_nodeStorage, address, writeFlags), address);

    /// <summary>
    /// Increments the reference count for the given state root node in the MemDb overlay. O(1).
    /// No-op if the root is not in the overlay (disk-resident roots need no tracking).
    /// Call when adding a state root to the alive set.
    /// </summary>
    public void Reference(Hash256 stateRoot)
    {
        byte[] key = NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, stateRoot);
        lock (_refCountLock)
        {
            if (_parents.TryGetValue(key, out int count))
                _parents[key] = count + 1;
        }
    }

    /// <summary>
    /// Decrements the reference count for the given state root and cascades through children,
    /// evicting nodes from the MemDb whose count reaches zero.
    /// Call when removing a state root from the alive set.
    /// </summary>
    public void Dereference(Hash256 stateRoot)
    {
        byte[] rootKey = NodeStorage.GetHalfPathNodeStoragePath(null, TreePath.Empty, stateRoot);
        lock (_refCountLock)
        {
            Stack<byte[]> stack = new();
            stack.Push(rootKey);

            while (stack.TryPop(out byte[]? key))
            {
                if (!_parents.TryGetValue(key, out int count))
                    continue;

                if (count > 1)
                {
                    _parents[key] = count - 1;
                    continue;
                }

                _parents.Remove(key, out _);
                _memDb.Remove(key);

                if (_children.TryGetValue(key, out List<byte[]>? childKeys))
                {
                    _children.Remove(key, out _);
                    foreach (byte[] childKey in childKeys)
                        stack.Push(childKey);
                }
            }
        }
    }

    /// <summary>
    /// Traverses all trie nodes reachable from <paramref name="stateRoot"/> and writes them to
    /// <paramref name="mainStateStore"/>. Reads from the MemDb overlay first, then falls back
    /// to the main state store (disk) via. Writing nodes already present on disk is idempotent.
    /// Used for shutdown persistence only.
    /// </summary>
    public void TraverseTrieAndCopyTo(Hash256 stateRoot, INodeStorage mainStateStore)
    {
        Stack<(Hash256? address, TreePath path, Hash256 hash)> stack = new();
        stack.Push((null, TreePath.Empty, stateRoot));

        while (stack.TryPop(out (Hash256? addr, TreePath p, Hash256 h) item))
        {
            // Could call TryLoadRlp instead which would call baseStore.TryLoadRlp and even if base trie store has been disposed,
            // fetching from the underlying node storage would still work, but clearer to just use the passed node storage here.
            // Base trie store's underlying node storage is the same as this passed one.
            byte[]? rlp = _nodeStorage.Get(item.addr, in item.p, item.h) ?? mainStateStore.Get(item.addr, in item.p, item.h);
            if (rlp is null)
                continue;

            TupleStackSink stackSink = new(stack);
            PushChildren(rlp, item.addr, item.p, ref stackSink);
            mainStateStore.Set(item.addr, in item.p, item.h, rlp);
        }
    }

    /// <summary>
    /// Called by <see cref="TrackingCommitter"/> for each node written to the MemDb overlay.
    /// Initialises this node's parent count and increments the parent count of its MemDb-resident children.
    /// Since Patricia tries commit bottom-up (children before parents), children are already tracked
    /// when their parent node is processed here.
    /// </summary>
    private void TrackCommittedNode(byte[] nodeKey, CappedArray<byte> fullRlp, Hash256? address, TreePath path)
    {
        lock (_refCountLock)
        {
            // Node already tracked (committed during a prior reconstruction) — skip.
            if (_parents.ContainsKey(nodeKey))
                return;

            _parents[nodeKey] = 0;
        }

        // Decode children outside the lock: RLP decoding is pure CPU work with no shared state.
        List<byte[]> childKeys = [];
        KeyListSink keySink = new(childKeys);
        PushChildren(fullRlp, address, path, ref keySink);

        if (childKeys.Count == 0)
            return;

        lock (_refCountLock)
        {
            _children[nodeKey] = childKeys;
            foreach (byte[] childKey in childKeys)
            {
                if (_parents.TryGetValue(childKey, out int cnt))
                    _parents[childKey] = cnt + 1;
                // else: child is disk-resident (not in MemDb overlay) — no tracking needed.
            }
        }
    }

    private sealed class TrackingCommitter(
        ReconstructedStateTrieStore store,
        RawScopedTrieStore.Committer inner,
        Hash256? address) : ICommitter
    {
        public void Dispose() => inner.Dispose();

        public TrieNode CommitNode(ref TreePath path, TrieNode node)
        {
            TrieNode result = inner.CommitNode(ref path, node);
            if (!node.IsBoundaryProofNode && node.Keccak is not null)
            {
                byte[] nodeKey = NodeStorage.GetHalfPathNodeStoragePath(address, path, node.Keccak);
                store.TrackCommittedNode(nodeKey, node.FullRlp, address, path);
            }
            return result;
        }
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
                // Inline child (< 32 bytes) is embedded in the parent — not a separate MemDb entry.
            }
        }
    }

    private static Hash256? DecodeAccountStorageRoot(ReadOnlySpan<byte> accountRlp)
    {
        Rlp.ValueDecoderContext ctx = new Rlp.ValueDecoderContext(accountRlp);
        Hash256 storageRoot = _accountDecoder.DecodeStorageRootOnly(ref ctx);
        return storageRoot == Keccak.EmptyTreeHash ? null : storageRoot;
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
}
