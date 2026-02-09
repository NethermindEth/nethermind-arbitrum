// SPDX-License-Identifier: BUSL-1.1
// SPDX-License-Identifier: MIT

using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;

/// <summary>
/// Helper class for encoding calldata for the log Stylus contract.
/// The log contract emits events with specified topics and data.
/// Format: [num_topics:1 byte][topic1:32]...[topicN:32][data:rest]
/// See nitro/arbitrator/stylus/tests/log/src/main.rs for reference.
/// </summary>
public static class LogContractCallData
{
    /// <summary>
    /// Creates calldata for the log contract to emit an event.
    /// </summary>
    /// <param name="topics">Array of topics (0-4 topics per EVM spec)</param>
    /// <param name="data">Log data payload</param>
    public static byte[] CreateLogCallData(Hash256[] topics, byte[] data)
    {
        int totalSize = 1 + topics.Length * Hash256.Size + data.Length;
        byte[] result = new byte[totalSize];
        int offset = 0;

        result[offset++] = (byte)topics.Length;

        foreach (Hash256 topic in topics)
        {
            topic.Bytes.CopyTo(result.AsSpan(offset, Hash256.Size));
            offset += Hash256.Size;
        }

        if (data.Length > 0)
            data.CopyTo(result.AsSpan(offset));

        return result;
    }

    /// <summary>
    /// Creates calldata for emitting a log with specified number of topics.
    /// </summary>
    public static byte[] CreateLogCallData(int numTopics, byte[] data, out Hash256[] generatedTopics)
    {
        generatedTopics = new Hash256[numTopics];
        for (int i = 0; i < numTopics; i++)
        {
            byte[] bytes = new byte[Hash256.Size];
            bytes[^1] = (byte)(i + 1);
            generatedTopics[i] = new Hash256(bytes);
        }

        return CreateLogCallData(generatedTopics, data);
    }
}
