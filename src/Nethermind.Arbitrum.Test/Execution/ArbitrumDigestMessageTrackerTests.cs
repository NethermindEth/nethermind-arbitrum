// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using static Nethermind.Core.Test.Builders.TestItem;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public sealed class ArbitrumDigestMessageTrackerTests
{
    private ArbitrumRpcTestBlockchain? _blockchain;
    private ArbitrumDigestMessageTracker? _digestMessageTracker;
    private IBlockTree? _blockTree;
    private IArbitrumSpecHelper? _specHelper;

    [SetUp]
    public void SetUp()
    {
        _blockchain = ArbitrumRpcTestBlockchain.CreateDefault();
        _blockTree = _blockchain.BlockTree;
        _specHelper = _blockchain.SpecHelper;

        // Ensure we have a genesis block
        if (_blockTree.Head is null)
        {
            Block genesisBlock = Build.A.Block
                .WithHeader(Build.A.BlockHeader
                    .WithNumber(0)
                    .WithHash(KeccakA)
                    .WithDifficulty(1000000)
                    .TestObject)
                .TestObject;
            _blockTree.SuggestBlock(genesisBlock);
            _blockTree.UpdateMainChain(genesisBlock);
        }

        _digestMessageTracker = new ArbitrumDigestMessageTracker(_blockTree, _specHelper, _blockchain.LogManager);
    }

    [TearDown]
    public void TearDown()
    {
        _digestMessageTracker?.Dispose();
        _blockchain?.Dispose();
    }

    [Test]
    public void Constructor_InitializesWithCurrentTip()
    {
        _digestMessageTracker.Should().NotBeNull();
        _blockTree!.Head.Should().NotBeNull();
    }

    [Test]
    public async Task EnsureConsistencyAsync_FirstMessage_ReturnsTrue()
    {
        const long messageNumber = 0;
        bool result = await _digestMessageTracker!.EnsureConsistencyAsync(messageNumber);
        result.Should().BeTrue();
    }

    [Test]
    public async Task EnsureConsistencyAsync_NoPreviousResponse_ReturnsFalse()
    {
        const long messageNumber = 5; // No previous response recorded
        bool result = await _digestMessageTracker!.EnsureConsistencyAsync(messageNumber);
        result.Should().BeFalse();
    }

    [Test]
    public void ValidateTipAdvancement_MessageNumberCorrespondsToCurrentTip_DoesNotThrow()
    {
        Block currentTip = _blockTree!.Head!;
        ulong messageNumber = MessageBlockConverter.BlockNumberToMessageIndex(currentTip.Number, _specHelper!);

        _digestMessageTracker!
            .Invoking(tracker => tracker.ValidateTipAdvancement((long)messageNumber))
            .Should().NotThrow();
    }

    [Test]
    public void ValidateTipAdvancement_MessageNumberBeyondTip_DoesNotThrow()
    {
        Block currentTip = _blockTree!.Head!;
        ulong messageNumber = MessageBlockConverter.BlockNumberToMessageIndex(currentTip.Number, _specHelper!) + 1; // One more than current tip

        _digestMessageTracker!
            .Invoking(tracker => tracker.ValidateTipAdvancement((long)messageNumber))
            .Should().NotThrow();
    }

    [Test]
    public void ValidateTipAdvancement_ZeroMessageNumber_DoesNotThrow()
    {
        const long messageNumber = 0;

        _digestMessageTracker!
            .Invoking(tracker => tracker.ValidateTipAdvancement(messageNumber))
            .Should().NotThrow();
    }

    [Test]
    public void Dispose_UnsubscribesFromEvents()
    {
        ArbitrumDigestMessageTracker tracker = new(_blockTree!, _specHelper!, _blockchain!.LogManager);
        tracker.Dispose();
        tracker.Invoking(t => t.Dispose()).Should().NotThrow();
    }

    [Test]
    public void ValidateTipAdvancement_TipAdvancedBeyondMessage_ThrowsException()
    {
        // Arrange - create a scenario where tip is advanced beyond the expected message
        // We need to create a block with a higher number than what the message would expect
        Block currentTip = _blockTree!.Head!;
        ulong currentMessageNumber = MessageBlockConverter.BlockNumberToMessageIndex(currentTip.Number, _specHelper!);

        // Create a block with a higher number to simulate tip advancement
        Block advancedBlock = Build.A.Block
            .WithHeader(Build.A.BlockHeader
                .WithNumber(currentTip.Number + 1) // Advance by 1 block
                .WithHash(KeccakB)
                .WithDifficulty(1000000)
                .WithParentHash(currentTip.Hash!)
                .TestObject)
            .TestObject;

        _blockTree.SuggestBlock(advancedBlock);
        _blockTree.UpdateMainChain(advancedBlock);

        // Now try to validate a message number that corresponds to the old tip
        ulong oldMessageNumber = currentMessageNumber;

        _digestMessageTracker!
            .Invoking(tracker => tracker.ValidateTipAdvancement((long)oldMessageNumber))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Tip has advanced beyond expected message number*");
    }
}
