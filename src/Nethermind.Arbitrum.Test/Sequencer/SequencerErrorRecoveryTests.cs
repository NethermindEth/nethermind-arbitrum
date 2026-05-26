// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerErrorRecoveryTests
{
    [Test]
    public async Task EndSequencing_NonRetryError_ReturnsErrorToCallers()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = true;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        Task<ResultWrapper<Hash256>> sendTask = Task.Run(
            () => chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes));
        await Task.Delay(50);

        // Start sequencing — creates block
        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        // End with a non-retry error — should return error to caller, not re-queue
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing("block validation failed").ShouldAsync().RequestSucceed();

        // Caller should receive an error
        ResultWrapper<Hash256> sendResult = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
        sendResult.Should().RequestFail("validation failed");
    }

    [Test]
    public async Task EndSequencing_RetryErrorNoBackup_RequeuesInsteadOfReturningError()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = true;
                c.SequencerQueueTimeoutMs = 10000;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        Task<ResultWrapper<Hash256>> sendTask = Task.Run(
            () => chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes));
        await Task.Delay(50);

        // StartSequencing — creates and commits block
        StartSequencingEnvironment env1 = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env1.L1BLockNumber, env1.L1Timestamp, env1.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        // EndSequencing with retry-sequencer — no forwarder, so re-queue (NOT error-return)
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing("retry sequencer").ShouldAsync().RequestSucceed();

        // Key assertion: tx was re-queued, NOT error-returned immediately
        await Task.Delay(50);
        sendTask.IsCompleted.Should().BeFalse(
            "retry-sequencer error should re-queue txs, not return error immediately");

        // Second StartSequencing picks up re-queued tx; nonce check rejects it
        // (block from first pass was already committed, so nonce is now stale)
        StartSequencingEnvironment env2 = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env2.L1BLockNumber, env2.L1Timestamp, env2.L2Timestamp)
            .ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        // Caller gets nonce error (from re-queue + nonce check), not "retry sequencer"
        ResultWrapper<Hash256> sendResult = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
        sendResult.Should().RequestFail("Nonce too low");
    }

    [Test]
    public async Task EndSequencing_RetryErrorWithBackup_ForwardsToBackup()
    {
        using TestHttpServer remoteSequencer = TestHttpServer.Start();

        int forwardCount = 0;
        // Handle one forwarded tx
        Task responseTask = remoteSequencer.Handle(body =>
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("method").GetString().Should().Be("eth_sendRawTransaction");
            Interlocked.Increment(ref forwardCount);
            return """{"jsonrpc":"2.0","id":1,"result":"0x0000000000000000000000000000000000000000000000000000000000000001"}"""u8.ToArray();
        });

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = true;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        Task<ResultWrapper<Hash256>> sendTask = Task.Run(
            () => chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes));
        await Task.Delay(50);

        // Start sequencing — creates block
        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        // Set up forwarding to backup, then end with retry error
        chain.NitroExecutionRpcModule.nitroexecution_forwardTo(remoteSequencer.Uri).Should().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing("retry sequencer").ShouldAsync().RequestSucceed();

        // Wait for the forwarding to complete
        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        forwardCount.Should().Be(1, "tx should have been forwarded to backup");

        // Caller should receive success (forwarded)
        ResultWrapper<Hash256> sendResult = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
        sendResult.Should().RequestSucceed();
    }

    [Test]
    public void NonceFailureEviction_NoBackup_ReturnsError()
    {
        // Without a forwarding callback, eviction returns error directly
        NonceFailureCache cache = new(2, TimeSpan.FromMilliseconds(50));

        Transaction tx = Build.A.Transaction
            .WithNonce(5)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;
        TxQueueItem item = TxQueueItem.CreateRegular(tx);

        cache.Add(FullChainSimulationAccounts.AccountA.Address, 5, item);

        // Wait for expiry
        Thread.Sleep(100);
        cache.EvictExpired();

        Exception? error = item.ResultChannel.Task.GetAwaiter().GetResult();
        error.Should().NotBeNull();
        error.Should().BeOfType<InvalidOperationException>();
        error!.Message.Should().Contain("expired");
    }

    [Test]
    public async Task NonceFailureEviction_WithCallback_CallsCallback()
    {
        TxQueueItem? evictedItem = null;
        string? evictedMessage = null;

        NonceFailureCache cache = new(1, TimeSpan.FromSeconds(10), onEvict: (item, msg) =>
        {
            evictedItem = item;
            evictedMessage = msg;
            // Simulate forwarding success
            item.ReturnResult(null);
        });

        Transaction tx1 = Build.A.Transaction
            .WithNonce(5)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;
        TxQueueItem item1 = TxQueueItem.CreateRegular(tx1);
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 5, item1);

        // Add another item — capacity is 1, so item1 should be evicted via callback
        Transaction tx2 = Build.A.Transaction
            .WithNonce(6)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;
        TxQueueItem item2 = TxQueueItem.CreateRegular(tx2);
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 6, item2);

        evictedItem.Should().BeSameAs(item1, "first item should have been evicted");
        evictedMessage.Should().Contain("overflow");

        // Evicted item was handled by callback (returned success)
        Exception? result = await item1.ResultChannel.Task.WaitAsync(TimeSpan.FromSeconds(1));
        result.Should().BeNull("callback returned success");
    }

    [Test]
    public void NonceFailureEviction_CancelledToken_ReturnsCancellation()
    {
        TxQueueItem? evictedItem = null;

        NonceFailureCache cache = new(1, TimeSpan.FromSeconds(10), onEvict: (item, msg) =>
        {
            evictedItem = item;
            item.ReturnResult(null); // This shouldn't be called
        });

        Transaction tx1 = Build.A.Transaction
            .WithNonce(5)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;
        TxQueueItem item1 = TxQueueItem.CreateRegular(tx1, TimeSpan.Zero);
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 5, item1);

        // Add another item — capacity is 1, so item1 should be evicted
        Transaction tx2 = Build.A.Transaction
            .WithNonce(6)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;
        TxQueueItem item2 = TxQueueItem.CreateRegular(tx2);
        cache.Add(FullChainSimulationAccounts.AccountA.Address, 6, item2);

        // Cancelled token should bypass callback and return OperationCanceledException
        evictedItem.Should().BeNull("callback should not be called for cancelled tokens");

        Exception? result = item1.ResultChannel.Task.GetAwaiter().GetResult();
        result.Should().BeOfType<OperationCanceledException>();
    }
}
