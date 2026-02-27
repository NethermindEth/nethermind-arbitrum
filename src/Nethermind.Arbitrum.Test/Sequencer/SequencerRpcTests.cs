// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerRpcTests
{
    [Test]
    public async Task StartSequencing_ViaRpc_ReturnsResult()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: c => c.SequencerEnabled = true);

        chain.ArbitrumRpcModule.DigestInitMessage(FullChainSimulationInitMessage.CreateDigestInitMessage(92));

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = CreateEthDepositMessage(requestId, 92, TestItem.AddressA, TestItem.AddressB, 1.Ether());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages([depositMsg], genesisDelayedMsgRead);

        ResultWrapper<StartSequencingResult> result = await chain.NitroExecutionRpcModule.nitroexecution_startSequencing(0, 0, 0);

        result.Result.Should().Be(Result.Success, $"start sequencing should succeed, error: {result.Result.Error}");
        result.Data.SequencedMsg.Should().NotBeNull($"expected sequenced msg but got WaitDurationMs={result.Data.WaitDurationMs}");
        result.Data.WaitDurationMs.Should().Be(0);
        result.Data.SequencedMsg!.MsgWithMeta.DelayedMessagesRead.Should().Be(genesisDelayedMsgRead + 1);
        result.Data.SequencedMsg.MsgResult.Should().NotBeNull();
        result.Data.SequencedMsg.MsgResult!.Hash.Should().NotBeNull();
    }

    [Test]
    public void EndSequencing_ViaRpc_Succeeds()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: c => c.SequencerEnabled = true);

        chain.ArbitrumRpcModule.DigestInitMessage(FullChainSimulationInitMessage.CreateDigestInitMessage(92));

        ResultWrapper<EmptyResponse> result = chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null);

        result.Result.Should().Be(Result.Success);
    }

    [Test]
    public void EnqueueDelayedMessages_ViaRpc_QueuesMessages()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: c => c.SequencerEnabled = true);

        chain.ArbitrumRpcModule.DigestInitMessage(FullChainSimulationInitMessage.CreateDigestInitMessage(92));

        L1IncomingMessageHeader header = new(ArbitrumL1MessageKind.EthDeposit, Address.SystemUser, 1, 1000, null, 92);
        L1IncomingMessage[] messages =
        [
            new(header, [1], null, null),
            new(header, [2], null, null)
        ];

        ResultWrapper<EmptyResponse> enqueueResult = chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages(messages, 5);

        enqueueResult.Result.Should().Be(Result.Success);

        ResultWrapper<ulong> nextResult = chain.NitroExecutionRpcModule.nitroexecution_nextDelayedMessageNumber();
        nextResult.Result.Should().Be(Result.Success);
        nextResult.Data.Should().Be(7);
    }

    [Test]
    public async Task Pause_ViaRpc_StopsSequencing()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: c => c.SequencerEnabled = true);

        chain.ArbitrumRpcModule.DigestInitMessage(FullChainSimulationInitMessage.CreateDigestInitMessage(92));

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = CreateEthDepositMessage(requestId, 92, TestItem.AddressA, TestItem.AddressB, 1.Ether());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages([depositMsg], genesisDelayedMsgRead);

        ResultWrapper<EmptyResponse> pauseResult = chain.NitroExecutionRpcModule.nitroexecution_pause();
        pauseResult.Result.Should().Be(Result.Success);

        ResultWrapper<StartSequencingResult> result = await chain.NitroExecutionRpcModule.nitroexecution_startSequencing(0, 0, 0);
        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().BeNull("sequencer is paused, should not produce blocks");
    }

    [Test]
    public async Task SequencerRpc_EnqueueStartAppendEndCycle_ProducesBlock()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(
            configureArbitrum: c => c.SequencerEnabled = true);

        chain.ArbitrumRpcModule.DigestInitMessage(FullChainSimulationInitMessage.CreateDigestInitMessage(92));

        long headBefore = chain.BlockTree.Head!.Number;

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = CreateEthDepositMessage(requestId, 92, TestItem.AddressA, TestItem.AddressB, 1.Ether());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;

        ResultWrapper<EmptyResponse> enqueueResult = chain.NitroExecutionRpcModule.nitroexecution_enqueueDelayedMessages([depositMsg], genesisDelayedMsgRead);
        enqueueResult.Result.Should().Be(Result.Success);

        ResultWrapper<StartSequencingResult> seqResult = await chain.NitroExecutionRpcModule.nitroexecution_startSequencing(0, 0, 0);
        seqResult.Result.Should().Be(Result.Success);
        seqResult.Data.SequencedMsg.Should().NotBeNull();

        ResultWrapper<EmptyResponse> appendResult = await chain.NitroExecutionRpcModule.nitroexecution_appendLastSequencedBlock();
        appendResult.Result.Should().Be(Result.Success);

        ResultWrapper<EmptyResponse> endResult = chain.NitroExecutionRpcModule.nitroexecution_endSequencing(null);
        endResult.Result.Should().Be(Result.Success);

        chain.BlockTree.Head!.Number.Should().Be(headBefore + 1);
        chain.BlockTree.Head!.Header.Nonce.Should().Be(genesisDelayedMsgRead + 1);
    }

    private static L1IncomingMessage CreateEthDepositMessage(
        Hash256 requestId, UInt256 l1BaseFee, Address sender, Address receiver, UInt256 value)
    {
        ArbitrumDepositTransaction deposit = new()
        {
            SourceHash = requestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = UInt256.Zero,
            GasLimit = 0,
            IsOPSystemTransaction = false,
            Mint = value,
            ChainId = 412346,
            L1RequestId = requestId,
            Value = value,
            SenderAddress = sender,
            To = receiver
        };

        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.EthDeposit,
            sender,
            1,
            (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            requestId,
            l1BaseFee);

        byte[] l2Msg = NitroL2MessageSerializer.SerializeTransactions([deposit], header);

        return new L1IncomingMessage(header, l2Msg, null, null);
    }
}
