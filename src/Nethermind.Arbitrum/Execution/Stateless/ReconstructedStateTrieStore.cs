// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Concurrent;
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

    /// <summary>Per-MemDb-key reference counts for tracking which nodes are still needed by at least one alive state root.</summary>
    private readonly Dictionary<byte[], int> _refCounts = new(Bytes.EqualityComparer);

    private static readonly AccountDecoder _accountDecoder = AccountDecoder.Instance;

    public void Dispose()
    {
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
        => new RawScopedTrieStore.Committer(_nodeStorage, address, writeFlags);

    /// <summary>
    /// Traverses all MemDb-resident trie nodes reachable from the given state root and increments
    /// their reference counts. Call when adding a state root to the alive set.
    /// </summary>
    public void Reference(Hash256 stateRoot)
    {
        Traverse(null, TreePath.Empty, stateRoot, key =>
        {
            _refCounts[key] = _refCounts.TryGetValue(key, out int count) ? count + 1 : 1;
        });
    }

    /// <summary>
    /// Traverses all MemDb-resident trie nodes reachable from the given state root and decrements
    /// their reference counts. Nodes whose count reaches zero are evicted from the MemDb.
    /// Call when removing a state root from the alive set.
    /// </summary>
    public void Dereference(Hash256 stateRoot)
    {
        Traverse(null, TreePath.Empty, stateRoot, key =>
        {
            if (!_refCounts.TryGetValue(key, out int count))
                return;

            if (count <= 1)
            {
                _refCounts.Remove(key, out _);
                _memDb.Remove(key);
            }
            else
            {
                _refCounts[key] = count - 1;
            }
        });
    }

    private void Traverse(Hash256? address, TreePath path, Hash256 hash, Action<byte[]> onKey)
    {
        Stack<(Hash256? address, TreePath path, Hash256 hash)> stack = new();
        stack.Push((address, path, hash));

        while (stack.TryPop(out (Hash256? addr, TreePath p, Hash256 h) item))
        {
            byte[] key = NodeStorage.GetHalfPathNodeStoragePath(item.addr, item.p, item.h);
            byte[]? rlp = _memDb[key];
            // If the node is not in memDB, neither are its children. Then no need to reference them.
            if (rlp is null)
                continue;

            // Push children to stack BEFORE calling onKey (which during Dereference may delete this node).
            // Since this is a tree traversal (no intra-trie node sharing under HalfPath scheme), each key
            // is visited at most once.
            PushChildren(rlp, item.addr, item.p, stack);
            onKey(key);
        }
    }

    private static void PushChildren(
        byte[] rlp,
        Hash256? address,
        TreePath path,
        Stack<(Hash256? address, TreePath path, Hash256 hash)> stack)
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
                    stack.Push((address, path.Append(i), stream.DecodeKeccak()!));
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
                        stack.Push((addressHash, TreePath.Empty, storageRoot));
                    }
                }
                // Storage trie leaf: value is a storage slot — no child nodes.
            }
            else
            {
                // Extension node: single hash-referenced child, path extended by pathNibbles.
                (int _, int contentLength) = stream.PeekPrefixAndContentLength();
                if (contentLength == 32)
                    stack.Push((address, path.Append(pathNibbles), stream.DecodeKeccak()!));
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
}
