// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Converters;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Data;

[TestFixture]
public class GoCompatConverterTests
{
    private static readonly JsonSerializerOptions s_options = EthereumJsonSerializer.JsonOptions;

    [Test]
    public void GoCompatUInt256Write_WhenZero_WritesRawJsonNumber()
    {
        UInt256 value = UInt256.Zero;
        string json = JsonSerializer.Serialize(value, new JsonSerializerOptions { Converters = { new GoCompatUInt256Converter() } });
        json.Should().Be("0");
    }

    [Test]
    public void GoCompatUInt256Write_WhenNonZero_WritesRawJsonNumber()
    {
        UInt256 value = new(12345);
        string json = JsonSerializer.Serialize(value, new JsonSerializerOptions { Converters = { new GoCompatUInt256Converter() } });
        json.Should().Be("12345");
    }

    [Test]
    public void GoCompatUInt256Read_WhenHexString_ParsesCorrectly()
    {
        UInt256 result = JsonSerializer.Deserialize<UInt256>("\"0x3039\"", new JsonSerializerOptions { Converters = { new GoCompatUInt256Converter() } });
        result.Should().Be(new UInt256(12345));
    }

    [Test]
    public void GoCompatUInt256Read_WhenDecimalString_ParsesCorrectly()
    {
        UInt256 result = JsonSerializer.Deserialize<UInt256>("\"12345\"", new JsonSerializerOptions { Converters = { new GoCompatUInt256Converter() } });
        result.Should().Be(new UInt256(12345));
    }

    [Test]
    public void GoCompatULongWrite_Always_WritesJsonNumber()
    {
        ulong value = 42;
        string json = JsonSerializer.Serialize(value, new JsonSerializerOptions { Converters = { new GoCompatULongConverter() } });
        json.Should().Be("42");
    }

    [Test]
    public void GoCompatULongRead_WhenHexString_ParsesCorrectly()
    {
        ulong result = JsonSerializer.Deserialize<ulong>("\"0x2a\"", new JsonSerializerOptions { Converters = { new GoCompatULongConverter() } });
        result.Should().Be(42UL);
    }

    [Test]
    public void GoCompatULongRead_WhenJsonNumber_ParsesCorrectly()
    {
        ulong result = JsonSerializer.Deserialize<ulong>("42", new JsonSerializerOptions { Converters = { new GoCompatULongConverter() } });
        result.Should().Be(42UL);
    }

    [Test]
    public void GoCompatNullableULongWrite_WhenNull_WritesJsonNull()
    {
        ulong? value = null;
        string json = JsonSerializer.Serialize(value, new JsonSerializerOptions { Converters = { new GoCompatNullableULongConverter() } });
        json.Should().Be("null");
    }

    [Test]
    public void GoCompatNullableULongWrite_WhenHasValue_WritesJsonNumber()
    {
        ulong? value = 99;
        string json = JsonSerializer.Serialize(value, new JsonSerializerOptions { Converters = { new GoCompatNullableULongConverter() } });
        json.Should().Be("99");
    }

    [Test]
    public void StartSequencingResult_WhenRoundtripped_PreservesGoCompatFormats()
    {
        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.L2Message,
            Address.SystemUser,
            100UL,
            1234567UL,
            Hash256.Zero,
            new UInt256(42));
        MessageWithMetadata msgWithMeta = new(
            new L1IncomingMessage(header, new byte[] { 1, 2, 3 }, null, null),
            5UL);
        MessageResultForRpc msgResult = new() { Hash = Keccak.Compute("test"), SendRoot = Keccak.Compute("root") };
        byte[] blockMetadata = new byte[] { 10, 20, 30 };

        SequencedMsg original = new(42UL, msgWithMeta, msgResult, blockMetadata);
        StartSequencingResult result = new(original, 100L);

        string json = JsonSerializer.Serialize(result, s_options);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement sequencedMsg = doc.RootElement.GetProperty("sequencedMsg");
        JsonElement msgWithMetaEl = sequencedMsg.GetProperty("msgWithMeta");
        JsonElement messageEl = msgWithMetaEl.GetProperty("message");
        JsonElement headerEl = messageEl.GetProperty("header");

        headerEl.GetProperty("baseFeeL1").ValueKind.Should().Be(JsonValueKind.Number);
        headerEl.GetProperty("baseFeeL1").GetUInt64().Should().Be(42UL);

        headerEl.GetProperty("blockNumber").ValueKind.Should().Be(JsonValueKind.Number);
        headerEl.GetProperty("blockNumber").GetUInt64().Should().Be(100UL);
        headerEl.GetProperty("timestamp").ValueKind.Should().Be(JsonValueKind.Number);
        headerEl.GetProperty("timestamp").GetUInt64().Should().Be(1234567UL);

        msgWithMetaEl.GetProperty("delayedMessagesRead").ValueKind.Should().Be(JsonValueKind.Number);
        msgWithMetaEl.GetProperty("delayedMessagesRead").GetUInt64().Should().Be(5UL);

        string blockMetaJson = sequencedMsg.GetProperty("blockMetadata").GetString()!;
        Convert.FromBase64String(blockMetaJson).Should().BeEquivalentTo(blockMetadata);

        string l2MsgJson = messageEl.GetProperty("l2Msg").GetString()!;
        Convert.FromBase64String(l2MsgJson).Should().BeEquivalentTo(new byte[] { 1, 2, 3 });

        StartSequencingResult? deserialized = JsonSerializer.Deserialize<StartSequencingResult>(json, s_options);
        deserialized.Should().NotBeNull();
        deserialized!.SequencedMsg!.MsgIdx.Should().Be(42UL);
        deserialized.SequencedMsg.MsgWithMeta.Message.Header.BaseFeeL1.Should().Be(new UInt256(42));
        deserialized.SequencedMsg.MsgWithMeta.Message.Header.BlockNumber.Should().Be(100UL);
        deserialized.SequencedMsg.MsgWithMeta.DelayedMessagesRead.Should().Be(5UL);
        deserialized.SequencedMsg.BlockMetadata.Should().BeEquivalentTo(blockMetadata);
    }

    [Test]
    public void DigestMessage_WhenDeserializedFromGoHex_ParsesCorrectly()
    {
        string goFormatJson = """
        {
            "header": {
                "kind": 3,
                "sender": "0xffffffffffffffffffffffffffffffffffffffff",
                "blockNumber": "0x64",
                "timestamp": "0x3e8",
                "requestId": null,
                "baseFeeL1": "0x2a"
            },
            "l2Msg": "AQID",
            "batchGasCost": null,
            "batchDataTokens": null
        }
        """;

        L1IncomingMessage? msg = JsonSerializer.Deserialize<L1IncomingMessage>(goFormatJson, s_options);
        msg.Should().NotBeNull();
        msg!.Header.BaseFeeL1.Should().Be(new UInt256(42));
        msg.Header.BlockNumber.Should().Be(100UL);
        msg.Header.Timestamp.Should().Be(1000UL);
        msg.L2Msg.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
    }
}
