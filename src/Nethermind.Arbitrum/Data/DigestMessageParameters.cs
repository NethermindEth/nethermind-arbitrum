// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Data.Converters;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Data;

public record DigestMessageParameters(
    [property: JsonPropertyName("index")] ulong Index, // L2 block index, to convert to L2 number use genesis.blockNumber + blockIndex
    [property: JsonPropertyName("message")] MessageWithMetadata Message,
    [property: JsonPropertyName("messageForPrefetch")] MessageWithMetadata? MessageForPrefetch
);

public record MessageWithMetadata(
    [property: JsonPropertyName("message")] L1IncomingMessage Message,
    [property: JsonPropertyName("delayedMessagesRead"), JsonConverter(typeof(GoCompatULongConverter))] ulong DelayedMessagesRead
);

public record L1IncomingMessage(
    [property: JsonPropertyName("header")] L1IncomingMessageHeader Header,
    [property: JsonPropertyName("l2Msg"), JsonConverter(typeof(Base64Converter))] byte[]? L2Msg,
    [property: JsonPropertyName("batchGasCost"), JsonConverter(typeof(GoCompatNullableULongConverter))] ulong? BatchGasCost,
    [property: JsonPropertyName("batchDataTokens")] BatchDataStats? BatchDataStats
);

public record BatchDataStats(
    [property: JsonPropertyName("length"), JsonConverter(typeof(GoCompatULongConverter))] ulong Length,
    [property: JsonPropertyName("nonzeros"), JsonConverter(typeof(GoCompatULongConverter))] ulong NonZeros
);

public record L1IncomingMessageHeader(
    [property: JsonPropertyName("kind")] ArbitrumL1MessageKind Kind,
    [property: JsonPropertyName("sender")] Address Sender,
    [property: JsonPropertyName("blockNumber"), JsonConverter(typeof(GoCompatULongConverter))] ulong BlockNumber, // L1 block number
    [property: JsonPropertyName("timestamp"), JsonConverter(typeof(GoCompatULongConverter))] ulong Timestamp,
    [property: JsonPropertyName("requestId")] Hash256? RequestId,
    [property: JsonPropertyName("baseFeeL1"), JsonConverter(typeof(GoCompatNullableUInt256Converter))] UInt256? BaseFeeL1
);

public record DigestInitMessage(
    [property: JsonPropertyName("initialL1BaseFee")] UInt256 InitialL1BaseFee,
    [property: JsonPropertyName("serializedChainConfig"), JsonConverter(typeof(Base64Converter))] byte[]? SerializedChainConfig
);

public record MessageWithMetadataAndBlockInfo(
    [property: JsonPropertyName("message")] MessageWithMetadata MessageWithMeta,
    [property: JsonPropertyName("blockHash")] Hash256 BlockHash,
    [property: JsonPropertyName("blockMetadata"), JsonConverter(typeof(Base64Converter))] byte[] BlockMetadata
);

public record ReorgParameters(
    [property: JsonPropertyName("number")] ulong MsgIdxOfFirstMsgToAdd,
    [property: JsonPropertyName("message")] MessageWithMetadataAndBlockInfo[] NewMessages,
    [property: JsonPropertyName("messageForPrefetch")] MessageWithMetadata[] OldMessages
);

public record RecordBlockCreationParameters(
    [property: JsonPropertyName("index")] ulong Index,
    [property: JsonPropertyName("message")] MessageWithMetadata Message,
    [property: JsonPropertyName("wasmTargets")] string[] WasmTargets
);

public record PrepareForRecordParameters(
    [property: JsonPropertyName("start")] ulong Start,
    [property: JsonPropertyName("end")] ulong End
);

public record EnqueueDelayedMessagesParams(
    [property: JsonPropertyName("messages")] L1IncomingMessage[] Messages,
    [property: JsonPropertyName("firstMsgIdx")] ulong FirstMsgIdx
);

public record StartSequencingResult(
    [property: JsonPropertyName("sequencedMsg")] SequencedMsg? SequencedMsg,
    [property: JsonPropertyName("waitDurationMs")] long WaitDurationMs
);

public record SequencedMsg(
    [property: JsonPropertyName("msgIdx")] ulong MsgIdx,
    [property: JsonPropertyName("msgWithMeta")] MessageWithMetadata MsgWithMeta,
    [property: JsonPropertyName("msgResult")] MessageResultForRpc? MsgResult,
    [property: JsonPropertyName("blockMetadata"), JsonConverter(typeof(Base64Converter))] byte[] BlockMetadata
);

public record EndSequencingParams(
    [property: JsonPropertyName("error")] string? Error
);

public record StartSequencingParams(
    [property: JsonPropertyName("l1BlockNumber"), JsonConverter(typeof(GoCompatULongConverter))] ulong L1BlockNumber,
    [property: JsonPropertyName("l1Timestamp"), JsonConverter(typeof(GoCompatULongConverter))] ulong L1Timestamp,
    [property: JsonPropertyName("timestamp"), JsonConverter(typeof(GoCompatULongConverter))] ulong Timestamp
);
