// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class FeeAndTimeoutTests
{
    [Test]
    public void Tx_GasFeeCapBelowBaseFee_RejectedAtDequeue()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = true;
                c.SequencerQueueTimeoutMs = 5000;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        // Submit a tx with gas price = 1 wei, well below the base fee (92 wei)
        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1) // 1 wei - far below base fee of 92
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        Task<ResultWrapper<Hash256>> sendTask = Task.Run(
            () => chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes));
        Thread.Sleep(50);

        // Trigger sequencing — the GasFeeCap check should reject the tx
        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        // No block should be produced since the only tx was rejected
        result.SequencedMsg.Should().BeNull();

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();

        // The send task should complete with an error
        ResultWrapper<Hash256> sendResult = sendTask.GetAwaiter().GetResult();
        sendResult.Should().RequestFail("baseFee");
    }

    [Test]
    public void Tx_GasFeeCapAboveBaseFee_AcceptedAtDequeue()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
                c.SequencerQueueTimeoutMs = 5000;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 10.Ether).Should().RequestSucceed();

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei) // well above base fee
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes).ShouldAsync().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result.SequencedMsg.Should().NotBeNull("tx with adequate fee cap should be accepted");

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public async Task Tx_QueueFull_BlocksUntilSpace()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 1 }, new DisabledExpressLaneTracker(), TimeProvider.System);

        // Fill the queue
        TxQueueItem item1 = TxQueueItem.CreateRegular(Build.A.Transaction.TestObject);
        await queue.EnqueueAsync(item1);

        // Second enqueue should block, not fail immediately
        TxQueueItem item2 = TxQueueItem.CreateRegular(Build.A.Transaction.WithNonce(1).TestObject, TimeSpan.FromMilliseconds(500));
        Task<ResultWrapper<Hash256>> enqueueTask = queue.EnqueueAsync(item2);

        // Verify the task is not completed immediately (blocking)
        await Task.Delay(50);
        enqueueTask.IsCompleted.Should().BeFalse("should block when queue is full, not reject immediately");

        // Drain the queue to make space
        queue.DrainBatch();

        // The blocked enqueue should now complete
        ResultWrapper<Hash256> result = await enqueueTask;
        result.Should().RequestSucceed();
    }

    [Test]
    public async Task Tx_QueueFull_TimesOut()
    {
        TransactionQueue queue = new(new ArbitrumConfig { SequencerMaxTxQueueSize = 1 }, new DisabledExpressLaneTracker(), TimeProvider.System);

        // Fill the queue
        TxQueueItem item1 = TxQueueItem.CreateRegular(Build.A.Transaction.TestObject);
        await queue.EnqueueAsync(item1);

        // Second enqueue should block then timeout
        TxQueueItem item2 = TxQueueItem.CreateRegular(Build.A.Transaction.WithNonce(1).TestObject, TimeSpan.FromMilliseconds(100));

        ResultWrapper<Hash256> result = await queue.EnqueueAsync(item2);

        result.Should().RequestFail("timeout");
    }
}
