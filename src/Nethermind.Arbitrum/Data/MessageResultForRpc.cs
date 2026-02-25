// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core.Crypto;
using Newtonsoft.Json;

namespace Nethermind.Arbitrum.Data;

public class MessageResultForRpc
{
    [JsonProperty("hash")]
    public Hash256? Hash { get; set; }

    [JsonProperty("sendRoot")]
    public Hash256? SendRoot { get; set; }

    /// <summary>
    /// Total gas consumed by the block.
    /// Only present when Nitro is configured with ExposeGasUsedInMessageResult.
    /// Used for benchmarking only - not present in production responses.
    /// </summary>
    [JsonProperty("gasUsed", NullValueHandling = NullValueHandling.Ignore)]
    public ulong? GasUsed { get; set; }

    public MessageResult ToMessageResult() => new()
    {
        BlockHash = Hash ?? Hash256.Zero,
        SendRoot = SendRoot ?? Hash256.Zero,
        GasUsed = GasUsed
    };
}
