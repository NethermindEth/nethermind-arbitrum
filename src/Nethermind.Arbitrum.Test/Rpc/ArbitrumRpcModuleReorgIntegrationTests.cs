// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Test.Rpc;

[TestFixture]
public class ArbitrumRpcModuleReorgIntegrationTests
{
    private static readonly UInt256 L1BaseFee = 92;

    [Test]
    public async Task Reorg_FullChainReorgWithBalanceVerification()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"), numberToDigest: 0)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address[] receivers = Enumerable.Range(0, 5)
            .Select(_ => new Address(RandomNumberGenerator.GetBytes(Address.Size)))
            .ToArray();
        UInt256 value = 1000.Ether();

        // 1. Build chain with 5 deposits to different addresses
        for (int i = 0; i < 5; i++)
            await chain.Digest(new TestEthDeposit(
                new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)),
                L1BaseFee, sender, receivers[i], value));

        ulong msgIndexAtBlock2 = chain.GenesisBlockNumber + 2;

        // 2. Verify all balances
        for (int i = 0; i < 5; i++)
        {
            UInt256 balance = chain.WorldStateAccessor.GetBalance(receivers[i]);
            balance.Should().Be(value, $"receiver {i} should have initial deposit");
        }

        // 3. Reorg to block 2
        ResultWrapper<MessageResult[]> reorgResult = await chain.ReorgToMessageIndex(msgIndexAtBlock2);
        reorgResult.Result.ResultType.Should().Be(ResultType.Success);

        // 4. Verify balances reverted (only deposits 1-2 remain)
        for (int i = 0; i < 2; i++)
        {
            UInt256 balance = chain.WorldStateAccessor.GetBalance(receivers[i]);
            balance.Should().Be(value, $"receiver {i} should still have deposit after reorg");
        }
        for (int i = 2; i < 5; i++)
        {
            UInt256 balance = chain.WorldStateAccessor.GetBalance(receivers[i]);
            balance.Should().Be(0, $"receiver {i} should have no balance after reorg");
        }

        // 5. Add 2 new deposits
        Address[] newReceivers = Enumerable.Range(0, 2)
            .Select(_ => new Address(RandomNumberGenerator.GetBytes(Address.Size)))
            .ToArray();

        for (int i = 0; i < 2; i++)
            await chain.Digest(new TestEthDeposit(
                new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)),
                L1BaseFee, sender, newReceivers[i], value));

        // 6. Verify new balances correct
        for (int i = 0; i < 2; i++)
        {
            UInt256 balance = chain.WorldStateAccessor.GetBalance(newReceivers[i]);
            balance.Should().Be(value, $"new receiver {i} should have deposit");
        }
    }

    [Test]
    public async Task Reorg_EmptyReorg_ToSameHead_PreservesState()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"), numberToDigest: 0)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build chain with 3 deposits
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        ulong msgIndexAtHead = chain.LatestL2BlockIndex;
        Hash256 headHashBefore = chain.BlockTree.Head!.Hash!;
        UInt256 balanceBefore = chain.WorldStateAccessor.GetBalance(receiver);

        // Reorg to the same head (empty reorg - no blocks removed)
        ResultWrapper<MessageResult[]> result = await chain.ReorgToMessageIndex(msgIndexAtHead);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().BeEmpty();
        chain.BlockTree.Head!.Hash.Should().Be(headHashBefore);
        chain.WorldStateAccessor.GetBalance(receiver).Should().Be(balanceBefore);
    }

    [Test]
    public async Task Reorg_ResultAtPos_ReturnsCorrectResultsAfterReorg()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"), numberToDigest: 0)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build 5 blocks
        ulong[] msgIndices = new ulong[5];
        for (int i = 0; i < 5; i++)
        {
            await chain.Digest(new TestEthDeposit(
                new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)),
                L1BaseFee, sender, receiver, value));
            msgIndices[i] = chain.LatestL2BlockIndex;
        }

        // Verify all ResultAtMessageIndex calls succeed before reorg
        for (int i = 0; i < 5; i++)
        {
            ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(msgIndices[i]);
            result.Result.ResultType.Should().Be(ResultType.Success, $"block {i + 1} should exist before reorg");
        }

        // Reorg to block 3
        ResultWrapper<MessageResult[]> reorgResult = await chain.ReorgToMessageIndex(msgIndices[2]);
        reorgResult.Result.ResultType.Should().Be(ResultType.Success);

        // Verify: ResultAtMessageIndex for blocks 1-3 should succeed
        for (int i = 0; i < 3; i++)
        {
            ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(msgIndices[i]);
            result.Result.ResultType.Should().Be(ResultType.Success, $"block {i + 1} should still exist after reorg");
        }

        // Verify: ResultAtMessageIndex for blocks 4-5 should fail (reorged out)
        for (int i = 3; i < 5; i++)
        {
            ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(msgIndices[i]);
            result.Result.ResultType.Should().Be(ResultType.Failure, $"block {i + 1} should be reorged out");
        }
    }

    [Test]
    public async Task Reorg_DeepReorg_RevertsMultipleBlocks()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"), numberToDigest: 0)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build 10 blocks
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        ulong msgIndexAtBlock1 = chain.LatestL2BlockIndex;

        for (int i = 1; i < 10; i++)
            await chain.Digest(new TestEthDeposit(
                new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)),
                L1BaseFee, sender, receiver, value));

        long headBefore = chain.BlockTree.Head!.Number;
        UInt256 balanceBefore = chain.WorldStateAccessor.GetBalance(receiver);
        balanceBefore.Should().Be(value * 10);

        // Deep reorg back to block 1
        ResultWrapper<MessageResult[]> result = await chain.ReorgToMessageIndex(msgIndexAtBlock1);

        result.Result.ResultType.Should().Be(ResultType.Success);
        chain.BlockTree.Head!.Number.Should().Be(headBefore - 9, "should have removed 9 blocks");
        chain.WorldStateAccessor.GetBalance(receiver).Should().Be(value, "only block 1 deposit should remain");
    }

    [Test]
    public async Task Reorg_WithReplacementBlocks_AppliesCorrectly()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"), numberToDigest: 0)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address originalReceiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address newReceiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build chain with 3 deposits to the originalReceiver
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, originalReceiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, originalReceiver, value));
        ulong msgIndexAtBlock2 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, originalReceiver, value));

        // Verify initial state
        chain.WorldStateAccessor.GetBalance(originalReceiver).Should().Be(value * 3);
        chain.WorldStateAccessor.GetBalance(newReceiver).Should().Be(0);

        // Reorg to block 2 with a new deposit to newReceiver
        ResultWrapper<MessageResult[]> result = await chain.Reorg(
            new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, newReceiver, value),
            msgIndexAtBlock2 + 1);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().HaveCount(1);

        // Verify the final state
        chain.WorldStateAccessor.GetBalance(originalReceiver).Should().Be(value * 2, "original receiver lost block 3");
        chain.WorldStateAccessor.GetBalance(newReceiver).Should().Be(value, "new receiver got replacement block");
    }
}
