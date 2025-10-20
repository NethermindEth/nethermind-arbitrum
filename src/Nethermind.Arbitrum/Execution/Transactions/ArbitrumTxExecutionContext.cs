// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Transactions;

/// <summary>
/// Execution context for Arbitrum transactions.
/// Lifetime: One instance per transaction (reused via Reset()).
/// </summary>
public class ArbitrumTxExecutionContext
{
    public Hash256? CurrentRetryable { get; set; }

    public Address? CurrentRefundTo { get; set; }

    public UInt256 PosterFee { get; set; }

    public ulong PosterGas { get; set; }

    // Amount of gas temporarily held to prevent compute from exceeding the block gas limit
    public ulong ComputeHoldGas { get; set; }

    public ArbitrumTxType TopLevelTxType { get; set; }

    /// <summary>
    /// Cached L1 block number for the current transaction.
    /// Cleared via Reset() between transactions to prevent stale data.
    /// </summary>
    public ulong? CachedL1BlockNumber { get; set; }

    /// <summary>
    /// Cached L1 block hashes for the current transaction.
    /// Key: L1 block number, Value: Block hash
    /// Maximum size: 256 entries (BLOCKHASH opcode only looks back 256 blocks)
    /// Cleared via Reset() between transactions to prevent infinite memory growth.
    /// Memory usage: ~12 KB at maximum capacity (256 * 48 bytes per entry)
    /// </summary>
    public Dictionary<ulong, Hash256> CachedL1BlockHashes { get; } = new();

    /// <summary>
    /// Resets the context for the next transaction.
    /// Must clear caches to prevent memory growth and stale data.
    /// </summary>
    public void Reset()
    {
        CurrentRetryable = null;
        CurrentRefundTo = null;
        PosterFee = 0;
        PosterGas = 0;
        ComputeHoldGas = 0;
        TopLevelTxType = ArbitrumTxType.EthLegacy;

        // Cache cleanup
        CachedL1BlockNumber = null;
        CachedL1BlockHashes.Clear();
    }
}
