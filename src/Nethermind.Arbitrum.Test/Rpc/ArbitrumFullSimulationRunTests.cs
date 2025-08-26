// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Rpc;

public class ArbitrumFullSimulationRunTests : ArbitrumRpcModuleTests
{
    private const string DigestInitMessage = " {\"initialL1BaseFee\":\"0x7\",\"serializedChainConfig\":\"eyJjaGFpbklkIjo0MTIzNDYsImhvbWVzdGVhZEJsb2NrIjowLCJkYW9Gb3JrU3VwcG9ydCI6dHJ1ZSwiZWlwMTUwQmxvY2siOjAsImVpcDE1MEhhc2giOiIweDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAiLCJlaXAxNTVCbG9jayI6MCwiZWlwMTU4QmxvY2siOjAsImJ5emFudGl1bUJsb2NrIjowLCJjb25zdGFudGlub3BsZUJsb2NrIjowLCJwZXRlcnNidXJnQmxvY2siOjAsImlzdGFuYnVsQmxvY2siOjAsIm11aXJHbGFjaWVyQmxvY2siOjAsImJlcmxpbkJsb2NrIjowLCJsb25kb25CbG9jayI6MCwiY2xpcXVlIjp7InBlcmlvZCI6MCwiZXBvY2giOjB9LCJhcmJpdHJ1bSI6eyJFbmFibGVBcmJPUyI6dHJ1ZSwiQWxsb3dEZWJ1Z1ByZWNvbXBpbGVzIjp0cnVlLCJEYXRhQXZhaWxhYmlsaXR5Q29tbWl0dGVlIjpmYWxzZSwiSW5pdGlhbEFyYk9TVmVyc2lvbiI6MzIsIkluaXRpYWxDaGFpbk93bmVyIjoiMHg1RTE0OTdkRDFmMDhDODdiMmQ4RkUyM2U5QUFCNmMxRGU4MzNEOTI3IiwiR2VuZXNpc0Jsb2NrTnVtIjowfX0=\"}";

