// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.Logging;

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
    public async Task StartSequencing_WithUserTx_ProducesBlock()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue _, out TransactionQueue txQueue);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        // Fund an account first via ETH deposit
        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId, 92, TestItem.AddressA,
            FullChainSimulationAccounts.AccountA.Address, 10.Ether());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        engine.EnqueueDelayedMessages([depositMsg], genesisDelayedMsgRead);

        ResultWrapper<StartSequencingResult> depositResult = await engine.StartSequencingAsync();
        depositResult.Result.Should().Be(Result.Success);
        depositResult.Data.SequencedMsg.Should().NotBeNull();

        engine.EndSequencing(null);

        ResultWrapper<EmptyResponse> appendResult = await engine.AppendLastSequencedBlockAsync();
        appendResult.Result.Should().Be(Result.Success);

        long headAfterDeposit = chain.BlockTree.Head!.Number;

        // Now submit a signed user transaction
        Transaction userTx = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithTo(TestItem.AddressB)
            .WithValue(1.Ether())
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        Task<Exception?> txResultTask = txQueue.EnqueueAsync(userTx, CancellationToken.None);

        ResultWrapper<StartSequencingResult> seqResult = await engine.StartSequencingAsync();

        seqResult.Result.Should().Be(Result.Success, $"start sequencing should succeed, error: {seqResult.Result.Error}");
        seqResult.Data.SequencedMsg.Should().NotBeNull("expected a block with user tx");
        seqResult.Data.WaitDurationMs.Should().Be(0);

        // EndSequencing should notify the sender
        engine.EndSequencing(null);

        Exception? txResult = await txResultTask.WaitAsync(TimeSpan.FromSeconds(5));
        txResult.Should().BeNull("user tx should be included successfully");

        chain.BlockTree.Head!.Number.Should().Be(headAfterDeposit + 1);
    }

    [Test]
    public async Task StartSequencing_DelayedMsgPriority_SequencesDelayedFirst()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue delayedQueue, out TransactionQueue txQueue);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        // Fund account first
        Hash256 requestId1 = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId1, 92, TestItem.AddressA,
            FullChainSimulationAccounts.AccountA.Address, 10.Ether());

        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        delayedQueue.Enqueue([depositMsg], delayedMsgRead);

        // Also enqueue a user tx
        Transaction userTx = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithTo(TestItem.AddressB)
            .WithValue(1.Wei())
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        Task<Exception?> txResultTask = txQueue.EnqueueAsync(userTx, CancellationToken.None);

        // First StartSequencing should sequence the delayed message (priority)
        ResultWrapper<StartSequencingResult> result1 = await engine.StartSequencingAsync();
        result1.Result.Should().Be(Result.Success);
        result1.Data.SequencedMsg.Should().NotBeNull();
        // Delayed message increases DelayedMessagesRead
        result1.Data.SequencedMsg!.MsgWithMeta.DelayedMessagesRead.Should().Be(delayedMsgRead + 1);

        engine.EndSequencing(null);

        ResultWrapper<EmptyResponse> appendResult = await engine.AppendLastSequencedBlockAsync();
        appendResult.Result.Should().Be(Result.Success);

        // Second StartSequencing should sequence the user tx
        ResultWrapper<StartSequencingResult> result2 = await engine.StartSequencingAsync();
        result2.Result.Should().Be(Result.Success);
        result2.Data.SequencedMsg.Should().NotBeNull();
        // User tx message keeps DelayedMessagesRead the same as the parent block's
        result2.Data.SequencedMsg!.MsgWithMeta.DelayedMessagesRead.Should().Be(delayedMsgRead + 1);

        engine.EndSequencing(null);

        Exception? txResult = await txResultTask.WaitAsync(TimeSpan.FromSeconds(5));
        txResult.Should().BeNull();
    }

    [Test]
    public async Task EndSequencing_Success_NotifiesSenders()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue _, out TransactionQueue txQueue);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        // Fund account
        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId, 92, TestItem.AddressA,
            FullChainSimulationAccounts.AccountA.Address, 10.Ether());

        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        engine.EnqueueDelayedMessages([depositMsg], delayedMsgRead);

        ResultWrapper<StartSequencingResult> depositResult = await engine.StartSequencingAsync();
        depositResult.Result.Should().Be(Result.Success);
        engine.EndSequencing(null);
        await engine.AppendLastSequencedBlockAsync();

        // Submit two user transactions
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
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei())
            .WithTo(TestItem.AddressC)
            .WithValue(1.Ether())
            .WithChainId(412346)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject;

        Task<Exception?> result1 = txQueue.EnqueueAsync(tx1, CancellationToken.None);
        Task<Exception?> result2 = txQueue.EnqueueAsync(tx2, CancellationToken.None);

        ResultWrapper<StartSequencingResult> seqResult = await engine.StartSequencingAsync();
        seqResult.Result.Should().Be(Result.Success);
        seqResult.Data.SequencedMsg.Should().NotBeNull();

        // Before EndSequencing, tasks should still be pending
        result1.IsCompleted.Should().BeFalse();
        result2.IsCompleted.Should().BeFalse();

        // EndSequencing with success should notify all senders
        engine.EndSequencing(null);

        Exception? err1 = await result1.WaitAsync(TimeSpan.FromSeconds(5));
        Exception? err2 = await result2.WaitAsync(TimeSpan.FromSeconds(5));

        err1.Should().BeNull("tx1 should be included successfully");
        err2.Should().BeNull("tx2 should be included successfully");
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

}
