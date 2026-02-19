using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Execution.Stateless;

/// <summary>
/// Overlay trie store for state reconstruction. Reconstructed trie nodes are stored in a MemDb overlay
/// and fall back to the base store (main TrieStore dirty cache + disk) for reads.
/// BeginScope is a no-op to avoid acquiring the main TrieStore's scope/pruning locks during
/// potentially long-running state reconstruction.
/// </summary>
public class ReconstructedStateTrieStore(IKeyValueStoreWithBatching keyValueStore, IReadOnlyTrieStore baseStore) : ITrieStore, IReadOnlyTrieStore
{
    private readonly INodeStorage _nodeStorage = new NodeStorage(keyValueStore);

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

    public bool IsPersisted(Hash256? address, in TreePath path, in ValueHash256 keccak)
        => _nodeStorage.Get(address, in path, in keccak) is not null || baseStore.IsPersisted(address, in path, in keccak);

    public bool HasRoot(Hash256 stateRoot)
        => _nodeStorage.Get(null, TreePath.Empty, stateRoot) is not null || baseStore.HasRoot(stateRoot);

    public IDisposable BeginScope(BlockHeader? baseBlock) => new Reactive.AnonymousDisposable(() => { });

    public IScopedTrieStore GetTrieStore(Hash256? address) => new ScopedTrieStore(this, address);

    public INodeStorage.KeyScheme Scheme => baseStore.Scheme;

    public IBlockCommitter BeginBlockCommit(long blockNumber) => NullCommitter.Instance;

    public ICommitter BeginCommit(Hash256? address, TrieNode? root, WriteFlags writeFlags)
        => new RawScopedTrieStore.Committer(_nodeStorage, address, writeFlags);
}
