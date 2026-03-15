// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer.Timeboost;

[TestFixture]
public class TimeboostSequencerEngineTests
{
    [Test]
    public async Task AuctionResolutionTx_WithRegularTxPending_SequencedFirst()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();
        chain.PrefundAccount(FullChainSimulationAccounts.AccountB.Address, 10.Ether).Should().RequestSucceed();

        // Regular tx from AccountA — submitted via RPC
        byte[] regularTxBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(regularTxBytes).ShouldAsync().RequestSucceed();

        // Auction resolution tx from AccountB — written directly to the priority queue
        Transaction auctionTx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountB.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject;

        IAuctionResolutionQueue auctionResolutionQueue = chain.Container.Resolve<IAuctionResolutionQueue>();
        await auctionResolutionQueue.WriteAsync(TxQueueItem.CreateRegular(auctionTx));

        // First sequencing: auction queue is drained before the regular queue
        StartSequencingEnvironment env1 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result1 = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env1.L1BLockNumber, env1.L1Timestamp, env1.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedMsg1 = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env1, [Rlp.Encode(auctionTx).Bytes], [0, 0]);
        result1.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg1, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();

        // Second sequencing: regular tx is now picked up
        StartSequencingEnvironment env2 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result2 = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env2.L1BLockNumber, env2.L1Timestamp, env2.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedMsg2 = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env2, [regularTxBytes], [0, 0]);
        result2.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg2, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task TimeboostedTx_Sequenced_IsMarkedInBlockMetadata()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        // Regular tx via RPC: no timeboost bits should be set
        byte[] regularTxBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(regularTxBytes).ShouldAsync().RequestSucceed();

        StartSequencingEnvironment env1 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult regularResult = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env1.L1BLockNumber, env1.L1Timestamp, env1.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedRegularMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env1, [regularTxBytes], [0, 0]);
        regularResult.Should().BeEquivalentTo(new StartSequencingResult(expectedRegularMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();

        // Timeboosted tx: at least one bitmap bit should be set
        ulong headBlock = (ulong)chain.BlockTree.Head!.Number;
        Transaction timeboostedTx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem timeboostedItem = TxQueueItem.CreateTimeboosted(timeboostedTx, blockStamp: headBlock);
        await transactionQueue.EnqueueAsync(timeboostedItem);

        StartSequencingEnvironment env2 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult boostResult = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env2.L1BLockNumber, env2.L1Timestamp, env2.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedBoostMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env2, [Rlp.Encode(timeboostedTx).Bytes], [0, 2]);
        boostResult.Should().BeEquivalentTo(new StartSequencingResult(expectedBoostMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task TimeboostedTx_ExpiredByBlockCount_IsEvictedFromQueue()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostQueueTimeoutInBlocks = 0;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        // Stamp the tx at the current head block; with timeout=0 it expires immediately
        ulong currentHead = (ulong)chain.BlockTree.Head!.Number;
        Transaction tx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem expiredItem = TxQueueItem.CreateTimeboosted(tx, blockStamp: currentHead);
        Task<Exception?> resultTask = expiredItem.ResultChannel.Task;
        await transactionQueue.EnqueueAsync(expiredItem);

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result.Should().BeEquivalentTo(new StartSequencingResult(null, 250),
            "expired timeboosted tx should be evicted, leaving nothing to sequence");

        Exception? error = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));
        error.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("expired");
    }

    [Test]
    public async Task TimeboostedTx_WithinTimeout_SequencedNormally()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        ulong headBlock = (ulong)chain.BlockTree.Head!.Number;
        Transaction tx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem item = TxQueueItem.CreateTimeboosted(tx, blockStamp: headBlock);
        await transactionQueue.EnqueueAsync(item);

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env, [Rlp.Encode(tx).Bytes], [0, 2]);
        result.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task TimeboostedTx_MixedBatch_OnlyExpiredEvicted()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostQueueTimeoutInBlocks = 0;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();
        chain.PrefundAccount(FullChainSimulationAccounts.AccountB.Address, 10.Ether).Should().RequestSucceed();

        // Expired timeboosted tx from AccountA
        ulong currentHead = (ulong)chain.BlockTree.Head!.Number;
        Transaction expiredTx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem expiredItem = TxQueueItem.CreateTimeboosted(expiredTx, blockStamp: currentHead);
        Task<Exception?> expiredResultTask = expiredItem.ResultChannel.Task;
        await transactionQueue.EnqueueAsync(expiredItem);

        // Regular tx from AccountB via RPC
        byte[] regularTxBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountB.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(regularTxBytes).ShouldAsync().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env, [regularTxBytes], [0, 0]);
        result.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg, 0));

        Exception? expiredError = await expiredResultTask.WaitAsync(TimeSpan.FromSeconds(5));
        expiredError.Should().BeOfType<InvalidOperationException>();
        expiredError.Message.Should().Contain("expired");

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task TimeboostedTx_MultipleBoostedInBlock_AllBitsSet()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();
        chain.PrefundAccount(FullChainSimulationAccounts.AccountB.Address, 10.Ether).Should().RequestSucceed();

        ulong headBlock = (ulong)chain.BlockTree.Head!.Number;

        Transaction txA = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        Transaction txB = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountB.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject;

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        await transactionQueue.EnqueueAsync(TxQueueItem.CreateTimeboosted(txA, blockStamp: headBlock));
        await transactionQueue.EnqueueAsync(TxQueueItem.CreateTimeboosted(txB, blockStamp: headBlock));

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        // ArbOS internal tx at index 0, txA at index 1, txB at index 2
        // Bitmap: (1 << 1) | (1 << 2) = 2 + 4 = 6
        SequencedMsg expectedMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env, [Rlp.Encode(txA).Bytes, Rlp.Encode(txB).Bytes], [0, 6]);
        result.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task TimeboostedAndRegular_InSameBlock_OnlyBoostedBitSet()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();
        chain.PrefundAccount(FullChainSimulationAccounts.AccountB.Address, 10.Ether).Should().RequestSucceed();

        // Regular tx from AccountA via RPC
        byte[] regularTxBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(regularTxBytes).ShouldAsync().RequestSucceed();

        // Timeboosted tx from AccountB via queue injection
        ulong headBlock = (ulong)chain.BlockTree.Head!.Number;
        Transaction timeboostedTx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountB.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject;

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        await transactionQueue.EnqueueAsync(TxQueueItem.CreateTimeboosted(timeboostedTx, blockStamp: headBlock));

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        // Both txs in one block: ArbOS internal (index 0), regular (index 1), timeboosted (index 2)
        // Only bit 2 set for timeboosted: (1 << 2) = 4
        SequencedMsg expectedMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env, [regularTxBytes, Rlp.Encode(timeboostedTx).Bytes], [0, 4]);
        result.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task AuctionResolutionTx_WithDelayedMessagePending_DelayedSequencedFirst()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        // Enqueue delayed message
        L1IncomingMessage depositMsg = TestL1IncomingMessage.CreateEthDepositMessage(
            TestItem.KeccakA, chain.InitialL1BaseFee, FullChainSimulationAccounts.AccountA.Address,
            FullChainSimulationAccounts.AccountB.Address, 5.Ether);

        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages([depositMsg], delayedMsgRead)
            .Should().RequestSucceed();

        // Auction resolution tx from AccountA
        Transaction auctionTx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        IAuctionResolutionQueue auctionResolutionQueue = chain.Container.Resolve<IAuctionResolutionQueue>();
        await auctionResolutionQueue.WriteAsync(TxQueueItem.CreateRegular(auctionTx));

        // Round 1: delayed message has highest priority
        StartSequencingEnvironment env1 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result1 = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env1.L1BLockNumber, env1.L1Timestamp, env1.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedMsg1 = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, depositMsg, delayedMsgRead + 1, [0, 0]);
        result1.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg1, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();

        // Round 2: auction resolution tx is next in priority
        StartSequencingEnvironment env2 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result2 = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env2.L1BLockNumber, env2.L1Timestamp, env2.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedMsg2 = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env2, [Rlp.Encode(auctionTx).Bytes], [0, 0]);
        result2.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg2, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task TimeboostedTx_BlockStampZero_NotEvicted()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostQueueTimeoutInBlocks = 0;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        Transaction tx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        // BlockStamp=0 should bypass eviction even with timeout=0
        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem item = TxQueueItem.CreateTimeboosted(tx, blockStamp: 0);
        await transactionQueue.EnqueueAsync(item);

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env, [Rlp.Encode(tx).Bytes], [0, 2]);
        result.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task AuctionResolutionTx_Alone_SequencedSuccessfully()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.TimeboostEnabled = true;
                c.TimeboostAuctionContractAddress = new("0x0000000000000000000000000000000000000001");
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        Transaction auctionTx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        IAuctionResolutionQueue auctionResolutionQueue = chain.Container.Resolve<IAuctionResolutionQueue>();
        await auctionResolutionQueue.WriteAsync(TxQueueItem.CreateRegular(auctionTx));

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        SequencedMsg expectedMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env, [Rlp.Encode(auctionTx).Bytes], [0, 0]);
        result.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }
}
