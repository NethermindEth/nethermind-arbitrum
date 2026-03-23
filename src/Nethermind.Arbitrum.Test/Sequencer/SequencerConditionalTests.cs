// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerConditionalTests
{
    [Test]
    public async Task SendTx_ConditionalTx_AcceptedWhenConditionsMet()
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

        Transaction tx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        ConditionalOptions options = new()
        {
            BlockNumberMin = 0,
            BlockNumberMax = 100,
            TimestampMin = 0,
        };

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem item = TxQueueItem.CreateRegular(tx, options: options);
        await transactionQueue.EnqueueAsync(item);

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        SequencedMsg expectedMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env, [Rlp.Encode(tx).Bytes], [0, 0]);
        result.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();
    }

    [Test]
    public async Task SendTx_ConditionalTx_RejectedWhenBlockNumberOutOfRange()
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

        Transaction tx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        // BlockNumberMax = 0, but FromNowUtc() uses L1 block number = 1 → rejected
        ConditionalOptions options = new() { BlockNumberMax = 0 };

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem item = TxQueueItem.CreateRegular(tx, options: options);
        Task<Exception?> resultTask = item.ResultChannel.Task;
        await transactionQueue.EnqueueAsync(item);

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result.Should().BeEquivalentTo(new StartSequencingResult(null, 250),
            "conditional tx should be evicted, leaving nothing to sequence");

        Exception? error = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));
        error.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("Conditional tx rejected");
    }

    [Test]
    public async Task SendTx_ConditionalTx_RejectedWhenStorageRootMismatch()
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

        Transaction tx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        // AccountA is an EOA with EmptyTreeHash storage root; specifying a random hash should fail
        ConditionalOptions options = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [FullChainSimulationAccounts.AccountA.Address] = new() { RootHash = TestItem.KeccakA }
            }
        };

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem item = TxQueueItem.CreateRegular(tx, options: options);
        Task<Exception?> resultTask = item.ResultChannel.Task;
        await transactionQueue.EnqueueAsync(item);

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result.Should().BeEquivalentTo(new StartSequencingResult(null, 250),
            "storage root mismatch should evict the conditional tx");

        Exception? error = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));
        error.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("Conditional tx rejected");
    }

    [Test]
    public async Task SendTx_ConditionalTx_RejectedWhenSlotValueMismatch()
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

        Transaction tx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        // AccountA is an EOA with no storage; slot 0 is zero, specifying a non-zero value should fail
        ConditionalOptions options = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [FullChainSimulationAccounts.AccountA.Address] = new()
                {
                    SlotValues = new Dictionary<UInt256, Hash256> { [UInt256.Zero] = TestItem.KeccakA }
                }
            }
        };

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem item = TxQueueItem.CreateRegular(tx, options: options);
        Task<Exception?> resultTask = item.ResultChannel.Task;
        await transactionQueue.EnqueueAsync(item);

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result.Should().BeEquivalentTo(new StartSequencingResult(null, 250),
            "slot value mismatch should evict the conditional tx");

        Exception? error = await resultTask.WaitAsync(TimeSpan.FromSeconds(5));
        error.Should().BeOfType<InvalidOperationException>();
        error.Message.Should().Contain("Conditional tx rejected");
    }

    [Test]
    public async Task SendTx_MixedBatch_ConditionalRejectedRegularAccepted()
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

        // Conditional tx from AccountA with failing conditions
        Transaction conditionalTx = Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Wei)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject;

        ConditionalOptions failingOptions = new() { BlockNumberMax = 0 };

        TransactionQueue transactionQueue = chain.Container.Resolve<TransactionQueue>();
        TxQueueItem conditionalItem = TxQueueItem.CreateRegular(conditionalTx, options: failingOptions);
        Task<Exception?> conditionalResultTask = conditionalItem.ResultChannel.Task;
        await transactionQueue.EnqueueAsync(conditionalItem);

        // Regular tx from AccountB via RPC (no conditions)
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

        // Conditional tx should be evicted with error
        Exception? conditionalError = await conditionalResultTask.WaitAsync(TimeSpan.FromSeconds(5));
        conditionalError.Should().BeOfType<InvalidOperationException>();
        conditionalError.Message.Should().Contain("Conditional tx rejected");

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        // Regular tx should be included in the block
        SequencedMsg expectedMsg = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, env, [regularTxBytes], [0, 0]);
        result.Should().BeEquivalentTo(new StartSequencingResult(expectedMsg, 0));

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();
    }
}
