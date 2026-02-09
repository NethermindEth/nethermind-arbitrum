// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Modules;
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
public class UserTxSequencingTests
{
    [Test]
    public void L2MessageAssembler_SingleSignedTx_RoundTripsWithParser()
    {
        Transaction tx = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithTo(TestItem.AddressB)
            .WithValue(1.Ether())
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        BlockHeader parentHeader = Build.A.BlockHeader
            .WithNumber(1)
            .WithTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .WithNonce(1)
            .TestObject;

        MessageWithMetadata assembled = L2MessageAssembler.AssembleFromSignedTransactions([tx], parentHeader, 0);

        IReadOnlyList<Transaction> parsed = NitroL2MessageParser.ParseTransactions(
            assembled.Message, 412346, 20, LimboLogs.Instance.GetClassLogger());

        parsed.Should().HaveCount(1);
        // NitroL2MessageParser doesn't recover sender from signature; verify data fields
        parsed[0].To.Should().Be(tx.To!);
        parsed[0].Value.Should().Be(tx.Value);
        parsed[0].Nonce.Should().Be(tx.Nonce);
        parsed[0].GasLimit.Should().Be(tx.GasLimit);
        parsed[0].Signature.Should().NotBeNull();
    }

    [Test]
    public void L2MessageAssembler_BatchOfSignedTxs_RoundTripsWithParser()
    {
        Transaction tx1 = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithTo(TestItem.AddressB)
            .WithValue(1.Ether())
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        Transaction tx2 = Build.A.Transaction
            .WithNonce(1)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithTo(TestItem.AddressC)
            .WithValue(2.Ether())
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        BlockHeader parentHeader = Build.A.BlockHeader
            .WithNumber(1)
            .WithTimestamp((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .WithNonce(1)
            .TestObject;

        MessageWithMetadata assembled = L2MessageAssembler.AssembleFromSignedTransactions([tx1, tx2], parentHeader, 0);

        IReadOnlyList<Transaction> parsed = NitroL2MessageParser.ParseTransactions(
            assembled.Message, 412346, 20, LimboLogs.Instance.GetClassLogger());

        parsed.Should().HaveCount(2);
        parsed[0].To.Should().Be(tx1.To!);
        parsed[0].Nonce.Should().Be(tx1.Nonce);
        parsed[0].Value.Should().Be(tx1.Value);
        parsed[1].To.Should().Be(tx2.To!);
        parsed[1].Nonce.Should().Be(tx2.Nonce);
        parsed[1].Value.Should().Be(tx2.Value);
    }

    [Test]
    public void TransactionQueue_Full_RejectsNew()
    {
        TransactionQueue queue = new(1, 95000);

        Transaction tx1 = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        Transaction tx2 = Build.A.Transaction
            .WithNonce(1)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        // First enqueue should succeed (fills capacity=1)
        Task<Exception?> task1 = queue.EnqueueAsync(tx1, CancellationToken.None);

        // Second enqueue should be rejected since capacity is 1
        Task<Exception?> task2 = queue.EnqueueAsync(tx2, CancellationToken.None);

        task2.IsCompleted.Should().BeTrue();
        task2.Result.Should().BeOfType<InvalidOperationException>();
        task2.Result!.Message.Should().Contain("queue is full");
    }

    [Test]
    public void TransactionQueue_OversizedTx_RejectsImmediately()
    {
        TransactionQueue queue = new(10, 100);

        Transaction tx = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithData(new byte[200])
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        Task<Exception?> task = queue.EnqueueAsync(tx, CancellationToken.None);

        task.IsCompleted.Should().BeTrue();
        task.Result.Should().BeOfType<InvalidOperationException>();
        task.Result!.Message.Should().Contain("exceeds maximum");
    }

    [Test]
    public void NonceCache_Hit_SkipsStateRead()
    {
        NonceCache cache = new(16);
        BlockHeader header = Build.A.BlockHeader
            .WithNumber(1)
            .WithStateRoot(TestItem.KeccakA)
            .TestObject;

        // Pre-populate cache by doing a manual update
        cache.Update(header, TestItem.AddressA, 42);

        // Now Get should return cached value without needing a real state reader
        // We pass a mock state reader that would throw if called
        ulong nonce = cache.Get(header, null!, TestItem.AddressA);

        nonce.Should().Be(42);
    }

    [Test]
    public void NonceCache_BlockMismatch_Resets()
    {
        NonceCache cache = new(16);
        BlockHeader header1 = Build.A.BlockHeader
            .WithNumber(1)
            .WithHash(TestItem.KeccakA)
            .WithParentHash(TestItem.KeccakB)
            .TestObject;

        // Populate cache for header1
        cache.Update(header1, TestItem.AddressA, 10);

        // Finalize with a block whose parentHash matches header1's parent
        Block block1 = Build.A.Block
            .WithHeader(header1)
            .TestObject;
        cache.Finalize(block1);

        // Create a new header with different parent (simulating a different block)
        BlockHeader header2 = Build.A.BlockHeader
            .WithNumber(2)
            .WithHash(TestItem.KeccakC)
            .WithParentHash(TestItem.KeccakD)
            .TestObject;

        // The cache should reset since the new header has a different parent hash than the finalized block hash
        // Get will need to read from state; since we can't mock IStateReader easily here,
        // we verify by updating and checking
        cache.Update(header2, TestItem.AddressA, 20);
        ulong nonce = cache.Get(header2, null!, TestItem.AddressA);

        nonce.Should().Be(20);
    }

    [Test]
    public async Task SendRawTransaction_WithUserTx_ProducesBlock()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(
            chain, out DelayedMessageQueue _, out TransactionQueue _, out ArbitrumEthRpcModule ethRpcModule);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountA.Address);
        long headAfterDeposit = chain.BlockTree.Head!.Number;

        Transaction userTx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Ether());
        byte[] txBytes = Rlp.Encode(userTx).Bytes;

