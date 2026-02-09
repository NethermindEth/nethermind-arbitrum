// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Int256;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Data.Converters;

// Go's *big.Int.UnmarshalJSON delegates to UnmarshalText with raw bytes — requires unquoted decimal numbers
public class GoCompatUInt256Converter : JsonConverter<UInt256>
{
    private static readonly UInt256Converter s_inner = new();

    public override UInt256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => s_inner.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, UInt256 value, JsonSerializerOptions options)
        => writer.WriteRawValue(((BigInteger)value).ToString(CultureInfo.InvariantCulture));
}
