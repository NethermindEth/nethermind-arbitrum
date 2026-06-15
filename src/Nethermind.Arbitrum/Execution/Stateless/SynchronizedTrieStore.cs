// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Trie;
using Nethermind.Trie.Pruning;

namespace Nethermind.Arbitrum.Execution.Stateless;

public sealed class SynchronizedTrieStore(ITrieStore inner) : ITrieStore
{
    private readonly Lock _lock = new();

    public TrieNode FindCachedOrUnknown(Hash256? address, in TreePath path, Hash256 hash)
    {
        lock (_lock)
        {
            return inner.FindCachedOrUnknown(address, in path, hash);
        }
    }

    public byte[]? LoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
    {
        lock (_lock)
        {
            return inner.LoadRlp(address, in path, hash, flags);
        }
    }

    public byte[]? TryLoadRlp(Hash256? address, in TreePath path, Hash256 hash, ReadFlags flags = ReadFlags.None)
    {
        lock (_lock)
        {
            return inner.TryLoadRlp(address, in path, hash, flags);
        }
    }

    public ICommitter BeginCommit(Hash256? address, TrieNode? root, WriteFlags writeFlags) => inner.BeginCommit(address, root, writeFlags);

    public INodeStorage.KeyScheme Scheme => inner.Scheme;

    public bool HasRoot(Hash256 stateRoot) => inner.HasRoot(stateRoot);

    public IDisposable BeginScope(BlockHeader? baseBlock) => inner.BeginScope(baseBlock);

    public IScopedTrieStore GetTrieStore(Hash256? address) => new ScopedTrieStore(this, address);

    public IBlockCommitter BeginBlockCommit(long blockNumber) => inner.BeginBlockCommit(blockNumber);

    public void Dispose() => inner.Dispose();
}
