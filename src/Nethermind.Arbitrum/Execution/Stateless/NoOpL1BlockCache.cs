// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Evm;

/// <summary>
/// No-op implementation of L1 block caching.
/// Does not cache any block numbers or hashes so that validator
/// always goes through the world state and records state accesses.
/// </summary>
public sealed class NoOpL1BlockCache : IL1BlockCache
{
    public ulong? GetCachedL1BlockNumber()
    {
        return null;
    }

    public void SetCachedL1BlockNumber(ulong blockNumber)
    {
    }

    public void ClearL1BlockNumberCache()
    {
    }

    public bool TryGetL1BlockHash(ulong l1BlockNumber, out Hash256 hash)
    {
        hash = Hash256.Zero;
        return false;
    }

    public void SetL1BlockHash(ulong l1BlockNumber, Hash256 hash)
    {
    }
}
