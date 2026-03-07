// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.JsonRpc.Data;

namespace Nethermind.Arbitrum.Rpc;

/// <summary>
/// Custom JSON converter for ReceiptForRpc that enables polymorphic serialization.
/// This ensures that derived types like ArbitrumReceiptForRpc serialize their own properties
/// (e.g., GasUsedForL1, MultiGasUsed) instead of only the base class properties.
/// </summary>
public class ReceiptForRpcPolymorphicConverter : JsonConverter<ReceiptForRpc>
{
    public override ReceiptForRpc? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // For deserialization, use the default behavior
        // Create a copy of options without this converter to avoid recursion
        var newOptions = new JsonSerializerOptions(options);
        newOptions.Converters.Remove(this);
        return JsonSerializer.Deserialize<ReceiptForRpc>(ref reader, newOptions);
    }

    public override void Write(Utf8JsonWriter writer, ReceiptForRpc value, JsonSerializerOptions options)
    {
        // Serialize using the actual runtime type to include derived class properties
        var runtimeType = value.GetType();

        // Create a copy of options without this converter to avoid recursion
        var newOptions = new JsonSerializerOptions(options);
        newOptions.Converters.Remove(this);

        JsonSerializer.Serialize(writer, value, runtimeType, newOptions);
    }
}
