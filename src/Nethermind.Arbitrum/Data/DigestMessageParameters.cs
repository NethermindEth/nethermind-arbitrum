using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Data;

public record DigestMessageParameters(
    [property: JsonPropertyName("number")] ulong Number, // L2 block index, to convert to L2 number use genesis.blockNumber + blockIndex
    [property: JsonPropertyName("message")] MessageWithMetadata Message,
    [property: JsonPropertyName("messageForPrefetch")] MessageWithMetadata? MessageForPrefetch
);

public record SequenceDelayedMessageParameters(
    [property: JsonPropertyName("delayedSeqNum")] ulong Number,
    [property: JsonPropertyName("message")] L1IncomingMessage Message
);

public record MessageWithMetadataAndBlockInfo(
    [property: JsonPropertyName("message")] MessageWithMetadata MessageWithMeta,
    [property: JsonPropertyName("blockHash")] Hash256 BlockHash,
    [property: JsonPropertyName("blockMetadata")] byte[] BlockMetadata
);

public record MessageWithMetadata(
    [property: JsonPropertyName("message")] L1IncomingMessage Message,
    [property: JsonPropertyName("delayedMessagesRead")] ulong DelayedMessagesRead
);

public record L1IncomingMessage(
    [property: JsonPropertyName("header")] L1IncomingMessageHeader Header,
    [property: JsonPropertyName("l2Msg"), JsonConverter(typeof(Base64Converter))] byte[]? L2Msg,
    [property: JsonPropertyName("batchGasCost")] ulong? BatchGasCost
);

public record L1IncomingMessageHeader(
    [property: JsonPropertyName("kind")] ArbitrumL1MessageKind Kind,
    [property: JsonPropertyName("sender")] Address Sender,
    [property: JsonPropertyName("blockNumber")] ulong BlockNumber, // L1 block number
    [property: JsonPropertyName("timestamp")] ulong Timestamp,
    [property: JsonPropertyName("requestId")] Hash256? RequestId,
    [property: JsonPropertyName("baseFeeL1")] UInt256 BaseFeeL1
);

public record DigestInitMessage(
    [property: JsonPropertyName("initialL1BaseFee")] UInt256 InitialL1BaseFee,
    [property: JsonPropertyName("serializedChainConfig"), JsonConverter(typeof(Base64Converter))] byte[]? SerializedChainConfig
);

public record ReorgParameters(
    [property: JsonPropertyName("number")] ulong MsgIdxOfFirstMsgToAdd,
    [property: JsonPropertyName("message")] MessageWithMetadataAndBlockInfo[] NewMessages,
    [property: JsonPropertyName("messageForPrefetch")] MessageWithMetadata[] OldMessages
);
