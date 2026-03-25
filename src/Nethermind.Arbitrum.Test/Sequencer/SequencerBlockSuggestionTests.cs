// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerBlockSuggestionTests
{
    [Test]
    public void StartSequencing_WithUserTx_HeadUnchangedUntilAppend()
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

        long headBeforeSequencing = chain.BlockTree.Head!.Number;

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
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing);

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing + 1);

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();
    }

    [Test]
    public void EndSequencing_WithNonRetryError_NextCycleSucceeds()
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
        chain.PrefundAccount(FullChainSimulationAccounts.AccountB.Address, 10.Ether).Should().RequestSucceed();

        long headBeforeSequencing = chain.BlockTree.Head!.Number;

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes).ShouldAsync().RequestSucceed();

        StartSequencingEnvironment env1 = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env1.L1BLockNumber, env1.L1Timestamp, env1.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing("block validation failed");

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing);

        byte[] tx2Bytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountB.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountC.Address)
            .WithValue(2.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountB)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(tx2Bytes).ShouldAsync().RequestSucceed();

        StartSequencingEnvironment env2 = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result2 = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env2.L1BLockNumber, env2.L1Timestamp, env2.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result2.SequencedMsg.Should().NotBeNull("second cycle should produce a block");

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing + 1);
        chain.WorldStateAccessor.GetBalance(FullChainSimulationAccounts.AccountC.Address).Should().Be(2.Ether);
    }

    [Test]
    public async Task EndSequencing_WithRetryError_TransactionsRequeued()
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

        long headBeforeSequencing = chain.BlockTree.Head!.Number;

        byte[] txBytes = Rlp.Encode(Build.A.Transaction
            .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        Task sendTask = Task.Run(() => chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes));
        await Task.Delay(50);

        StartSequencingEnvironment env1 = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env1.L1BLockNumber, env1.L1Timestamp, env1.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing, "head should not advance after Start");

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing("retry sequencer").ShouldAsync().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing, "head should not advance after discard");

        await Task.Delay(50);
        sendTask.IsCompleted.Should().BeFalse("retry-sequencer should re-queue txs, not return error");

        StartSequencingEnvironment env2 = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env2.L1BLockNumber, env2.L1Timestamp, env2.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void StartSequencing_ThreeConsecutiveCycles_ChainContinuityMaintained()
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

        long headBeforeSequencing = chain.BlockTree.Head!.Number;

        for (int i = 0; i < 3; i++)
        {
            byte[] txBytes = Rlp.Encode(Build.A.Transaction
                .WithNonce(chain.WorldStateAccessor.GetNonce(FullChainSimulationAccounts.AccountA.Address))
                .WithGasLimit(21000)
                .WithGasPrice(1.GWei)
                .WithTo(FullChainSimulationAccounts.AccountB.Address)
                .WithValue(1.Ether)
                .WithChainId(chain.BlockTree.ChainId)
                .SignedAndResolved(FullChainSimulationAccounts.AccountA)
                .TestObject).Bytes;

            chain.ArbitrumEthRpcModule.eth_sendRawTransaction(txBytes).ShouldAsync().RequestSucceed();

            StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
            chain.NitroExecutionRpcModule
                .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
                .ShouldAsync().RequestSucceed();

            chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing + i,
                $"head should not advance after Start (iteration {i})");

            chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

            chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing + i + 1,
                $"head should advance after Append (iteration {i})");

            chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();
        }

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing + 3);

        Block current = chain.BlockTree.Head!;
        for (int i = 0; i < 3; i++)
        {
            Block? parent = chain.BlockTree.FindBlock(current.ParentHash!, BlockTreeLookupOptions.RequireCanonical);
            parent.Should().NotBeNull($"parent of block {current.Number} should be findable");
            current = parent!;
        }
    }

    [Test]
    public void StartSequencing_WithDelayedMessage_DeferredUntilAppend()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c => c.SequencerEnabled = true)
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        long headBeforeSequencing = chain.BlockTree.Head!.Number;
        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;

        L1IncomingMessage depositMsg = TestL1IncomingMessage.CreateEthDepositMessage(
            TestItem.KeccakA, chain.InitialL1BaseFee,
            FullChainSimulationAccounts.AccountA.Address, FullChainSimulationAccounts.AccountB.Address, 5.Ether);

        chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages([depositMsg], delayedMsgRead)
            .Should().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing);

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBeforeSequencing + 1);
        chain.BlockTree.Head!.Header.Nonce.Should().Be(delayedMsgRead + 1);

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        chain.WorldStateAccessor.GetBalance(FullChainSimulationAccounts.AccountB.Address).Should().Be(5.Ether);
    }

    [Test]
    public void AppendLastSequencedBlock_WithEthTransfer_WorldStateCorrect()
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
            .WithValue(3.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(transferTxBytes).ShouldAsync().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed();

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        chain.WorldStateAccessor.GetBalance(FullChainSimulationAccounts.AccountB.Address).Should().Be(3.Ether);
    }

    [Test]
    public void AppendLastSequencedBlock_WithUserTx_BlockFindableOnlyAfterAppend()
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

        long expectedBlockNumber = chain.BlockTree.Head!.Number + 1;

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

        chain.BlockTree.FindBlock(expectedBlockNumber, BlockTreeLookupOptions.RequireCanonical)
            .Should().BeNull("block should not be canonical before Append");

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        Block? appendedBlock = chain.BlockTree.FindBlock(expectedBlockNumber, BlockTreeLookupOptions.RequireCanonical);
        appendedBlock.Should().NotBeNull("block should be canonical after Append");
        appendedBlock!.Hash!.Should().Be(result.SequencedMsg!.MsgResult!.Hash!);

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();
    }

    [Test]
    public void DigestMessage_ViaFollowerPath_SuggestsImmediately()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c => c.SequencerEnabled = true)
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        long headBefore = chain.BlockTree.Head!.Number;

        chain.PrefundAccount(FullChainSimulationAccounts.AccountA.Address, 5.Ether).Should().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBefore + 1);
    }

    [Test]
    public void EndSequencing_NoBlockProduced_HeadUnchanged()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c =>
            {
                c.SequencerEnabled = true;
                c.SequencerAwaitTxResult = false;
            })
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        long headBefore = chain.BlockTree.Head!.Number;

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result.SequencedMsg.Should().BeNull();

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        chain.BlockTree.Head!.Number.Should().Be(headBefore);
    }
}
