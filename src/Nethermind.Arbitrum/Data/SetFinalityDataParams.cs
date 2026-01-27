// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// Parameters for the SetFinalityData JSON-RPC method.
/// </summary>
public sealed class SetFinalityDataParams
{
    public RpcFinalityData? SafeFinalityData { get; set; }
    public RpcFinalityData? FinalizedFinalityData { get; set; }
    public RpcFinalityData? ValidatedFinalityData { get; set; }
}

/// <summary>
/// RPC representation of finality data that matches the JSON-RPC interface.
/// </summary>
public readonly struct RpcFinalityData
{
    public required ulong MsgIdx { get; init; }
    public required Hash256 BlockHash { get; init; }

    /// <summary>
    /// Converts RPC finality data to internal ArbitrumFinalityData.
    /// </summary>
    public ArbitrumFinalityData ToArbitrumFinalityData() =>
        new(MsgIdx, BlockHash);

    /// <summary>
    /// Creates RPC finality data from internal ArbitrumFinalityData.
    /// </summary>
    public static RpcFinalityData? FromArbitrumFinalityData(ArbitrumFinalityData? data) =>
        data?.MessageIndex is ulong msgIdx && data?.BlockHash is Hash256 blockHash
            ? new() { MsgIdx = msgIdx, BlockHash = blockHash }
            : null;
}
