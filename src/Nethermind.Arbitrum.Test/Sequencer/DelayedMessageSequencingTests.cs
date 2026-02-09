// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class DelayedMessageSequencingTests
{
    [Test]
    public async Task StartSequencing_WithDelayedDeposit_ProducesBlockWithDeposit()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue queue, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId, 92, TestItem.AddressA, TestItem.AddressB, 1.Ether());

        // Genesis block has Nonce=1 (DelayedMessagesRead=1), so first delayed message index is 1
        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        queue.Enqueue([depositMsg], genesisDelayedMsgRead);

        ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

        result.Result.Should().Be(Result.Success, $"start sequencing should succeed, error: {result.Result.Error}");
        result.Data.SequencedMsg.Should().NotBeNull($"expected sequenced msg but got WaitDurationMs={result.Data.WaitDurationMs}");
        result.Data.WaitDurationMs.Should().Be(0);
        result.Data.SequencedMsg!.MsgWithMeta.DelayedMessagesRead.Should().Be(genesisDelayedMsgRead + 1);
        result.Data.SequencedMsg.MsgResult.Should().NotBeNull();
        result.Data.SequencedMsg.MsgResult!.Hash.Should().NotBeNull();

        chain.BlockTree.Head!.Header.Nonce.Should().Be(genesisDelayedMsgRead + 1);
    }

    [Test]
    public async Task StartSequencing_NoDelayedMessages_ReturnsNullWithWaitDuration()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out _, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().BeNull();
        result.Data.WaitDurationMs.Should().BeGreaterThan(0);
    }

    [Test]
    public async Task StartSequencing_WithRetryable_ProducesBlockWithRetryable()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue queue, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage retryableMsg = SequencerTestHelpers.CreateSubmitRetryableMessage(
            requestId, 92, TestItem.AddressA, TestItem.AddressB, TestItem.AddressC,
            2.Ether(), 1.Ether(), 10.GWei(), 1_000_000, 1.GWei());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        queue.Enqueue([retryableMsg], genesisDelayedMsgRead);

        ResultWrapper<StartSequencingResult> result = await engine.StartSequencingAsync();

        result.Result.Should().Be(Result.Success);
        result.Data.SequencedMsg.Should().NotBeNull();
        result.Data.SequencedMsg!.MsgResult.Should().NotBeNull();
        result.Data.SequencedMsg.MsgResult!.Hash.Should().NotBeNull();
    }

    [Test]
    public void NextDelayedMessageNumber_EmptyQueue_ReadsFromHeader()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out _, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        ResultWrapper<ulong> result = engine.NextDelayedMessageNumber();

        result.Result.Should().Be(Result.Success);
        // Genesis block has Nonce=1 (DelayedMessagesRead=1)
        result.Data.Should().Be(chain.BlockTree.Head!.Header.Nonce);
    }

    [Test]
    public void NextDelayedMessageNumber_WithQueue_ReturnsTailPlusOne()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue queue, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.EthDeposit, Address.SystemUser, 1, 1000, null, 92);
        L1IncomingMessage[] messages =
        [
            new(header, new byte[] { 1 }, null, null),
            new(header, new byte[] { 2 }, null, null),
            new(header, new byte[] { 3 }, null, null)
        ];

        engine.EnqueueDelayedMessages(messages, 5);

        ResultWrapper<ulong> result = engine.NextDelayedMessageNumber();

        result.Result.Should().Be(Result.Success);
        result.Data.Should().Be(8);
    }

    [Test]
    public async Task AppendLastSequencedBlock_AfterSequencing_CachesL1PriceData()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out DelayedMessageQueue queue, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        L1IncomingMessage depositMsg = SequencerTestHelpers.CreateEthDepositMessage(requestId, 92, TestItem.AddressA, TestItem.AddressB, 1.Ether());

        ulong genesisDelayedMsgRead = chain.BlockTree.Head!.Header.Nonce;
        queue.Enqueue([depositMsg], genesisDelayedMsgRead);

        ResultWrapper<StartSequencingResult> seqResult = await engine.StartSequencingAsync();
        seqResult.Result.Should().Be(Result.Success);
        seqResult.Data.SequencedMsg.Should().NotBeNull();

        ResultWrapper<EmptyResponse> appendResult = await engine.AppendLastSequencedBlockAsync();

        appendResult.Result.Should().Be(Result.Success);
    }

    [Test]
    public void EnqueueDelayedMessages_BatchOf3_AllQueued()
    {
        using ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault();
        ArbitrumExecutionEngine engine = SequencerTestHelpers.CreateEngineWithSequencer(chain, out _, out _);

        ResultWrapper<MessageResult> genesisResult = engine.DigestInitMessage(
            FullChainSimulationInitMessage.CreateDigestInitMessage(92));
        genesisResult.Result.Should().Be(Result.Success);

        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.EthDeposit, Address.SystemUser, 1, 1000, null, 92);
        L1IncomingMessage[] messages =
        [
            new(header, new byte[] { 1 }, null, null),
            new(header, new byte[] { 2 }, null, null),
            new(header, new byte[] { 3 }, null, null)
        ];

        ResultWrapper<EmptyResponse> enqueueResult = engine.EnqueueDelayedMessages(messages, 0);

        enqueueResult.Result.Should().Be(Result.Success);

        ResultWrapper<ulong> nextResult = engine.NextDelayedMessageNumber();
        nextResult.Data.Should().Be(3);
    }
}
