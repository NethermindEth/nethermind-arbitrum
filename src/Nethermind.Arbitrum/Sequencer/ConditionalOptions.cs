// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Represents conditional options for transaction execution
/// </summary>
public abstract class ConditionalOptions
{
    /// <summary>
    /// Known account storage requirements - maps address to root hash or specific slot values
    /// </summary>
    public Dictionary<Address, RootHashOrSlots> KnownAccounts { get; set; } = new();

    /// <summary>
    /// Minimum L1 block number for transaction inclusion
    /// </summary>
    public ulong? BlockNumberMin { get; set; }

    /// <summary>
    /// Maximum L1 block number for transaction inclusion
    /// </summary>
    public ulong? BlockNumberMax { get; set; }

    /// <summary>
    /// Minimum L2 timestamp for transaction inclusion
    /// </summary>
    public ulong? TimestampMin { get; set; }

    /// <summary>
    /// Maximum L2 timestamp for transaction inclusion
    /// </summary>
    public ulong? TimestampMax { get; set; }
}

/// <summary>
/// Storage requirements - either a root hash or specific slot values
/// </summary>
public abstract class RootHashOrSlots
{
    /// <summary>
    /// Expected storage root hash for the account
    /// </summary>
    public Hash256? RootHash { get; set; }

    /// <summary>
    /// Expected values for specific storage slots
    /// </summary>
    public Dictionary<UInt256, UInt256> SlotValues { get; set; } = new();
}
