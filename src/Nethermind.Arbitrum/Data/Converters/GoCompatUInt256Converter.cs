// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
