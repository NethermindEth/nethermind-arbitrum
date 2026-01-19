// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// Represents finality data for an Arbitrum block containing message index and block hash.
/// </summary>
public readonly struct ArbitrumFinalityData(ulong messageIndex, Hash256 blockHash)
{
    /// <summary>
    /// The hash of the block.
    /// </summary>
    public Hash256 BlockHash => blockHash;
    /// <summary>
    /// The Arbitrum message index for this block.
    /// </summary>
    public ulong MessageIndex => messageIndex;

    public override string ToString() => $"MessageIndex: {MessageIndex}, BlockHash: {BlockHash}";
}
