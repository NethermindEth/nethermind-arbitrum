// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerDelayedMessageTests
{
    [Test]
    public void StartSequencing_WithDelayedDeposit_ProducesBlockWithDeposit()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c => c.SequencerEnabled = true)
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        L1IncomingMessage depositMsg = TestL1IncomingMessage.CreateEthDepositMessage(
            TestItem.KeccakA, chain.InitialL1BaseFee,
            FullChainSimulationAccounts.AccountA.Address, FullChainSimulationAccounts.AccountB.Address, 1.Ether);

        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages([depositMsg], delayedMsgRead)
            .Should().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        SequencedMsg expectedSequencedMessage = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, depositMsg, delayedMsgRead + 1, [0, 0]);
        StartSequencingResult expectedSequencingResult = new(expectedSequencedMessage, 0);
        result.Should().BeEquivalentTo(expectedSequencingResult);

        chain.BlockTree.Head!.Header.Nonce.Should().Be(delayedMsgRead + 1);

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public void StartSequencing_WithRetryable_ProducesBlockWithRetryable()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c => c.SequencerEnabled = true)
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        L1IncomingMessage retryableMsg = TestL1IncomingMessage.CreateSubmitRetryableMessage(
            TestItem.KeccakA, chain.InitialL1BaseFee,
            FullChainSimulationAccounts.AccountA.Address, FullChainSimulationAccounts.AccountB.Address,
            FullChainSimulationAccounts.AccountC.Address,
            2.Ether, 1.Ether, 10.GWei, 1_000_000, 1.GWei);

        ulong delayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages([retryableMsg], delayedMsgRead)
            .Should().RequestSucceed();

        StartSequencingEnvironment env = StartSequencingEnvironment.FromNowUtc();
        StartSequencingResult result = chain.NitroExecutionRpcModule
            .nitroexecution_startSequencing(env.L1BLockNumber, env.L1Timestamp, env.L2Timestamp)
            .ShouldAsync().RequestSucceed()
            .And.Subject.Data;

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        SequencedMsg expectedSequencedMessage = TestSequencer.ExpectedSequencedMessage(
            chain.BlockTree.Head!.Header, retryableMsg, delayedMsgRead + 1, [0, 0]);
        StartSequencingResult expectedSequencingResult = new(expectedSequencedMessage, 0);
        result.Should().BeEquivalentTo(expectedSequencingResult);

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).Should().RequestSucceed();
    }

    [Test]
    public void NextDelayedMessageNumber_EmptyQueue_ReadsFromHeader()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c => c.SequencerEnabled = true)
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        chain.NitroExecutionRpcModule.nitroexecution_nextDelayedMessageNumber()
            .Should().RequestSucceed()
            .And.Subject.Data
            .Should().Be(chain.BlockTree.Head!.Header.Nonce);
    }
}
