// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public class ArbitrumDigestMessageTrackerTests
{
    private ArbitrumRpcTestBlockchain? _blockchain;
    private IBlockTree _blockTree = null!;
    private IArbitrumSpecHelper _specHelper = null!;
    private ArbitrumDigestMessageTracker _digestMessageTracker = null!;

    private static readonly Hash256 KeccakA = new("0x0000000000000000000000000000000000000000000000000000000000000001");

    [SetUp]
    public void SetUp()
    {
        static void configurer(ContainerBuilder cb) =>
            cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
            {
                SuggestGenesisOnStart = true,
                FillWithTestDataOnStart = true
            });

        _blockchain = ArbitrumRpcTestBlockchain.CreateDefault(configurer);
        _blockTree = _blockchain.BlockTree;
        _specHelper = _blockchain.SpecHelper;
        _digestMessageTracker = new ArbitrumDigestMessageTracker(_blockTree, _specHelper, LimboLogs.Instance);
    }
    [TearDown]
    public void TearDown()
    {
        _blockchain?.Dispose();
    }

    [Test]
    [TestCase(0, true, Description = "First message")]
    [TestCase(1, true, Description = "No previous response recorded")]
    public async Task EnsureConsistencyAsync_VariousScenarios_ReturnsExpectedResult(long messageNumber, bool expectedResult)
    {
        var result = await _digestMessageTracker.EnsureConsistencyAsync(messageNumber, timeoutMs: 1000);
        result.Should().Be(expectedResult);
    }

    [Test]
    public async Task EnsureConsistencyAsync_TipAlreadyMatches_ReturnsTrue()
    {
        Block? currentTip = _blockTree.Head!;
        _digestMessageTracker.RecordDigestMessageResponse(0, currentTip.Hash!);

        var result = await _digestMessageTracker.EnsureConsistencyAsync(messageNumber: 1, timeoutMs: 1000);
        result.Should().BeTrue();
    }

    [Test]
    public async Task EnsureConsistencyAsync_TipDoesNotMatch_ReturnsFalseOnTimeout()
    {
        _digestMessageTracker.RecordDigestMessageResponse(0, KeccakA);

        var result = await _digestMessageTracker.EnsureConsistencyAsync(messageNumber: 1, timeoutMs: 100);
        result.Should().BeFalse();
    }

    [Test]
    [TestCase(1, false, Description = "Valid advancement")]
    [TestCase(0, false, Description = "Message number equal to tip")]
    [TestCase(1, false, Description = "Message number less than tip")]
    public void ValidateTipAdvancement_VariousScenarios_BehavesCorrectly(long relativeMessageNumber, bool shouldThrow)
    {
        var currentTip = _blockTree.Head!.Number;
        var messageNumber = relativeMessageNumber == 0 ? currentTip : currentTip + relativeMessageNumber;

        if (relativeMessageNumber <= 0)
        {
            _digestMessageTracker.RecordDigestMessageResponse(currentTip, _blockTree.Head!.Hash!);
        }

        Action? act = () => _digestMessageTracker.ValidateTipAdvancement(messageNumber);

        if (shouldThrow)
            act.Should().Throw<InvalidOperationException>();
        else
            act.Should().NotThrow();
    }

    [Test]
    public void ValidateTipAdvancement_TipAdvancedBeyondMessage_ThrowsException()
    {
        Hash256? tipHash = _blockTree.Head!.Hash!;
        _digestMessageTracker.RecordDigestMessageResponse(2L, tipHash);
        const long messageNumber = 0L;

        Action act = () => _digestMessageTracker.ValidateTipAdvancement(messageNumber);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tip has advanced beyond expected message number*");
    }
}