    private const string Message1 = "{\"number\":\"0x1\",\"message\":{\"message\":{\"header\":{\"kind\":9,\"sender\":\"0x52eda38e4e9cbcc76047c4ed427db4457e0c0a9b\",\"blockNumber\":\"0x1e5\",\"timestamp\":\"0x688898b8\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000001\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"AAAAAAAAAAAAAAAAP6sYRiLcGbYQk0m5SBFJO/KkU2IAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAI4byb8EAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAjmgvhS3ZIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAJkgAAAAAAAAAAAAAAADGc2IvLXbzVdZzVgy4jB32CIcUAgAAAAAAAAAAAAAAAMZzYi8tdvNV1nNWDLiMHfYIhxQCAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUggAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO5rKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"},\"delayedMessagesRead\":\"0x2\"}}\n";
    private const string Message2 = "{\"number\":\"0x2\",\"message\":{\"message\":{\"header\":{\"kind\":3,\"sender\":\"0x52eda38e4e9cbcc76047c4ed427db4457e0c0a9b\",\"blockNumber\":\"0x1e5\",\"timestamp\":\"0x688898b8\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000002\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"BPilgIUXSHboAIMBhqCAgLhTYEWAYA5gADmAYADzUP5//////////////////////////////////////////+A2AWAAgWAggjeANYKCNPWAFRVgOVeBgv1bgIJSUFBQYBRgDPMboCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIioCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIi\"},\"delayedMessagesRead\":\"0x3\"}}\n";
    private const string Message3 = "{\"number\":\"0x3\",\"message\":{\"message\":{\"header\":{\"kind\":9,\"sender\":\"0x52eda38e4e9cbcc76047c4ed427db4457e0c0a9b\",\"blockNumber\":\"0x1e5\",\"timestamp\":\"0x688898b8\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000003\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"AAAAAAAAAAAAAAAAu24CS5z/rLlHpxmR44ZoGxzRR30AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAV8CE5fPAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABX055XfjZIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAJkgAAAAAAAAAAAAAAADGc2IvLXbzVdZzVgy4jB32CIcUAgAAAAAAAAAAAAAAAMZzYi8tdvNV1nNWDLiMHfYIhxQCAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUggAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO5rKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"},\"delayedMessagesRead\":\"0x4\"}}\n";
    private const string Message4 = "{\"number\":\"0x4\",\"message\":{\"message\":{\"header\":{\"kind\":3,\"sender\":\"0x52eda38e4e9cbcc76047c4ed427db4457e0c0a9b\",\"blockNumber\":\"0x1e5\",\"timestamp\":\"0x688898b8\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000004\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"BPkBbICFF0h26ACDA8TYgIC5AVRggGBAUjSAFWEAEFdgAID9W1BhATSAYQAgYAA5YADz/mCAYEBSNIAVYA9XYACA/VtQYAQ2EGAoV2AANWDgHIBjSvY/AhRgLVdbYACA/Vtgz2AEgDYDYECBEBVgQVdgAID9W4EBkGAggQGBNWQBAAAAAIERFWBbV2AAgP1bggGDYCCCAREVYGxXYACA/VuANZBgIAGRhGABgwKEARFkAQAAAACDERcVYI1XYACA/VuRkICAYB8BYCCAkQQCYCABYEBRkIEBYEBSgJOSkZCBgVJgIAGDg4CChDdgAJIBkZCRUlCSlVBQkTWSUGDrkVBQVltgQIBRYAFgAWCgGwOQkhaCUlGQgZADYCABkPNbYACBg1FgIIUBYAD1k5JQUFBW/qJkaXBmc1giEiBrRPioLLaxVr/MPcaq3W307v0gS8kopDl/0V2s9tUyBWRzb2xjQwAGAgAzG4MkcACCJHA=\"},\"delayedMessagesRead\":\"0x5\"}}\n";
    private const string Message5 = "{\"number\":\"0x5\",\"message\":{\"message\":{\"header\":{\"kind\":9,\"sender\":\"0x52eda38e4e9cbcc76047c4ed427db4457e0c0a9b\",\"blockNumber\":\"0x1e5\",\"timestamp\":\"0x688898b8\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000005\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"AAAAAAAAAAAAAAAATI0pChs2isRyjYOp6DIfw68rObEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAI4byb8EAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAjmgvhS3ZIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAJkgAAAAAAAAAAAAAAADGc2IvLXbzVdZzVgy4jB32CIcUAgAAAAAAAAAAAAAAAMZzYi8tdvNV1nNWDLiMHfYIhxQCAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUggAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO5rKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"},\"delayedMessagesRead\":\"0x6\"}}";
    private const string Message6 = "{\"number\":\"0x6\",\"message\":{\"message\":{\"header\":{\"kind\":3,\"sender\":\"0x52eda38e4e9cbcc76047c4ed427db4457e0c0a9b\",\"blockNumber\":\"0x1e5\",\"timestamp\":\"0x688898b8\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000006\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"BPh+gIUXSHboAIMBhqCAgK1gH4BgDmAAOYBgAPNQ/mAANoGCN4A2gjT1gBUVYBRXgYL9W4CCUlBQYBRgDPMboCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIioCIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIiIi\"},\"delayedMessagesRead\":\"0x7\"}}";
    private const string Message7 = "{\"number\":\"0x7\",\"message\":{\"message\":{\"header\":{\"kind\":9,\"sender\":\"0x52eda38e4e9cbcc76047c4ed427db4457e0c0a9b\",\"blockNumber\":\"0x1e5\",\"timestamp\":\"0x688898b8\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000007\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"AAAAAAAAAAAAAAAAqZAHfDIFy9+GHhf6Uy7rBpzp/5YAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABHDeTfggAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEcSqzvknZIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAJkgAAAAAAAAAAAAAAADGc2IvLXbzVdZzVgy4jB32CIcUAgAAAAAAAAAAAAAAAMZzYi8tdvNV1nNWDLiMHfYIhxQCAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUggAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAO5rKAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"},\"delayedMessagesRead\":\"0x8\"}}";
    private const string Message8 = "{\"number\":\"0x8\",\"message\":{\"message\":{\"header\":{\"kind\":3,\"sender\":\"0x52eda38e4e9cbcc76047c4ed427db4457e0c0a9b\",\"blockNumber\":\"0x1e5\",\"timestamp\":\"0x688898b8\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000008\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"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\"},\"delayedMessagesRead\":\"0x9\"}}";
    private const string Message9 = "{\"number\":\"0x9\",\"message\":{\"message\":{\"header\":{\"kind\":12,\"sender\":\"0x502fae7d46d88f08fc2f8ed27fcb2ab183eb3e1f\",\"blockNumber\":\"0x23b\",\"timestamp\":\"0x6888990e\",\"requestId\":\"0x0000000000000000000000000000000000000000000000000000000000000009\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"Px6ufUbYjwj8L47Sf8sqsYPrLQ4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAFS0Cx+FK9oAAAA==\"},\"delayedMessagesRead\":\"0xa\"}}\n";
    private const string MessageA = "{\"number\":\"0xa\",\"message\":{\"message\":{\"header\":{\"kind\":13,\"sender\":\"0xe2148ee53c0755215df69b2616e552154edc584f\",\"blockNumber\":\"0xc9a\",\"timestamp\":\"0x6888a37c\",\"requestId\":\"0x000000000000000000000000000000000000000000000000000000000000000a\",\"baseFeeL1\":\"0x7\"},\"l2Msg\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGiIo3ziFI7lPAdVIV32myYW5VIVTtxYT4VZtckeRZVUIgWKYb9Lck3arICx1iWODiO+QoCq2L1GAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABwAAAAAAAAAA\",\"batchGasCost\":\"0x9e0e\"},\"delayedMessagesRead\":\"0xb\"}}";

