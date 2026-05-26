// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text;
using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Rpc;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public class ArbitrumConsensusClientTests
{
    [Test]
    public async Task GetBlockMetadataAsync_DisabledClient_ReturnsNull()
    {
        DisabledArbitrumConsensusClient client = new(LimboLogs.Instance);

        byte[]? result = await client.GetBlockMetadataAsync(100);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetBlockMetadataAsync_BlockBeforeGenesis_ReturnsNull()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using ArbitrumConsensusClient client = CreateClient(genesisBlockNum: 100, consensusUrl: server.Uri);

        byte[]? result = await client.GetBlockMetadataAsync(99);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetBlockMetadataAsync_RpcCallFails_ReturnsNull()
    {
        using ArbitrumConsensusClient client = CreateClient(consensusUrl: "http://127.0.0.1:1/");

        byte[]? result = await client.GetBlockMetadataAsync(10);

        result.Should().BeNull();
    }

    [Test]
    public async Task GetBlockMetadataAsync_ConsensusNodeAvailable_ReturnsMetadata()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using ArbitrumConsensusClient client = CreateClient(consensusUrl: server.Uri);

        Task handleTask = server.Handle(body =>
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("method").GetString()
                .Should().Be("nitroconsensus_blockMetadataAtMessageIndex");

            long id = doc.RootElement.GetProperty("id").GetInt64();
            return Encoding.UTF8.GetBytes($$"""{"jsonrpc":"2.0","id":{{id}},"result":"0x0005"}""");
        });

        byte[]? result = await client.GetBlockMetadataAsync(10);
        await handleTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().BeEquivalentTo(new byte[] { 0x00, 0x05 });
    }

    [Test]
    public async Task GetBlockMetadataAsync_BlockAfterGenesis_SendsCorrectMsgIdx()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using ArbitrumConsensusClient client = CreateClient(genesisBlockNum: 100, consensusUrl: server.Uri);

        string? receivedParams = null;
        Task handleTask = server.Handle(body =>
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            receivedParams = doc.RootElement.GetProperty("params")[0].GetRawText();

            long id = doc.RootElement.GetProperty("id").GetInt64();
            return Encoding.UTF8.GetBytes($$"""{"jsonrpc":"2.0","id":{{id}},"result":"0x00"}""");
        });

        await client.GetBlockMetadataAsync(110); // block 110 - genesis 100 = msgIdx 10
        await handleTask.WaitAsync(TimeSpan.FromSeconds(5));

        // msgIdx can be serialized as number or hex string depending on serializer
        receivedParams.Should().NotBeNull();
        ulong msgIdx = receivedParams!.StartsWith('"')
            ? Convert.ToUInt64(receivedParams.Trim('"').TrimStart('0', 'x'), 16)
            : ulong.Parse(receivedParams);
        msgIdx.Should().Be(10);
    }

    [Test]
    public async Task GetBlockMetadataAsync_NullResult_ReturnsNull()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using ArbitrumConsensusClient client = CreateClient(consensusUrl: server.Uri);

        Task handleTask = server.Handle(body =>
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            long id = doc.RootElement.GetProperty("id").GetInt64();
            return Encoding.UTF8.GetBytes($$"""{"jsonrpc":"2.0","id":{{id}},"result":null}""");
        });

        byte[]? result = await client.GetBlockMetadataAsync(10);
        await handleTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().BeNull();
    }

    private static ArbitrumConsensusClient CreateClient(ulong genesisBlockNum = 0, string consensusUrl = "http://localhost:1/")
    {
        ArbitrumConfig config = new() { ConsensusNodeRpcUrl = consensusUrl };
        ArbitrumSpecHelper specHelper = new(new ArbitrumChainSpecEngineParameters { GenesisBlockNum = genesisBlockNum }, new DisabledArbOsVersionOverride());
        return new ArbitrumConsensusClient(config, specHelper, new EthereumJsonSerializer(), LimboLogs.Instance);
    }
}
