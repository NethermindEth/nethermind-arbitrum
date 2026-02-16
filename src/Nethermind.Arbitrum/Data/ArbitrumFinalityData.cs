// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// Represents finality data for an Arbitrum block containing message index and block hash.
/// </summary>
public readonly struct ArbitrumFinalityData(ulong messageIndex, Hash256 blockHash)
{
    /// <summary>
    /// The Arbitrum message index for this block.
    /// </summary>
    public ulong MessageIndex => messageIndex;

    /// <summary>
    /// The hash of the block.
    /// </summary>
    public Hash256 BlockHash => blockHash;

    public override string ToString() => $"MessageIndex: {MessageIndex}, BlockHash: {BlockHash}";
}
