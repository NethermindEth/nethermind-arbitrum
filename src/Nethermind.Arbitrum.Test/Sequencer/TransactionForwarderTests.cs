// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text;
using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class TransactionForwarderTests
{
    private static readonly byte[] SampleRlp = [0x01, 0x02, 0x03];
    private static readonly Hash256 SampleTxHash = TestItem.KeccakA;

    [Test]
    public async Task ForwardTransactionAsync_WithOptions_SendsConditionalRpcMethod()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using TransactionForwarder forwarder = new(server.Uri, LimboLogs.Instance);
        ConditionalOptions options = new() { TimestampMax = 999 };

        string? capturedBody = null;
        Task handleTask = server.Handle(body =>
        {
            capturedBody = body;
            return Encoding.UTF8.GetBytes(SuccessJson());
        });

        await forwarder.ForwardTransactionAsync(SampleRlp, options, SampleTxHash, CancellationToken.None);
        await handleTask;

        capturedBody.Should().NotBeNull();
        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        doc.RootElement.GetProperty("method").GetString().Should().Be("eth_sendRawTransactionConditional");
    }

    [Test]
    public async Task ForwardTransactionAsync_WithKnownAccounts_IncludesOptionsInParams()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using TransactionForwarder forwarder = new(server.Uri, LimboLogs.Instance);
        ConditionalOptions options = new()
        {
            BlockNumberMin = 10,
            BlockNumberMax = 20,
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [TestItem.AddressA] = new() { RootHash = TestItem.KeccakB }
            }
        };

        string? capturedBody = null;
        Task handleTask = server.Handle(body =>
        {
            capturedBody = body;
            return Encoding.UTF8.GetBytes(SuccessJson());
        });

        ResultWrapper<Hash256> result = await forwarder.ForwardTransactionAsync(SampleRlp, options, SampleTxHash, CancellationToken.None);
        await handleTask;

        result.Should().RequestSucceed();
        capturedBody.Should().NotBeNull();
        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        JsonElement paramsArr = doc.RootElement.GetProperty("params");
        paramsArr.GetArrayLength().Should().Be(2);

        paramsArr[0].GetString().Should().Be(SampleRlp.ToHexString(withZeroX: true));

        JsonElement optionsEl = paramsArr[1];
        optionsEl.GetProperty("blockNumberMin").GetString().Should().Be("0xa");
        optionsEl.GetProperty("blockNumberMax").GetString().Should().Be("0x14");
        optionsEl.GetProperty("knownAccounts").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Test]
    public async Task ForwardTransactionAsync_SuccessResponse_ReturnsTxHash()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using TransactionForwarder forwarder = new(server.Uri, LimboLogs.Instance);

        Task handleTask = server.Handle(_ => Encoding.UTF8.GetBytes(SuccessJson()));

        ResultWrapper<Hash256> result = await forwarder.ForwardTransactionAsync(
            SampleRlp, new ConditionalOptions(), SampleTxHash, CancellationToken.None);
        await handleTask;

        result.Should().RequestSucceed();
        result.Data.Should().Be(SampleTxHash);
    }

    [Test]
    public async Task ForwardTransactionAsync_ErrorResponse_ReturnsFailure()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using TransactionForwarder forwarder = new(server.Uri, LimboLogs.Instance);

        Task handleTask = server.Handle(_ => Encoding.UTF8.GetBytes(ErrorJson("condition not met")));

        ResultWrapper<Hash256> result = await forwarder.ForwardTransactionAsync(
            SampleRlp, new ConditionalOptions(), SampleTxHash, CancellationToken.None);
        await handleTask;

        result.Should().RequestFail("condition not met");
    }

    [Test]
    public async Task ForwardTransactionAsync_WithoutOptions_SendsRegularRpcMethod()
    {
        using TestHttpServer server = TestHttpServer.Start();
        using TransactionForwarder forwarder = new(server.Uri, LimboLogs.Instance);

        string? capturedBody = null;
        Task handleTask = server.Handle(body =>
        {
            capturedBody = body;
            return Encoding.UTF8.GetBytes(SuccessJson());
        });

        await forwarder.ForwardTransactionAsync(SampleRlp, SampleTxHash, CancellationToken.None);
        await handleTask;

        capturedBody.Should().NotBeNull();
        using JsonDocument doc = JsonDocument.Parse(capturedBody!);
        doc.RootElement.GetProperty("method").GetString().Should().Be("eth_sendRawTransaction");
        doc.RootElement.GetProperty("params").GetArrayLength().Should().Be(1);
    }

    [Test]
    public async Task ForwardTransaction_WithUrl_ForwardsTransactions()
    {
        TestHttpServer remoteSequencer = TestHttpServer.Start();

        bool transactionReceived = false;
        Task responseTask = remoteSequencer.Handle(body =>
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("method").GetString().Should().Be("eth_sendRawTransaction");
            transactionReceived = true;

            return """{"jsonrpc":"2.0","id":1,"result":"0x0000000000000000000000000000000000000000000000000000000000000001"}"""u8.ToArray();
        });

        using TransactionForwarder forwarder = new(remoteSequencer.Uri, LimboLogs.Instance);

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        Hash256 txHash = TestItem.KeccakA;
        ResultWrapper<Hash256> result = await forwarder.ForwardTransactionAsync(txBytes, txHash, CancellationToken.None);

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().RequestSucceed("transaction should forward successfully");
        result.Data.Should().Be(txHash);
        transactionReceived.Should().BeTrue("server should have received the forwarded transaction");
    }

    [Test]
    public async Task ForwardTransaction_Disabled_ReturnsError()
    {
        TransactionForwarder forwarder = new("http://localhost:19999/", LimboLogs.Instance);
        forwarder.Disable();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        ResultWrapper<Hash256> result = await forwarder.ForwardTransactionAsync(txBytes, TestItem.KeccakA, CancellationToken.None);

        result.Should().RequestFail("not available");

        forwarder.Dispose();
    }

    private static string SuccessJson()
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, result = "0x" + new string('0', 64) });

    private static string ErrorJson(string message)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, error = new { code = -32000, message } });
}
