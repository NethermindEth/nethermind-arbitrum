// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;

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
        return reader.TokenType != JsonTokenType.Number ?
            throw new JsonException($"Expected number, got {reader.TokenType}") :
            new MessageIndex(reader.GetUInt64());
    }

    public override void Write(
        Utf8JsonWriter writer,
        MessageIndex value,
        JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}
