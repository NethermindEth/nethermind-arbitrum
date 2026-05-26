// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// Provides caching for L1 block numbers and hashes to optimize EVM opcode execution.
/// </summary>
public interface IL1BlockCache
{
    /// <summary>
    /// Gets the cached L1 block number for the current transaction, or null if not cached.
    /// Cache lifetime is per-transaction and should be cleared between transactions.
    /// </summary>
    ulong? GetCachedL1BlockNumber();

    /// <summary>
    /// Sets the cached L1 block number for the current transaction.
    /// </summary>
    void SetCachedL1BlockNumber(ulong blockNumber);

    /// <summary>
    /// Clears the per-transaction L1 block number cache.
    /// Should be called when starting a new transaction.
    /// </summary>
    void ClearL1BlockNumberCache();

    /// <summary>
    /// Attempts to get a cached L1 block hash.
    /// Uses global LRU cache with application lifetime (256 entries).
    /// </summary>
    bool TryGetL1BlockHash(ulong l1BlockNumber, out Hash256 hash);

    /// <summary>
    /// Sets a cached L1 block hash in the global LRU cache.
    /// </summary>
    void SetL1BlockHash(ulong l1BlockNumber, Hash256 hash);
}
