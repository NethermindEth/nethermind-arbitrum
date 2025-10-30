// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum;

/// <summary>
/// Arbitrum-specific constants for EIP-2935 historical block hash storage.
/// </summary>
public static class ArbitrumEip2935Constants
{
    /// <summary>
    /// Arbitrum's ring buffer size for EIP-2935 history storage: 393,168 blocks.
    /// This provides approximately 1 day of block history at Arbitrum's target block time of 0.22 seconds.
    /// Compare to Ethereum's 8,191 blocks (see <see cref="Core.Eip2935Constants.RingBufferSize"/>)
    /// which provides ~1 day at Ethereum's 12 second block time.
    /// See https://github.com/OffchainLabs/go-ethereum/blob/57fe4b732d4e640e696da40773f2dacba97e722b/params/protocol_params.go#L218
    /// https://github.com/OffchainLabs/sys-asm/blob/4deae902aeb5ec6453531b2c7c20131b3c9f394c/src/execution_hash/main.eas#L19
    /// </summary>
    public const long RingBufferSize = 393168;
}
