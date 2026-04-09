// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Data;

public sealed class RecordResult
{
    [JsonConverter(typeof(ULongRawJsonConverter))]
    public ulong Pos { get; }

    public Hash256 BlockHash { get; }

    [JsonConverter(typeof(PreimagesConverter))]
    public Dictionary<Hash256, byte[]?> Preimages { get; }

    [JsonConverter(typeof(UserWasmsConverter))]
    public Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>> UserWasms { get; }

    public RecordResult(ulong messageIndex, Hash256 blockHash, ArbitrumWitness arbWitness)
    {
        Pos = messageIndex;
        BlockHash = blockHash;
        UserWasms = arbWitness.UserWasms?.ToDictionary(
            kvp => kvp.Key.ToHash256(),
            kvp => kvp.Value) ?? new();

        // Witness codes, states and headers should all be unique, so, using Add() is safe here
        Preimages = new();
        foreach (byte[] code in arbWitness.Witness.Codes)
            Preimages.Add(Keccak.Compute(code), code);
        foreach (byte[] state in arbWitness.Witness.State)
            Preimages.Add(Keccak.Compute(state), state);
        foreach (byte[] header in arbWitness.Witness.Headers)
            Preimages.Add(Keccak.Compute(header), header);
    }
}

/// <summary>
/// Serializes <see cref="RecordResult.Preimages"/> with base64-encoded values, matching Go's
/// default JSON encoding of <c>map[common.Hash][]byte</c>.
/// </summary>
file sealed class PreimagesConverter : JsonConverter<Dictionary<Hash256, byte[]?>>
{
    public override Dictionary<Hash256, byte[]?> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new Dictionary<Hash256, byte[]?>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            Hash256 key = new(reader.GetString()!);
            reader.Read();
            byte[]? value = reader.TokenType == JsonTokenType.Null ? null : reader.GetBytesFromBase64();
            result[key] = value;
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<Hash256, byte[]?> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach ((Hash256 key, byte[]? bytes) in value)
        {
            writer.WritePropertyName(key.ToString());
            if (bytes is null)
                writer.WriteNullValue();
            else
                writer.WriteBase64StringValue(bytes);
        }
        writer.WriteEndObject();
    }
}

/// <summary>
/// Serializes <see cref="RecordResult.UserWasms"/> with base64-encoded leaf byte values, matching
/// Go's default JSON encoding of <c>map[common.Hash]map[string][]byte</c>.
/// </summary>
file sealed class UserWasmsConverter : JsonConverter<Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>>>
{
    public override Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            Hash256 outerKey = new(reader.GetString()!);
            reader.Read(); // StartObject (inner)
            var inner = new Dictionary<string, byte[]>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                string innerKey = reader.GetString()!;
                reader.Read();
                inner[innerKey] = reader.GetBytesFromBase64();
            }
            result[outerKey] = inner;
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<Hash256, IReadOnlyDictionary<string, byte[]>> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach ((Hash256 outerKey, IReadOnlyDictionary<string, byte[]> inner) in value)
        {
            writer.WritePropertyName(outerKey.ToString());
            writer.WriteStartObject();
            foreach ((string innerKey, byte[] bytes) in inner)
                writer.WriteBase64String(innerKey, bytes);
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
    }
}
