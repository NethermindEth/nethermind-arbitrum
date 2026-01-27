// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// JSON converter for MessageIndex that serializes as a raw number
/// </summary>
public class MessageIndexJsonConverter : JsonConverter<MessageIndex>
{
    public override MessageIndex Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => new MessageIndex(reader.GetUInt64()),
            JsonTokenType.String => new MessageIndex(ULongConverter.FromString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan)),
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to MessageIndex")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        MessageIndex value,
        JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
