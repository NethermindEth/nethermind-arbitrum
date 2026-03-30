// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerLifecycleTests
{
    [Test]
    public void Pause_WhileActive_StopsBlockProduction()
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
            .WithNonce(0)
            .WithGasLimit(21000)
            .WithGasPrice(1.GWei)
            .WithTo(FullChainSimulationAccounts.AccountB.Address)
            .WithValue(1.Ether)
            .WithChainId(chain.BlockTree.ChainId)
            .SignedAndResolved(FullChainSimulationAccounts.AccountA)
            .TestObject).Bytes;

        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(transferTxBytes).ShouldAsync().RequestSucceed();

        chain.NitroExecutionRpcModule.nitroexecution_pause().Should().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        result.SequencedMsg.Should().BeNull("sequencer is paused, should not produce blocks");
        result.WaitDurationMs.Should().Be(50, "paused sequencer should return inactive wait time");

        chain.WorldStateAccessor.GetBalance(FullChainSimulationAccounts.AccountB.Address).Should().Be(UInt256.Zero);
    }

    [Test]
    public void Activate_AfterPause_ResumesBlockProduction()
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

        chain.NitroExecutionRpcModule.nitroexecution_pause().Should().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_activate().Should().RequestSucceed();

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

        result.SequencedMsg.Should().NotBeNull("block should be produced after reactivation");

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();
        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        chain.WorldStateAccessor.GetBalance(FullChainSimulationAccounts.AccountB.Address).Should().Be(1.Ether);
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
    public async Task HandleInactive_ForwardsAndRequeues_OnNoSequencer()
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

        // Send tx while Active — enqueues to channel
        chain.ArbitrumEthRpcModule.eth_sendRawTransaction(transferTxBytes).ShouldAsync().RequestSucceed();

        // Switch to forwarding — next startSequencing will drain queue and forward
        chain.NitroExecutionRpcModule.nitroexecution_forwardTo(remoteSequencer.Uri).Should().RequestSucceed();

        // StartSequencing in forwarding mode: drains queue, forwards tx, mock rejects, tx requeued
        StartSequencingResult forwardResult = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(0, 0, 0)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        await responseTask.WaitAsync(TimeSpan.FromSeconds(5));

        forwardResult.SequencedMsg.Should().BeNull("no block should be produced while forwarding");

        // Activate and process requeued tx
        chain.NitroExecutionRpcModule.nitroexecution_activate().Should().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult activeResult = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        // Requeued tx should be sequenced after activation.
        SequencedMsg expectedSequencedMessage = TestSequencer.ExpectedSequencedMessage(chain.BlockTree.Head!.Header, env, [transferTxBytes], [0, 0]);
        StartSequencingResult expectedSequencingResult = new(expectedSequencedMessage, 0);

        activeResult.Should().BeEquivalentTo(expectedSequencingResult);

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        chain.WorldStateAccessor.GetBalance(FullChainSimulationAccounts.AccountB.Address).Should().Be(1.Ether);
    }
}
