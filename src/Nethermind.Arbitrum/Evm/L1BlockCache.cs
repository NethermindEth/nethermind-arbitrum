// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Default implementation of L1 block caching.
/// Combines per-transaction cache for block number with global LRU cache for block hashes.
/// </summary>
public sealed class L1BlockCache : IL1BlockCache
{
    /// <summary>
    /// Per-transaction cache for L1 block number.
    /// </summary>
    private ulong? _cachedL1BlockNumber;

    /// <summary>
    /// Global LRU cache for L1 block hashes.
    /// 256 capacities match the BLOCKHASH opcode window (last 256 blocks).
    /// Thread-safe and shared across all transactions.
    /// </summary>
    private static readonly ClockCache<ulong, Hash256> CachedL1BlockHashes = new(256);

    public ulong? GetCachedL1BlockNumber()
    {
        return _cachedL1BlockNumber;
    }

    public void SetCachedL1BlockNumber(ulong blockNumber)
    {
        _cachedL1BlockNumber = blockNumber;
    }

    public void ClearL1BlockNumberCache()
    {
        _cachedL1BlockNumber = null;
    }

    public bool TryGetL1BlockHash(ulong l1BlockNumber, out Hash256 hash)
    {
        return CachedL1BlockHashes.TryGet(l1BlockNumber, out hash);
    }

    public void SetL1BlockHash(ulong l1BlockNumber, Hash256 hash)
    {
        CachedL1BlockHashes.Set(l1BlockNumber, hash);
    }
}