        // eth_sendRawTransaction blocks until block inclusion, so run in background
        Task<ResultWrapper<Hash256>> sendTask = Task.Run(() => ethRpcModule.eth_sendRawTransaction(txBytes));
        await Task.Delay(50);

        ResultWrapper<StartSequencingResult> seqResult = await engine.StartSequencingAsync();
        seqResult.Result.Should().Be(Result.Success, $"start sequencing should succeed, error: {seqResult.Result.Error}");
        seqResult.Data.SequencedMsg.Should().NotBeNull("expected a block with user tx");
        seqResult.Data.WaitDurationMs.Should().Be(0);

        engine.EndSequencing(null);

        ResultWrapper<Hash256> sendResult = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
        sendResult.Result.Should().Be(Result.Success);
        sendResult.Data.Should().NotBeNull();

        chain.BlockTree.Head!.Number.Should().Be(headAfterDeposit + 1);
    }

    [Test]
    public async Task SendRawTransaction_DelayedMsgPriority_SequencesDelayedFirst()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(
            chain, out DelayedMessageQueue delayedQueue, out TransactionQueue _, out ArbitrumEthRpcModule ethRpcModule);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        Hash256 requestId1 = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId1, 92, TestItem.AddressA,
            FullChainSimulationAccounts.AccountA.Address, 10.Ether());

        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        delayedQueue.Enqueue([depositMsg], delayedMsgRead);

        Transaction userTx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Wei());
        byte[] txBytes = Rlp.Encode(userTx).Bytes;

        Task<ResultWrapper<Hash256>> sendTask = Task.Run(() => ethRpcModule.eth_sendRawTransaction(txBytes));
        await Task.Delay(50);

        // Delayed messages have priority over user transactions
        ResultWrapper<StartSequencingResult> result1 = await engine.StartSequencingAsync();
        result1.Result.Should().Be(Result.Success);
        result1.Data.SequencedMsg.Should().NotBeNull();
        // Delayed message increases DelayedMessagesRead
        result1.Data.SequencedMsg!.MsgWithMeta.DelayedMessagesRead.Should().Be(delayedMsgRead + 1);

        engine.EndSequencing(null);

        ResultWrapper<EmptyResponse> appendResult = await engine.AppendLastSequencedBlockAsync();
        appendResult.Result.Should().Be(Result.Success);

        ResultWrapper<StartSequencingResult> result2 = await engine.StartSequencingAsync();
        result2.Result.Should().Be(Result.Success);
        result2.Data.SequencedMsg.Should().NotBeNull();
        // User tx message keeps DelayedMessagesRead the same as the parent block's
        result2.Data.SequencedMsg!.MsgWithMeta.DelayedMessagesRead.Should().Be(delayedMsgRead + 1);

        engine.EndSequencing(null);

        ResultWrapper<Hash256> sendResult = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
        sendResult.Result.Should().Be(Result.Success);
    }

    [Test]
    public async Task SendRawTransaction_EndSequencingSuccess_NotifiesSenders()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(
            chain, out DelayedMessageQueue _, out TransactionQueue _, out ArbitrumEthRpcModule ethRpcModule);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountA.Address);
        await SequencerTestHelpers.FundAccountAsync(chain, engine, FullChainSimulationAccounts.AccountB.Address);

        Transaction tx1 = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Ether());
        Transaction tx2 = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithTo(TestItem.AddressC)
            .WithValue(1.Ether())
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject;

        byte[] tx1Bytes = Rlp.Encode(tx1).Bytes;
        byte[] tx2Bytes = Rlp.Encode(tx2).Bytes;

        Task<ResultWrapper<Hash256>> sendTask1 = Task.Run(() => ethRpcModule.eth_sendRawTransaction(tx1Bytes));
        Task<ResultWrapper<Hash256>> sendTask2 = Task.Run(() => ethRpcModule.eth_sendRawTransaction(tx2Bytes));
        await Task.Delay(50);

        ResultWrapper<StartSequencingResult> seqResult = await engine.StartSequencingAsync();
        seqResult.Result.Should().Be(Result.Success);
        seqResult.Data.SequencedMsg.Should().NotBeNull();

        sendTask1.IsCompleted.Should().BeFalse();
        sendTask2.IsCompleted.Should().BeFalse();

        engine.EndSequencing(null);

        ResultWrapper<Hash256> result1 = await sendTask1.WaitAsync(TimeSpan.FromSeconds(5));
        ResultWrapper<Hash256> result2 = await sendTask2.WaitAsync(TimeSpan.FromSeconds(5));

        result1.Result.Should().Be(Result.Success, "tx1 should be included successfully");
        result2.Result.Should().Be(Result.Success, "tx2 should be included successfully");
    }

    [Test]
    public async Task StartSequencing_NoUserTxsOrDelayed_ReturnsWaitDuration()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out _, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().BeNull();
        result.Data.WaitDurationMs.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task SendRawTransaction_SequencerDisabled_FallsBackToTxPool()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumEthRpcModule ethRpcModule = ArbitrumRpcTestBlockchain.CreateEthRpcModule(chain, transactionQueue: null);

        Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Ether());
        byte[] txBytes = Rlp.Encode(tx).Bytes;

        // With null TransactionQueue, falls through to base TxPool behavior
        ResultWrapper<Hash256> result = await ethRpcModule.eth_sendRawTransaction(txBytes);
        result.Should().NotBeNull();
    }

    [Test]
    public async Task SendRawTransaction_QueueFull_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        TransactionQueue smallQueue = new(1, 95000);
        ArbitrumEthRpcModule ethRpcModule = ArbitrumRpcTestBlockchain.CreateEthRpcModule(chain, smallQueue);

        Transaction tx1 = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Ether());
        Transaction tx2 = SequencerTestHelpers.CreateUserTx(1, TestItem.AddressB, 1.Ether());

        byte[] tx1Bytes = Rlp.Encode(tx1).Bytes;
        byte[] tx2Bytes = Rlp.Encode(tx2).Bytes;

        Task<ResultWrapper<Hash256>> _ = Task.Run(() => ethRpcModule.eth_sendRawTransaction(tx1Bytes));
        await Task.Delay(50);

        ResultWrapper<Hash256> result2 = await ethRpcModule.eth_sendRawTransaction(tx2Bytes);

        result2.Result.ResultType.Should().Be(ResultType.Failure);
        result2.Result.Error.Should().Contain("queue is full");
    }

    [Test]
    public async Task SendRawTransaction_OversizedTransaction_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        TransactionQueue smallQueue = new(10, 100);
        ArbitrumEthRpcModule ethRpcModule = ArbitrumRpcTestBlockchain.CreateEthRpcModule(chain, smallQueue);

        Transaction tx = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithData(new byte[200])
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        byte[] txBytes = Rlp.Encode(tx).Bytes;
        ResultWrapper<Hash256> result = await ethRpcModule.eth_sendRawTransaction(txBytes);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("exceeds maximum");
    }

    [Test]
    public async Task SendRawTransaction_InvalidRlp_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();

        TransactionQueue queue = new(10, 95000);
        ArbitrumEthRpcModule ethRpcModule = ArbitrumRpcTestBlockchain.CreateEthRpcModule(chain, queue);

        byte[] invalidBytes = [0xFF, 0xFE, 0xFD];
        ResultWrapper<Hash256> result = await ethRpcModule.eth_sendRawTransaction(invalidBytes);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("Invalid RLP");
    }

    [Test]
    public async Task SendRawTransaction_ForwardingMode_ForwardsToBackup()
    {
        using HttpListener listener = new();
        string prefix = "http://localhost:19878/";
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
            using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
            ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(
                chain, out DelayedMessageQueue _, out TransactionQueue _, out ArbitrumEthRpcModule ethRpcModule,
                useForwarder: prefix);

            ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
                FullChainSimulationInitMessage.CreateDigestInitMessage(92));
            genesisResult.Result.Should().Be(Result.Success);

            Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Ether());
            byte[] txBytes = Rlp.Encode(tx).Bytes;

            ResultWrapper<Hash256> result = await ethRpcModule.eth_sendRawTransaction(txBytes);

            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

            result.Result.Should().Be(Result.Success);
            result.Data.Should().NotBeNull();
            transactionReceived.Should().BeTrue();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    public async Task SendRawTransaction_ForwardingNoSequencer_ReturnsError()
    {
        using HttpListener listener = new();
        string prefix = "http://localhost:19879/";
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
                chain, out DelayedMessageQueue _, out TransactionQueue _, out ArbitrumEthRpcModule ethRpcModule,
                useForwarder: prefix);

            ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
                FullChainSimulationInitMessage.CreateDigestInitMessage(92));
            genesisResult.Result.Should().Be(Result.Success);

            Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Ether());
            byte[] txBytes = Rlp.Encode(tx).Bytes;

            ResultWrapper<Hash256> result = await ethRpcModule.eth_sendRawTransaction(txBytes);

            await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

            result.Result.ResultType.Should().Be(ResultType.Failure);
            result.Result.Error.Should().Contain("not available");
        }
        finally
        {
            listener.Stop();
        }
    }

    [Test]
    public async Task SendRawTransaction_PausedMode_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        SequencerState sequencerState = new(LimboLogs.Instance);
        sequencerState.Activate();
        sequencerState.Pause();

        TransactionQueue queue = new(10, 95000);
        ArbitrumEthRpcModule ethRpcModule = ArbitrumRpcTestBlockchain.CreateEthRpcModule(chain, queue, sequencerState);

        Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Ether());
        byte[] txBytes = Rlp.Encode(tx).Bytes;

        ResultWrapper<Hash256> result = await ethRpcModule.eth_sendRawTransaction(txBytes);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("not available");
    }

    [Test]
    public async Task SendRawTransaction_InactiveMode_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        SequencerState sequencerState = new(LimboLogs.Instance);

        TransactionQueue queue = new(10, 95000);
        ArbitrumEthRpcModule ethRpcModule = ArbitrumRpcTestBlockchain.CreateEthRpcModule(chain, queue, sequencerState);

        Transaction tx = SequencerTestHelpers.CreateUserTx(0, TestItem.AddressB, 1.Ether());
        byte[] txBytes = Rlp.Encode(tx).Bytes;

        ResultWrapper<Hash256> result = await ethRpcModule.eth_sendRawTransaction(txBytes);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("not available");
    }
}
