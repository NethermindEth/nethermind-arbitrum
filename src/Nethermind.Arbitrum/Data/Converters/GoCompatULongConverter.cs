// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Data.Converters;

// Go's encoding/json only accepts JSON numbers for uint64
public class GoCompatULongConverter : JsonConverter<ulong>
{
    private static readonly ULongConverter s_inner = new();

    public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => s_inner.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
