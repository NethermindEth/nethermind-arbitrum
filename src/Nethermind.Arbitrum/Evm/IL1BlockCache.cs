// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Provides caching for L1 block numbers and hashes to optimize EVM opcode execution.
/// </summary>
public interface IL1BlockCache
{
    /// <summary>
    /// Clears the per-transaction L1 block number cache.
    /// Should be called when starting a new transaction.
    /// </summary>
    public void ClearL1BlockNumberCache();
    /// <summary>
    /// Gets the cached L1 block number for the current transaction, or null if not cached.
    /// Cache lifetime is per-transaction and should be cleared between transactions.
    /// </summary>
    public ulong? GetCachedL1BlockNumber();

    /// <summary>
    /// Sets the cached L1 block number for the current transaction.
    /// </summary>
    public void SetCachedL1BlockNumber(ulong blockNumber);

    /// <summary>
    /// Sets a cached L1 block hash in the global LRU cache.
    /// </summary>
    public void SetL1BlockHash(ulong l1BlockNumber, Hash256 hash);

    /// <summary>
    /// Attempts to get a cached L1 block hash.
    /// Uses global LRU cache with application lifetime (256 entries).
    /// </summary>
    public bool TryGetL1BlockHash(ulong l1BlockNumber, out Hash256 hash);
}
