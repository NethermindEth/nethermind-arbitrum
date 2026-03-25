// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.JsonRpc.Data;

namespace Nethermind.Arbitrum.Rpc;

/// <summary>
/// Custom JSON converter for ReceiptForRpc that enables polymorphic serialization.
/// This ensures that derived types like ArbitrumReceiptForRpc serialize their own properties
/// (e.g., GasUsedForL1, MultiGasUsed) instead of only the base class properties.
/// </summary>
/// <remarks>
/// Required because ReceiptForRpc is in upstream Nethermind and cannot be decorated with
/// [JsonDerivedType] attributes for Arbitrum-specific types.
/// </remarks>
public class ReceiptForRpcPolymorphicConverter : JsonConverter<ReceiptForRpc>
{
    // Cache options without this converter to avoid allocation on every serialization
    private static readonly ConcurrentDictionary<JsonSerializerOptions, JsonSerializerOptions> _optionsCache = new();

    public override ReceiptForRpc? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<ReceiptForRpc>(ref reader, GetOptionsWithoutConverter(options));

    public override void Write(Utf8JsonWriter writer, ReceiptForRpc value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), GetOptionsWithoutConverter(options));

    private JsonSerializerOptions GetOptionsWithoutConverter(JsonSerializerOptions options) =>
        _optionsCache.GetOrAdd(options, static opts =>
        {
            var newOptions = new JsonSerializerOptions(opts);
            // Remove all instances of this converter type
            for (int i = newOptions.Converters.Count - 1; i >= 0; i--)
            {
                if (newOptions.Converters[i] is ReceiptForRpcPolymorphicConverter)
                    newOptions.Converters.RemoveAt(i);
            }
            return newOptions;
        });
}
