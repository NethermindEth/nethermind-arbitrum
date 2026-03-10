// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Data.Converters;

// Nullable variant of GoCompatUInt256Converter for Go's *big.Int nil
public class GoCompatNullableUInt256Converter : JsonConverter<UInt256?>
{
    private static readonly GoCompatUInt256Converter s_inner = new();

    public override UInt256? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        return s_inner.Read(ref reader, typeof(UInt256), options);
    }

    public override void Write(Utf8JsonWriter writer, UInt256? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        s_inner.Write(writer, value.Value, options);
    }
}
