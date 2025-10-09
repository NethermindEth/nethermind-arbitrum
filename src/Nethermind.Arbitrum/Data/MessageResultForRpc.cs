// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core.Crypto;
using Newtonsoft.Json;

namespace Nethermind.Arbitrum.Data;

public class MessageResultForRpc
{
    [JsonProperty("hash")]
    public Hash256? Hash { get; set; }
    
    [JsonProperty("sendRoot")]
    public Hash256? SendRoot { get; set; }

    public MessageResult ToMessageResult() => new()
    {
        BlockHash = Hash ?? Hash256.Zero,
        SendRoot = SendRoot ?? Hash256.Zero
    };
}
