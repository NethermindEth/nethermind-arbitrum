// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;
using Nethermind.State;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// LRU cache for fast per-address nonce pre-validation.
/// </summary>
public class NonceCache(int size)
{
    private readonly LruCache<Address, ulong> _cache = new(size, "SequencerNonceCache");
    private Hash256 _blockHash = Keccak.Zero;
    private BlockHeader? _dirty;

    public void BeginNewBlock()
    {
        _dirty = null;
    }

    public ulong Get(BlockHeader header, IStateReader stateReader, Address sender)
    {
        if (!Matches(header))
            Reset(header.ParentHash ?? Keccak.Zero);

        if (_cache.TryGet(sender, out ulong nonce))
            return nonce;

        nonce = (ulong)stateReader.GetNonce(header, sender);
        _cache.Set(sender, nonce);
        return nonce;
    }

    public void Update(BlockHeader header, Address sender, ulong nonce)
    {
        if (!Matches(header))
            Reset(header.ParentHash ?? Keccak.Zero);

        _dirty = header;
        _cache.Set(sender, nonce);
    }

    public void Finalize(Block block)
    {
        if (_blockHash == (block.Header.ParentHash ?? Keccak.Zero))
        {
            _blockHash = block.Hash!;
            _dirty = null;
        }
        else
            Reset(block.Hash!);
    }

    private bool Matches(BlockHeader header)
    {
        if (_dirty is not null)
            return _dirty.Hash == header.Hash;

        return _blockHash == (header.ParentHash ?? Keccak.Zero);
    }

    private void Reset(Hash256 blockHash)
    {
        _cache.Clear();
        _blockHash = blockHash;
        _dirty = null;
    }
}
