// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerRpcTests
{
    [Test]
    public async Task StartSequencing_ViaRpc_ReturnsResult()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        (ArbitrumRpcModule rpcModule, DelayedMessageQueue queue) = SequencerTestHelpers.CreateRpcModuleWithSequencer(chain);

        SequencerTestHelpers.InitGenesis(chain, rpcModule);

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId, 92, TestItem.AddressA, TestItem.AddressB, 1.Ether());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        queue.Enqueue([depositMsg], genesisDelayedMsgRead);

        ResultWrapper<StartSequencingResult> result = await rpcModule.StartSequencing();

        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().NotBeNull();
        result.Data.WaitDurationMs.Should().Be(0);
        result.Data.SequencedMsg!.MsgWithMeta.DelayedMessagesRead.Should().Be(genesisDelayedMsgRead + 1);
        result.Data.SequencedMsg.MsgResult.Should().NotBeNull();
        result.Data.SequencedMsg.MsgResult!.Hash.Should().NotBeNull();
    }

    [Test]
    public void EndSequencing_ViaRpc_Succeeds()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        (ArbitrumRpcModule rpcModule, _) = SequencerTestHelpers.CreateRpcModuleWithSequencer(chain);

        SequencerTestHelpers.InitGenesis(chain, rpcModule);

        ResultWrapper<string> result = rpcModule.EndSequencing(null);

        result.Result.Should().Be(Result.Success);
        result.Data.Should().Be("OK");
    }

    [Test]
    public void EnqueueDelayedMessages_ViaRpc_QueuesMessages()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        (ArbitrumRpcModule rpcModule, _) = SequencerTestHelpers.CreateRpcModuleWithSequencer(chain);

        SequencerTestHelpers.InitGenesis(chain, rpcModule);

        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.EthDeposit, Address.SystemUser, 1, 1000, null, 92);
        L1IncomingMessage[] messages =
        [
            new(header, new byte[] { 1 }, null, null),
            new(header, new byte[] { 2 }, null, null)
        ];

        EnqueueDelayedMessagesParams enqueueParams = new(messages, 5);
        ResultWrapper<string> enqueueResult = rpcModule.EnqueueDelayedMessages(enqueueParams);

        enqueueResult.Result.Should().Be(Result.Success);
        enqueueResult.Data.Should().Be("OK");

        ResultWrapper<ulong> nextResult = rpcModule.NextDelayedMessageNumber();
        nextResult.Result.Should().Be(Result.Success);
        nextResult.Data.Should().Be(7);
    }

    [Test]
    public async Task Pause_ViaRpc_StopsSequencing()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        (ArbitrumRpcModule rpcModule, DelayedMessageQueue queue) = SequencerTestHelpers.CreateRpcModuleWithSequencer(chain);

        SequencerTestHelpers.InitGenesis(chain, rpcModule);

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId, 92, TestItem.AddressA, TestItem.AddressB, 1.Ether());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        queue.Enqueue([depositMsg], genesisDelayedMsgRead);

        ResultWrapper<string> pauseResult = rpcModule.Pause();
        pauseResult.Result.Should().Be(Result.Success);
        pauseResult.Data.Should().Be("OK");

        ResultWrapper<StartSequencingResult> result = await rpcModule.StartSequencing();
        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().BeNull("sequencer is paused, should not produce blocks");
    }

    [Test]
    public async Task SequencerRpc_EnqueueStartAppendEndCycle_ProducesBlock()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        (ArbitrumRpcModule rpcModule, DelayedMessageQueue queue) = SequencerTestHelpers.CreateRpcModuleWithSequencer(chain);

        SequencerTestHelpers.InitGenesis(chain, rpcModule);

        long headBefore = chain.BlockTree.Head!.Number;

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId, 92, TestItem.AddressA, TestItem.AddressB, 1.Ether());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;

        // 1. Enqueue
        EnqueueDelayedMessagesParams enqueueParams = new([depositMsg], genesisDelayedMsgRead);
        ResultWrapper<string> enqueueResult = rpcModule.EnqueueDelayedMessages(enqueueParams);
        enqueueResult.Result.Should().Be(Result.Success);

        // 2. StartSequencing
        ResultWrapper<StartSequencingResult> seqResult = await rpcModule.StartSequencing();
        seqResult.Result.Should().Be(Result.Success);
        seqResult.Data.SequencedMsg.Should().NotBeNull();

        // 3. AppendLastSequencedBlock
        ResultWrapper<string> appendResult = await rpcModule.AppendLastSequencedBlock();
        appendResult.Result.Should().Be(Result.Success);
        appendResult.Data.Should().Be("OK");

        // 4. EndSequencing
        ResultWrapper<string> endResult = rpcModule.EndSequencing(null);
        endResult.Result.Should().Be(Result.Success);
        endResult.Data.Should().Be("OK");

        // Verify block was produced
        chain.BlockTree.Head!.Number.Should().Be(headBefore + 1);
        chain.BlockTree.Head!.Header.Nonce.Should().Be(genesisDelayedMsgRead + 1);
    }
}
