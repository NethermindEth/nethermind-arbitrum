// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Data.Converters;

public sealed class AccountStateConditionConverter : JsonConverter<AccountStateCondition>
{
    public override AccountStateCondition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            Hash256? hash = JsonSerializer.Deserialize<Hash256>(ref reader, options);
            return new AccountStateCondition { RootHash = hash };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            Dictionary<UInt256, Hash256>? slots = JsonSerializer.Deserialize<Dictionary<UInt256, Hash256>>(ref reader, options);
            return new AccountStateCondition { SlotValues = slots };
        }

        throw new JsonException($"Expected string or object for AccountStateCondition, got {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, AccountStateCondition value, JsonSerializerOptions options)
    {
        if (value.RootHash is not null)
        {
            JsonSerializer.Serialize(writer, value.RootHash, options);
        }
        else if (value.SlotValues is not null)
        {
            JsonSerializer.Serialize(writer, value.SlotValues, options);
        }
        else
        {
            throw new JsonException("AccountStateCondition must have either RootHash or SlotValues set");
        }
    }
}
