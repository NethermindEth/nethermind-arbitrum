// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Data.Converters;

// Nullable variant of GoCompatULongConverter for Go's *uint64
public class GoCompatNullableULongConverter : JsonConverter<ulong?>
{
    private static readonly ULongConverter s_inner = new();

    public override ulong? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        return s_inner.Read(ref reader, typeof(ulong), options);
    }

    public override void Write(Utf8JsonWriter writer, ulong? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value.Value);
    }
}
