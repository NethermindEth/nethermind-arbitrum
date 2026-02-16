// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
public class ArbitrumRpcModuleReorgTests
{
    private static readonly UInt256 L1BaseFee = 92;

    [Test]
    public async Task Reorg_WithMessageIndexZero_ReturnsError()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        ReorgParameters parameters = new(0, [], []);

        ResultWrapper<MessageResult[]> result = await chain.ArbitrumRpcModule.Reorg(parameters);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("Cannot reorg to genesis");
    }

    [Test]
    public async Task Reorg_WithNonExistentTargetBlock_ReturnsError()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        UInt256 value = 1000.Ether();

        // Build chain with 3 blocks
        await chain.Digest(new TestEthDeposit(requestId, L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        // Try to reorg to non-existent message index 10
        ReorgParameters parameters = new(10, [], []);

        ResultWrapper<MessageResult[]> result = await chain.ArbitrumRpcModule.Reorg(parameters);

        result.Result.ResultType.Should().Be(ResultType.Failure);
        result.Result.Error.Should().Contain("Reorg target block not found");
    }

    [Test]
    public async Task Reorg_ToExistingBlock_UpdatesHeadToTargetBlock()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build chain: genesis -> 1 -> 2 -> 3
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        ulong msgIndexAtBlock1 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        long headNumberBefore = chain.BlockTree.Head!.Number;

        // Reorg to message index 1 (empty, no new messages)
        ResultWrapper<MessageResult[]> result = await chain.ReorgToMessageIndex(msgIndexAtBlock1);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().BeEmpty();
        chain.BlockTree.Head!.Number.Should().BeLessThan(headNumberBefore);
    }

    [Test]
    public async Task Reorg_WithEmptyNewMessages_RemovesBlocksOnly()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build chain with 5 blocks
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        ulong msgIndexAtBlock3 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        UInt256 balanceBefore = chain.WorldStateAccessor.GetBalance(receiver);
        balanceBefore.Should().Be(value * 5);

        // Reorg to block 3 with no new messages
        ResultWrapper<MessageResult[]> result = await chain.ReorgToMessageIndex(msgIndexAtBlock3);

        result.Result.ResultType.Should().Be(ResultType.Success);

        // Balance should now reflect only 3 deposits
        UInt256 balanceAfter = chain.WorldStateAccessor.GetBalance(receiver);
        balanceAfter.Should().Be(value * 3);
    }

    [Test]
    public async Task Reorg_WithNewMessages_ProcessesNewBlocks()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiverA = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiverB = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build chain: genesis -> 1 -> 2 -> 3
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiverA, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiverA, value));
        ulong msgIndexAtBlock2 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiverA, value));

        // Reorg to block 2 with 1 new deposit to receiverB
        Hash256 newRequestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        ResultWrapper<MessageResult[]> result = await chain.Reorg(
            new TestEthDeposit(newRequestId, L1BaseFee, sender, receiverB, value),
            msgIndexAtBlock2 + 1);

        result.Result.ResultType.Should().Be(ResultType.Success);
        result.Data.Should().HaveCount(1);
        result.Data[0].BlockHash.Should().NotBeNull();

        // ReceiverA should have 2 deposits (blocks 1 and 2)
        UInt256 balanceA = chain.WorldStateAccessor.GetBalance(receiverA);
        balanceA.Should().Be(value * 2);

        // ReceiverB should have 1 deposit (new block after reorg)
        UInt256 balanceB = chain.WorldStateAccessor.GetBalance(receiverB);
        balanceB.Should().Be(value);
    }

    [Test]
    public async Task Reorg_ToTargetBlock_RevertsBalance()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Create deposits at blocks 1, 2, 3
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        ulong msgIndexAtBlock1 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        // Balance should be 3000 ETH
        UInt256 balanceBefore = chain.WorldStateAccessor.GetBalance(receiver);
        balanceBefore.Should().Be(value * 3);

        // Reorg to block 1
        ResultWrapper<MessageResult[]> result = await chain.ReorgToMessageIndex(msgIndexAtBlock1);
        result.Result.ResultType.Should().Be(ResultType.Success);

        // Balance should be 1000 ETH (blocks 2, 3 deposits reverted)
        UInt256 balanceAfter = chain.WorldStateAccessor.GetBalance(receiver);
        balanceAfter.Should().Be(value);
    }

    [Test]
    public async Task Reorg_WithNewDeposit_AppliesNewDeposit()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiverA = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiverB = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Create deposits to receiver A at blocks 1, 2, 3
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiverA, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiverA, value));
        ulong msgIndexAtBlock2 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiverA, value));

        // Reorg to block 2 with a new deposit to receiver B
        ResultWrapper<MessageResult[]> result = await chain.Reorg(
            new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiverB, value),
            msgIndexAtBlock2 + 1);

        result.Result.ResultType.Should().Be(ResultType.Success);

        // Receiver A should have 2000 ETH (blocks 1, 2 only)
        UInt256 balanceA = chain.WorldStateAccessor.GetBalance(receiverA);
        balanceA.Should().Be(value * 2);

        // Receiver B should have 1000 ETH (new deposit)
        UInt256 balanceB = chain.WorldStateAccessor.GetBalance(receiverB);
        balanceB.Should().Be(value);
    }

    [Test]
    public async Task Reorg_ThenDigest_ContinuesChainCorrectly()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build 3 blocks
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        ulong msgIndexAtBlock2 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        // Reorg to block 2
        ResultWrapper<MessageResult[]> reorgResult = await chain.ReorgToMessageIndex(msgIndexAtBlock2);
        reorgResult.Result.ResultType.Should().Be(ResultType.Success);

        // Digest a new message
        ResultWrapper<MessageResult> digestResult = await chain.Digest(
            new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        digestResult.Result.ResultType.Should().Be(ResultType.Success);

        // Chain should continue correctly from block 2
        UInt256 balance = chain.WorldStateAccessor.GetBalance(receiver);
        balance.Should().Be(value * 3); // 2 from before reorg + 1 new
    }

    [Test]
    public async Task Reorg_MultipleTimes_MaintainsConsistency()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build 5 blocks
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        ulong msgIndexAtBlock2 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        ulong msgIndexAtBlock3 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        // Reorg to block 3
        ResultWrapper<MessageResult[]> result1 = await chain.ReorgToMessageIndex(msgIndexAtBlock3);
        result1.Result.ResultType.Should().Be(ResultType.Success);
        chain.WorldStateAccessor.GetBalance(receiver).Should().Be(value * 3);

        // Reorg to block 4 (forward reorg is actually adding new blocks)
        // For now, just verify going back further
        ResultWrapper<MessageResult[]> result2 = await chain.ReorgToMessageIndex(msgIndexAtBlock2);
        result2.Result.ResultType.Should().Be(ResultType.Success);
        chain.WorldStateAccessor.GetBalance(receiver).Should().Be(value * 2);
    }

    [Test]
    public async Task Reorg_MultiAccountDeposits_BalancesCorrectAfterReorg()
    {
        // Adapted from Nitro's TestReorgResequencing - uses deposits instead of transfers
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address user1 = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address user2 = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address user3 = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address user4 = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address user5 = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1.Ether();

        // 1. Create 5 accounts via deposits
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, user1, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, user2, value));
        ulong msgIndexAtBlock2 = chain.LatestL2BlockIndex;
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, user3, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, user4, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, user5, value));

        // 2. Verify all have 1 ETH
        chain.WorldStateAccessor.GetBalance(user1).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user2).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user3).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user4).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user5).Should().Be(value);

        // 3. Reorg to block 2 (User3, User4, User5 deposits removed)
        ResultWrapper<MessageResult[]> reorgResult1 = await chain.ReorgToMessageIndex(msgIndexAtBlock2);
        reorgResult1.Result.ResultType.Should().Be(ResultType.Success);

        // 4. Verify: User1=1, User2=1, User3=0, User4=0, User5=0
        chain.WorldStateAccessor.GetBalance(user1).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user2).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user3).Should().Be(0);
        chain.WorldStateAccessor.GetBalance(user4).Should().Be(0);
        chain.WorldStateAccessor.GetBalance(user5).Should().Be(0);

        // 5. Empty reorg (no change)
        ResultWrapper<MessageResult[]> emptyReorg = await chain.ReorgToMessageIndex(msgIndexAtBlock2);
        emptyReorg.Result.ResultType.Should().Be(ResultType.Success);

        // 6. Verify balances unchanged
        chain.WorldStateAccessor.GetBalance(user1).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user2).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user3).Should().Be(0);

        // 7. Reorg with new deposit to User3
        ResultWrapper<MessageResult[]> reorgWithDeposit = await chain.Reorg(
            new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, user3, value),
            msgIndexAtBlock2 + 1);
        reorgWithDeposit.Result.ResultType.Should().Be(ResultType.Success);

        // 8. Verify: User3 now has 1 ETH
        chain.WorldStateAccessor.GetBalance(user1).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user2).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user3).Should().Be(value);
        chain.WorldStateAccessor.GetBalance(user4).Should().Be(0);
        chain.WorldStateAccessor.GetBalance(user5).Should().Be(0);
    }

    [Test]
    public async Task Reorg_VeryLargeReorg100Blocks_HandlesCorrectly()
    {
        // Stress test - build 100 blocks, reorg removing 90
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1.Ether();
        ulong msgIndexAtBlock10 = 0;

        // Build a chain with 100 deposit blocks
        for (int i = 0; i < 100; i++)
        {
            await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
            if (i == 9) // After the 10th block
                msgIndexAtBlock10 = chain.LatestL2BlockIndex;
        }

        // Verify 100 deposits
        chain.WorldStateAccessor.GetBalance(receiver).Should().Be(value * 100);

        // Reorg removing 90 blocks (keep only 10)
        ResultWrapper<MessageResult[]> result = await chain.ReorgToMessageIndex(msgIndexAtBlock10);

        result.Result.ResultType.Should().Be(ResultType.Success);

        // Verify state consistency - only 10 deposits remain
        chain.WorldStateAccessor.GetBalance(receiver).Should().Be(value * 10);
    }

    [Test]
    public async Task Reorg_MessageResultsForKeptBlocks_RemainConsistent()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build 5 blocks and save MessageResults for each
        Dictionary<ulong, MessageResult> savedResults = new();

        for (int i = 0; i < 5; i++)
        {
            ResultWrapper<MessageResult> digestResult = await chain.Digest(
                new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
            savedResults[chain.LatestL2BlockIndex] = digestResult.Data;
        }

        ulong msgIndexAtBlock3 = savedResults.Keys.Min() + 2;

        // Reorg to block 3
        ResultWrapper<MessageResult[]> reorgResult = await chain.ReorgToMessageIndex(msgIndexAtBlock3);
        reorgResult.Result.ResultType.Should().Be(ResultType.Success);

        // Verify: ResultAtMessageIndex for kept blocks (1, 2, 3) unchanged
        foreach (KeyValuePair<ulong, MessageResult> kvp in savedResults.Where(x => x.Key <= msgIndexAtBlock3))
        {
            ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(kvp.Key);
            result.Result.ResultType.Should().Be(ResultType.Success);
            result.Data.BlockHash.Should().Be(kvp.Value.BlockHash);
            result.Data.SendRoot.Should().Be(kvp.Value.SendRoot);
        }

        // Verify: ResultAtMessageIndex for removed blocks (4, 5) return error
        foreach (KeyValuePair<ulong, MessageResult> kvp in savedResults.Where(x => x.Key > msgIndexAtBlock3))
        {
            ResultWrapper<MessageResult> result = await chain.ArbitrumRpcModule.ResultAtMessageIndex(kvp.Key);
            result.Result.ResultType.Should().Be(ResultType.Failure);
        }
    }

    [Test]
    public async Task Reorg_SequentialEmptyReorgs_StateUnchanged()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithGenesisBlock(initialBaseFee: 92, arbosVersion: 40)
            .Build();

        Address sender = new(RandomNumberGenerator.GetBytes(Address.Size));
        Address receiver = new(RandomNumberGenerator.GetBytes(Address.Size));
        UInt256 value = 1000.Ether();

        // Build 3 blocks
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));
        await chain.Digest(new TestEthDeposit(new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size)), L1BaseFee, sender, receiver, value));

        ulong headIndex = chain.LatestL2BlockIndex;
        Hash256 headHash = chain.BlockTree.Head!.Hash!;
        UInt256 balanceBefore = chain.WorldStateAccessor.GetBalance(receiver);

        // Perform multiple sequential empty reorgs to the same point
        for (int i = 0; i < 5; i++)
        {
            ResultWrapper<MessageResult[]> result = await chain.ReorgToMessageIndex(headIndex);
            result.Result.ResultType.Should().Be(ResultType.Success);
            result.Data.Should().BeEmpty();
        }

        // Verify state is completely unchanged
        chain.BlockTree.Head!.Hash.Should().Be(headHash);
        chain.WorldStateAccessor.GetBalance(receiver).Should().Be(balanceBefore);
    }
}
