// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Data;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Facade.Eth;

namespace Nethermind.Arbitrum.Rpc;

public class ArbitrumBlockForRpc(Block block, bool includeFullTransactionData, ISpecProvider specProvider, ArbitrumBlockHeaderInfo headerInfo)
    : BlockForRpc(block, includeFullTransactionData, specProvider)
{
    [JsonPropertyName("l1BlockNumber")]
    public ulong L1BlockNumber { get; set; } = headerInfo.L1BlockNumber;

    [JsonPropertyName("sendRoot")]
    public Hash256 SendRoot { get; set; } = headerInfo.SendRoot;

    [JsonPropertyName("sendCount")]
    public ulong SendCount { get; set; } = headerInfo.SendCount;
}
