// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerRpcTests
{
    [Test]
    public void StartSequencing_ValidTransferTransaction_Succeeds()
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

        chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock().ShouldAsync().RequestSucceed();

        SequencedMsg expectedSequencedMessage = TestSequencer.ExpectedSequencedMessage(chain.BlockTree.Head!.Header, env, [transferTxBytes], [0, 0]);
        StartSequencingResult expectedSequencingResult = new(expectedSequencedMessage, 0);

        result.Should().BeEquivalentTo(expectedSequencingResult);

        chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null).ShouldAsync().RequestSucceed();

        chain.WorldStateAccessor.GetBalance(FullChainSimulationAccounts.AccountB.Address).Should().Be(1.Ether);
    }

    [Test]
    public void EnqueueDelayedMessages_ValidDelayedMessage_EnqueuesMessages()
    {
        using ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithArbitrumConfig(c => c.SequencerEnabled = true)
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        L1IncomingMessageHeader header = new(ArbitrumL1MessageKind.EthDeposit, Address.SystemUser, 1, 1000, null, 92);
        L1IncomingMessage[] messages =
        [
            new(header, [1], null, null),
            new(header, [2], null, null),
        ];

        const ulong firstMsgIdx = 5;
        chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages(messages, firstMsgIdx).Should().RequestSucceed();

        chain.NitroExecutionRpcModule.nitroexecution_nextDelayedMessageNumber()
            .Should().RequestSucceed()
            .And.Subject.Data
            .Should().Be(firstMsgIdx + (ulong)messages.Length);
    }

    [Test]
    public void Pause_SequencerIsActive_StopsSequencing()
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

        chain.WorldStateAccessor.GetBalance(TestItem.AddressB).Should().Be(UInt256.Zero);
    }
}
