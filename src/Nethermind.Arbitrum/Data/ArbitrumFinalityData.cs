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
    /// The Arbitrum message index for this block.
    /// </summary>
    public ulong MessageIndex { get; } = messageIndex;

    /// <summary>
    /// The hash of the block.
    /// </summary>
    public Hash256 BlockHash { get; } = blockHash;

    public override string ToString() => $"MessageIndex: {MessageIndex}, BlockHash: {BlockHash}";
}

/// <summary>
/// Represents the type of finality block being processed.
/// </summary>
public enum FinalityBlockType
{
    /// <summary>
    /// Safe block - block that is unlikely to be reorganized
    /// </summary>
    Safe,

    /// <summary>
    /// Finalized block - block that cannot be reorganized under normal conditions
    /// </summary>
    Finalized,

    /// <summary>
    /// Validated block - block that has been validated (used for validator wait logic)
    /// </summary>
    Validated
}