    private static readonly DigestInitMessage? _initMessage = JsonSerializer.Deserialize<DigestInitMessage>(DigestInitMessage, EthereumJsonSerializer.JsonOptions);

    private static readonly DigestMessageParameters[] _messages =
    [
        JsonSerializer.Deserialize<DigestMessageParameters>(Message1, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(Message2, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(Message3, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(Message4, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(Message5, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(Message6, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(Message7, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(Message8, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(Message9, EthereumJsonSerializer.JsonOptions)!,
        JsonSerializer.Deserialize<DigestMessageParameters>(MessageA, EthereumJsonSerializer.JsonOptions)!,
    ];

    [Test]
    public async Task FullRpcSequenceFromLog_ExecutesAllCallsSuccessfully()
    {
        ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();

        ResultWrapper<MessageResult> initResult = chain.ArbitrumRpcModule.DigestInitMessage(_initMessage);
        initResult.Result.ResultType.Should().Be(ResultType.Success);
        initResult.Data.BlockHash.Should().NotBeNull();
        initResult.Data.SendRoot.Should().Be(Hash256.Zero);

        // TODO: message 9 and 10 contains transactions that are not implemented
        // Error: Unknown transaction type 100
        // Data: txType: 100

        // for (int i = 0; i < 10; i++)
        Hash256 lastBlockHash = initResult.Data.BlockHash;
        for (int i = 0; i < 8; i++)
        {

            ResultWrapper<MessageResult> digestResult = await chain.ArbitrumRpcModule.DigestMessage(_messages[i]);
            digestResult.Result.ResultType.Should().Be(ResultType.Success,
                $"DigestMessage {i + 1} should succeed");
            // TODO: need to find a way to remove this
            digestResult.Data.BlockHash.Should().NotBeNull($"Block hash should be set for message {i + 1}");
            lastBlockHash = digestResult.Data.BlockHash;
        }

        ResultWrapper<ulong> headResult = await chain.ArbitrumRpcModule.HeadMessageNumber();
        headResult.Result.ResultType.Should().Be(ResultType.Success);
        headResult.Data.Should().BeGreaterOrEqualTo(0UL);

        SetFinalityDataParams finalityParams = new SetFinalityDataParams
        {
            SafeFinalityData = new RpcFinalityData
            {
                MsgIdx = 8UL, // Using 8 since we processed 8 messages
                BlockHash = lastBlockHash // Using the last processed block hash
            },
            FinalizedFinalityData = new RpcFinalityData
            {
                MsgIdx = 8UL,
                BlockHash = lastBlockHash
            }
        };

        ResultWrapper<string> finalityResult = chain.ArbitrumRpcModule.SetFinalityData(finalityParams);
        finalityResult.Result.ResultType.Should().Be(ResultType.Success);
        finalityResult.Data.Should().Be("OK");

        ResultWrapper<string> markFeedResult = chain.ArbitrumRpcModule.MarkFeedStart(8UL);
        markFeedResult.Result.ResultType.Should().Be(ResultType.Success);
        markFeedResult.Data.Should().Be("OK");

        ResultWrapper<MessageResult> resultAtPosResult = await chain.ArbitrumRpcModule.ResultAtPos(7UL); // Last processed message
        resultAtPosResult.Result.ResultType.Should().Be(ResultType.Success);
        resultAtPosResult.Data.BlockHash.Should().NotBeNull();
        resultAtPosResult.Data.SendRoot.Should().NotBeNull();

        ResultWrapper<ulong> finalHeadResult = await chain.ArbitrumRpcModule.HeadMessageNumber();
        finalHeadResult.Result.ResultType.Should().Be(ResultType.Success);
        finalHeadResult.Data.Should().Be(8UL, "Should have processed 8 messages");
    }
}
