// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// RPC-facing DTO for neth_publishExpressLaneTransaction.
/// </summary>
public class ExpressLaneSubmissionForRpc
{
    /// <summary>RLP-encoded transaction bytes.</summary>
    public byte[] Transaction { get; set; } = [];

    /// <summary>The express lane auction round this submission targets.</summary>
    public ulong Round { get; set; }

    /// <summary>Per-round sequence number for in-order processing.</summary>
    public ulong SequenceNumber { get; set; }

    /// <summary>65-byte ECDSA signature over the canonical message bytes.</summary>
    public byte[] Signature { get; set; } = [];

    /// <summary>Chain ID included in the signing message.</summary>
    public ulong ChainId { get; set; }

    /// <summary>Address of the ExpressLaneAuction contract included in the signing message.</summary>
    public Address AuctionContractAddress { get; set; } = Address.Zero;
}
