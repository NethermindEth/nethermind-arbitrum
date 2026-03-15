// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Sequencer.Timeboost;
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
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(FullChainSimulationChainSpecProvider.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        MessageWithMetadata assembled = L2MessageAssembler.AssembleFromSignedTransactions(
            [Rlp.Encode(tx).Bytes], 0, timestamp, 1);

        IReadOnlyList<Transaction> parsed = NitroL2MessageParser.ParseTransactions(
            assembled.Message, FullChainSimulationChainSpecProvider.ChainId, 20, LimboLogs.Instance.GetClassLogger());

        parsed.Should().HaveCount(1);
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
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(FullChainSimulationChainSpecProvider.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        Transaction tx2 = Build.A.Transaction
            .WithNonce(1)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(2.Ether)
            .WithChainId(FullChainSimulationChainSpecProvider.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        MessageWithMetadata assembled = L2MessageAssembler.AssembleFromSignedTransactions(
            [Rlp.Encode(tx1).Bytes, Rlp.Encode(tx2).Bytes], 0, timestamp, 1);

        IReadOnlyList<Transaction> parsed = NitroL2MessageParser.ParseTransactions(
            assembled.Message, FullChainSimulationChainSpecProvider.ChainId, 20, LimboLogs.Instance.GetClassLogger());

        parsed.Should().HaveCount(2);
        parsed[0].To.Should().Be(tx1.To!);
        parsed[0].Nonce.Should().Be(tx1.Nonce);
        parsed[0].Value.Should().Be(tx1.Value);
        parsed[1].To.Should().Be(tx2.To!);
        parsed[1].Nonce.Should().Be(tx2.Nonce);
        parsed[1].Value.Should().Be(tx2.Value);
    }

    [Test]
    public async Task TransactionQueue_Full_BlocksUntilTimeout()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 1 }, new DisabledExpressLaneTracker(), TimeProvider.System);

        Transaction tx1 = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        Transaction tx2 = Build.A.Transaction
            .WithNonce(1)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        ResultWrapper<Hash256> result1 = await queue.EnqueueAsync(TxQueueItem.CreateRegular(tx1));
        result1.Should().RequestSucceed();

        ResultWrapper<Hash256> result2 = await queue.EnqueueAsync(TxQueueItem.CreateRegular(tx2, TimeSpan.FromMilliseconds(100)));
        result2.Should().RequestFail("timeout");
    }

    [Test]
    public async Task TransactionQueue_OversizedTx_RejectsImmediately()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 10, SequencerMaxTxDataSize = 100 }, new DisabledExpressLaneTracker(), TimeProvider.System);

        Transaction tx = Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithData(new byte[200])
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        ResultWrapper<Hash256> result = await queue.EnqueueAsync(TxQueueItem.CreateRegular(tx));

        result.Should().RequestFail("exceeds maximum");
    }

    [Test]
    public void NonceCache_Hit_SkipsStateRead()
    {
        NonceCache cache = new(16);
        BlockHeader header = Build.A.BlockHeader
            .WithNumber(1)
            .WithStateRoot(TestItem.KeccakA)
            .TestObject;

        cache.Update(header, FullChainSimulationAccounts.AccountA.Address, 42);

        ulong nonce = cache.Get(header, null!, FullChainSimulationAccounts.AccountA.Address);

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

        cache.Update(header1, FullChainSimulationAccounts.AccountA.Address, 10);

        Block block1 = Build.A.Block
            .WithHeader(header1)
            .TestObject;
        cache.Finalize(block1);

        BlockHeader header2 = Build.A.BlockHeader
            .WithNumber(2)
            .WithHash(TestItem.KeccakC)
            .WithParentHash(TestItem.KeccakD)
            .TestObject;

        cache.Update(header2, FullChainSimulationAccounts.AccountA.Address, 20);
        ulong nonce = cache.Get(header2, null!, FullChainSimulationAccounts.AccountA.Address);

        nonce.Should().Be(20);
    }

    [Test]
    public void SendRawTransaction_WithUserTx_ProducesBlock()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        byte[] transferTxBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(transferTxBytes).ShouldAsync().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedSequencedMessage = TestSequencer.ExpectedSequencedMessage(chain.BlockTree.Head!.Header, env, [transferTxBytes], [0, 0]);
        StartSequencingResult expectedSequencingResult = new(expectedSequencedMessage, 0);

        result.Should().BeEquivalentTo(expectedSequencingResult);

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();

        chain.WorldStateAccessor.GetBalance(FullChainSimulationAccounts.AccountB.Address).Should().Be(1.Ether);
    }

    [Test]
    public void SendRawTransaction_DelayedMsgPriority_SequencesDelayedFirst()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        // Enqueue a delayed message (ETH deposit to AccountB)
        L1IncomingMessage depositMsg = TestL1IncomingMessage.CreateEthDepositMessage(
            TestItem.KeccakA, chain.InitialL1BaseFee, FullChainSimulationAccounts.AccountA.Address,
            FullChainSimulationAccounts.AccountB.Address, 5.Ether);

        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages([depositMsg], delayedMsgRead)
            .Should().RequestSucceed();

        // Submit user transaction
        byte[] transferTxBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(transferTxBytes).ShouldAsync().RequestSucceed();

        // First sequencing: delayed message has priority
        StartSequencingEnvironment env1 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result1 = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env1.L1BLockNumber, env1.L1Timestamp, env1.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedSequencedMessage1 = TestSequencer.ExpectedSequencedMessage(chain.BlockTree.Head!.Header, depositMsg, 1, [0, 0]);
        StartSequencingResult expectedSequencingResult1 = new(expectedSequencedMessage1, 0);
        result1.Should().BeEquivalentTo(expectedSequencingResult1);

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();

        // Second sequencing: user transaction
        StartSequencingEnvironment env2 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result2 = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env2.L1BLockNumber, env2.L1Timestamp, env2.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedSequencedMessage2 = TestSequencer.ExpectedSequencedMessage(chain.BlockTree.Head!.Header, env2, [transferTxBytes], [0, 0]);
        StartSequencingResult expectedSequencingResult2 = new(expectedSequencedMessage2, 0);
        result2.Should().BeEquivalentTo(expectedSequencingResult2);

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task SendRawTransaction_EndSequencingSuccess_NotifiesSenders()
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
        chain.PrefundAccount(FullChainSimulationAccounts.AccountB.Address, 10.Ether).Should().RequestSucceed();

        byte[] tx1Bytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        byte[] tx2Bytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountB.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject).Bytes;

        // With SequencerAwaitTxResult=true, eth_sendRawTransaction blocks until sequenced
        Task<ResultWrapper<Hash256>> sendTask1 = Task.Run(() => chain.ArbitrumEthRpcModule.eth_sendRawTransaction(tx1Bytes));
        await Task.Delay(10); // Ensure tx1 is enqueued before tx2 for deterministic ordering
        Task<ResultWrapper<Hash256>> sendTask2 = Task.Run(() => chain.ArbitrumEthRpcModule.eth_sendRawTransaction(tx2Bytes));
        await Task.Delay(50);

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedSequencedMessage = TestSequencer.ExpectedSequencedMessage(chain.BlockTree.Head!.Header, env, [tx1Bytes, tx2Bytes], [0, 0]);
        StartSequencingResult expectedSequencingResult = new(expectedSequencedMessage, 0);
        result.Should().BeEquivalentTo(expectedSequencingResult);

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();

        (await sendTask1.WaitAsync(TimeSpan.FromSeconds(5))).Should().RequestSucceed();
        (await sendTask2.WaitAsync(TimeSpan.FromSeconds(5))).Should().RequestSucceed();
    }

    [Test]
    public void StartSequencing_NoUserTxsOrDelayed_ReturnsWaitDuration()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result.SequencedMsg.Should().BeNull();
        result.WaitDurationMs.Should().BeGreaterThan(0);
    }

    [Test]
    public void SendRawTransaction_QueueFull_TimesOutWhenNoSpace()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain
            .CreateDefault(configureArbitrum: c =>
            {
                c.SequencerEnabled = true;
                c.SequencerMaxTxQueueSize = 1;
                c.SequencerQueueTimeoutMs = 100;
            });

        byte[] tx1Bytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(FullChainSimulationChainSpecProvider.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        byte[] tx2Bytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(1)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(FullChainSimulationChainSpecProvider.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        // First tx fills the queue (capacity=1)
        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(tx1Bytes).ShouldAsync().RequestSucceed();

        // Second tx blocks then times out
        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(tx2Bytes).ShouldAsync().RequestFail("timeout");
    }

    [Test]
    public void SendRawTransaction_OversizedTransaction_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.SequencerMaxTxDataSize = 100;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithData(new byte[200])
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes).ShouldAsync().RequestFail("exceeds maximum");
    }

    [Test]
    public void SendRawTransaction_InvalidRlp_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        byte[] invalidBytes = [0xFF, 0xFE, 0xFD];
        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(invalidBytes).ShouldAsync().RequestFail("Invalid RLP");
    }

    [Test]
    public async Task SendRawTransaction_ForwardingMode_ForwardsToBackup()
    {
        using TestRemoteSequencer remoteSequencer = TestRemoteSequencer.Start();

        bool transactionReceived = false;
        Task responseTask = remoteSequencer.Handle(body =>
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("method").GetString().Should().Be("eth_sendRawTransaction");
            transactionReceived = true;

            return """{"jsonrpc":"2.0","id":1,"result":"0x0000000000000000000000000000000000000000000000000000000000000001"}"""u8.ToArray();
        });

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.NitroExecutionRpcModule.nitroexecution_forwardTo(remoteSequencer.Uri).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes).ShouldAsync().RequestSucceed();

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        transactionReceived.Should().BeTrue("server should have received the forwarded transaction");
    }

    [Test]
    public async Task SendRawTransaction_ForwardingNoSequencer_ReturnsError()
    {
        using TestRemoteSequencer remoteSequencer = TestRemoteSequencer.Start();
        Task responseTask = remoteSequencer
            .Handle(_ => """{"jsonrpc":"2.0","id":1,"error":{"code":-32000,"message":"sequencer temporarily not available"}}"""u8.ToArray());

        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.NitroExecutionRpcModule.nitroexecution_forwardTo(remoteSequencer.Uri).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes).ShouldAsync().RequestFail("not available");

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void SendRawTransaction_PausedMode_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.NitroExecutionRpcModule.nitroexecution_pause().Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes).ShouldAsync().RequestFail("not available");
    }

    [Test]
    public void SendRawTransaction_SequencerDisabledAndNoForwarderDefined_ReturnsError()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes).ShouldAsync().RequestFail("not available");
    }
}
