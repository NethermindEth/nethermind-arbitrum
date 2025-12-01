// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Evm;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Data;

/// <summary>
/// All fields are serialized as hex strings with 0x prefix (e.g., "0x5208").
/// </summary>
public class MultiGasJsonConverter : JsonConverter<MultiGas>
{
    public override MultiGas Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected StartObject token");

        ulong unknown = 0;
        ulong computation = 0;
        ulong historyGrowth = 0;
        ulong storageAccess = 0;
        ulong storageGrowth = 0;
        ulong l1Calldata = 0;
        ulong l2Calldata = 0;
        ulong wasmComputation = 0;
        ulong refund = 0;
        ulong total = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new MultiGas(
                    unknown,
                    computation,
                    historyGrowth,
                    storageAccess,
                    storageGrowth,
                    l1Calldata,
                    l2Calldata,
                    wasmComputation,
                    total,
                    refund);

            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected PropertyName token");

            string? propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "unknown":
                    unknown = ReadUInt64Hex(ref reader);
                    break;
                case "computation":
                    computation = ReadUInt64Hex(ref reader);
                    break;
                case "historyGrowth":
                    historyGrowth = ReadUInt64Hex(ref reader);
                    break;
                case "storageAccess":
                    storageAccess = ReadUInt64Hex(ref reader);
                    break;
                case "storageGrowth":
                    storageGrowth = ReadUInt64Hex(ref reader);
                    break;
                case "l1Calldata":
                    l1Calldata = ReadUInt64Hex(ref reader);
                    break;
                case "l2Calldata":
                    l2Calldata = ReadUInt64Hex(ref reader);
                    break;
                case "wasmComputation":
                    wasmComputation = ReadUInt64Hex(ref reader);
                    break;
                case "refund":
                    refund = ReadUInt64Hex(ref reader);
                    break;
                case "total":
                    total = ReadUInt64Hex(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Expected EndObject token");
    }

    [SkipLocalsInit]
    public override void Write(Utf8JsonWriter writer, MultiGas value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        WriteUInt64Hex(writer, "unknown", value.Get(ResourceKind.Unknown));
        WriteUInt64Hex(writer, "computation", value.Get(ResourceKind.Computation));
        WriteUInt64Hex(writer, "historyGrowth", value.Get(ResourceKind.HistoryGrowth));
        WriteUInt64Hex(writer, "storageAccess", value.Get(ResourceKind.StorageAccess));
        WriteUInt64Hex(writer, "storageGrowth", value.Get(ResourceKind.StorageGrowth));
        WriteUInt64Hex(writer, "l1Calldata", value.Get(ResourceKind.L1Calldata));
        WriteUInt64Hex(writer, "l2Calldata", value.Get(ResourceKind.L2Calldata));
        WriteUInt64Hex(writer, "wasmComputation", value.Get(ResourceKind.WasmComputation));
        WriteUInt64Hex(writer, "refund", value.Refund);
        WriteUInt64Hex(writer, "total", value.Total);

        writer.WriteEndObject();
    }

    [SkipLocalsInit]
    private static void WriteUInt64Hex(Utf8JsonWriter writer, string propertyName, ulong value)
    {
        writer.WritePropertyName(propertyName);

        if (value == 0)
            writer.WriteRawValue("\"0x0\""u8, true);
        else
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
            ByteArrayConverter.Convert(writer, bytes);
        }
    }

    private static ulong ReadUInt64Hex(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetUInt64(),
            JsonTokenType.String when !reader.HasValueSequence => ULongConverter.FromString(reader.ValueSpan),
            JsonTokenType.String => ULongConverter.FromString(reader.ValueSequence.ToArray()),
            _ => throw new JsonException("Expected String or Number token for ulong value")
        };
    }
}
