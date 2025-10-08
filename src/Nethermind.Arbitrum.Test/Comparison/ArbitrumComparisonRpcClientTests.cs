// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Nethermind.Arbitrum.Modules;
using Nethermind.Core.Crypto;
using Nethermind.Logging;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Modules;

[TestFixture]
public class ArbitrumComparisonRpcClientTests
{
    private Mock<HttpMessageHandler> _httpMessageHandlerMock = null!;
    private HttpClient _httpClient = null!;
    private ArbitrumComparisonRpcClient _client = null!;

    [SetUp]
    public void Setup()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task GetBlockDataAsync_ValidResponse_ReturnsCorrectData()
    {
        string responseJson = @"{
            ""jsonrpc"": ""2.0"",
            ""id"": 1,
            ""result"": {
                ""hash"": ""0x1a74fcd08e44d672d3570095185ef778c4e707ddd05c433efbb6f4437884ab75"",
                ""extraData"": ""0xad3fe9ad8b19bd191d16e4774eeb077d4ab7ef2daa02e4b621300d9c7fdeedc4""
            }
        }";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Create client with mocked HTTP client via reflection
        _client = new ArbitrumComparisonRpcClient("http://test.com", LimboLogs.Instance.GetClassLogger<ArbitrumComparisonRpcClient>());
        var httpClientField = typeof(ArbitrumComparisonRpcClient).GetField("_httpClient", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        httpClientField?.SetValue(_client, _httpClient);

        var (blockHash, sendRoot) = await _client.GetBlockDataAsync(1075);

        blockHash.Should().NotBeNull();
        blockHash.Should().Be(new Hash256("0x1a74fcd08e44d672d3570095185ef778c4e707ddd05c433efbb6f4437884ab75"));
        sendRoot.Should().NotBeNull();
        sendRoot.Should().Be(new Hash256("0xad3fe9ad8b19bd191d16e4774eeb077d4ab7ef2daa02e4b621300d9c7fdeedc4"));
    }
}