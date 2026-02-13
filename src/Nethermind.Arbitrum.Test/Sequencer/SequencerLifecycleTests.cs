// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerLifecycleTests
{
    [Test]
    public async Task Pause_WhileActive_StopsBlockProduction()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue _, out TransactionQueue txQueue);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountA.Address);

        Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Wei());
        Task<Exception?> txResult = txQueue.EnqueueAsync(tx, CancellationToken.None);

        engine.Pause();

        ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().BeNull();
        result.Data.WaitDurationMs.Should().Be(50);
    }

    [Test]
    public async Task Activate_AfterPause_ResumesBlockProduction()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue _, out TransactionQueue txQueue);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountA.Address);

        engine.Pause();
        engine.Activate();

        Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Wei());
        Task<Exception?> txResult = txQueue.EnqueueAsync(tx, CancellationToken.None);

        ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().NotBeNull("block should be produced after reactivation");

        engine.EndSequencing(null);
        Exception? err = await txResult.WaitAsync(TimeSpan.FromSeconds(5));
        err.Should().BeNull();
    }

    [Test]
    public void ForwardTo_SameUrl_NoOp()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.ForwardTo("https://backup.example.com");
        state.Mode.Should().Be(SequencerMode.Forwarding);
        state.Forwarder.Should().NotBeNull();
        state.Forwarder!.PrimaryTarget.Should().Be("https://backup.example.com");

        TransactionForwarder? firstForwarder = state.Forwarder;

        state.ForwardTo("https://backup.example.com");
        state.Mode.Should().Be(SequencerMode.Forwarding);
        state.Forwarder.Should().BeSameAs(firstForwarder, "same URL should not create a new forwarder");
    }

    [Test]
    public void ForwardTo_ThenActivate_StopsForwarding()
    {
        SequencerState state = new(LimboLogs.Instance);

        state.ForwardTo("https://backup.example.com");
        state.Mode.Should().Be(SequencerMode.Forwarding);
        state.Forwarder.Should().NotBeNull();

        state.Activate();
        state.Mode.Should().Be(SequencerMode.Active);
        state.Forwarder.Should().BeNull("forwarder should be disabled and cleared on activate");
    }

    [Test]
    public async Task StartSequencing_WhilePaused_ReturnsNullWith50msWait()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out _, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        engine.Pause();

        ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().BeNull();
        result.Data.WaitDurationMs.Should().Be(50);
    }

    [Test]
    public async Task ForwardTo_WithUrl_ForwardsTransactions()
    {
        using HttpListener listener = new();
        string prefix = "http://localhost:19876/";
        listener.Prefixes.Add(prefix);
        listener.Start();

        bool transactionReceived = false;

        Task serverTask = Task.Run(async () =>
        {
            HttpListenerContext ctx = await listener.GetContextAsync();
            using StreamReader reader = new(ctx.Request.InputStream);
            string body = await reader.ReadToEndAsync();

            using JsonDocument doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("method").GetString().Should().Be("eth_sendRawTransaction");
            transactionReceived = true;

            byte[] responseBytes = Encoding.UTF8.GetBytes(
                """{"jsonrpc":"2.0","id":1,"result":"0x0000000000000000000000000000000000000000000000000000000000000001"}""");
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = responseBytes.Length;
            await ctx.Response.OutputStream.WriteAsync(responseBytes);
            ctx.Response.Close();
        });

        try
        {
            TransactionForwarder forwarder = new(prefix, LimboLogs.Instance);

            Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Wei());

            Exception? error = await forwarder.ForwardTransactionAsync(tx, CancellationToken.None);

            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

            error.Should().BeNull("transaction should forward successfully");
            transactionReceived.Should().BeTrue("server should have received the forwarded transaction");

            forwarder.Dispose();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    public async Task TransactionForwarder_Disabled_ReturnsError()
    {
        TransactionForwarder forwarder = new("http://localhost:19999/", LimboLogs.Instance);
        forwarder.Disable();

        Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Wei());
        Exception? error = await forwarder.ForwardTransactionAsync(tx, CancellationToken.None);

        error.Should().NotBeNull();
        error!.Message.Should().Contain("not available");

        forwarder.Dispose();
    }

    [Test]
    public async Task HandleInactive_ForwardsAndRequeues_OnNoSequencer()
    {
        using HttpListener listener = new();
        string prefix = "http://localhost:19877/";
        listener.Prefixes.Add(prefix);
        listener.Start();

        Task serverTask = Task.Run(async () =>
        {
            HttpListenerContext ctx = await listener.GetContextAsync();
            using StreamReader reader = new(ctx.Request.InputStream);
            await reader.ReadToEndAsync();

            byte[] responseBytes = Encoding.UTF8.GetBytes(
                """{"jsonrpc":"2.0","id":1,"error":{"code":-32000,"message":"sequencer temporarily not available"}}""");
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = responseBytes.Length;
            await ctx.Response.OutputStream.WriteAsync(responseBytes);
            ctx.Response.Close();
        });

        try
        {
            using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
            ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(
                chain, out DelayedMessageQueue _, out TransactionQueue txQueue, useForwarder: prefix);

            ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
                FullChainSimulationInitMessage.CreateDigestInitMessage(92));
            genesisResult.Result.Should().Be(Result.Success);

            Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Wei());
            Task<Exception?> txResultTask = txQueue.EnqueueAsync(tx, CancellationToken.None);

            ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

            result.Result.Should().Be(Result.Success);
            result.Data.SequencedMsg.Should().BeNull("no block should be produced while forwarding");
            result.Data.WaitDurationMs.Should().Be(50);

            // NoSequencerException causes re-queue rather than completion
            txResultTask.IsCompleted.Should().BeFalse("tx should be re-queued, not completed");

            engine.Activate();

            await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountA.Address);

            ResultWrapper<StartSequencingResult> activeResult = await engine.StartSequencingAsync();
            activeResult.Result.Should().Be(Result.Success);
            activeResult.Data.SequencedMsg.Should().NotBeNull("requeued tx should be sequenced after activation");

            engine.EndSequencing(null);
            Exception? txErr = await txResultTask.WaitAsync(TimeSpan.FromSeconds(5));
            txErr.Should().BeNull();
        }
        finally
        {
            listener.Stop();
        }
    }
}
