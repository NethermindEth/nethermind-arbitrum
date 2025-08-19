// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core.Test.Builders;
using Nethermind.Logging;

using static Nethermind.Core.Test.Builders.TestItem;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public sealed class ArbitrumSyncMonitorTests
{
    private ArbitrumRpcTestBlockchain? _blockchain;
    private IBlockTree _blockTree = null!;
    private IArbitrumSpecHelper _specHelper = null!;
    private IArbitrumConfig _config = null!;
    private ArbitrumSyncMonitor _syncMonitor = null!;

    [SetUp]
    public void SetUp()
    {
        _blockchain = ArbitrumRpcTestBlockchain.CreateDefault();
        _blockTree = _blockchain.BlockTree;
        _specHelper = _blockchain.SpecHelper;
        _config = new ArbitrumConfig();
        _syncMonitor = new ArbitrumSyncMonitor(_blockTree, _specHelper, _config, LimboLogs.Instance);

        // Ensure genesis block exists
        if (_blockTree.Genesis == null)
        {
            var genesisBlock = Build.A.Block.WithNumber(0).WithDifficulty(0).TestObject;
            _blockTree.SuggestBlock(genesisBlock);
        }
    }

    [TearDown]
    public void TearDown()
    {
        _blockchain?.Dispose();
    }

    [Test]
    public void SetFinalityData_WithNullData_DoesNotUpdateFinalityState()
    {
        _syncMonitor.SetFinalityData(null, null, null);

        _blockTree.FinalizedHash.Should().BeNull();
        _blockTree.SafeHash.Should().BeNull();
    }

    [Test]
    public void SetFinalityData_WithMissingHeaders_HandlesGracefully()
    {
        var finalityData = new ArbitrumFinalityData(999, KeccakA);
        Action act = () => _syncMonitor.SetFinalityData(null, finalityData, null);
        act.Should().NotThrow();
        _blockTree.FinalizedHash.Should().BeNull();
        _blockTree.SafeHash.Should().BeNull();
    }

    [Test]
    public void SetFinalityData_WithValidBlocks_UpdatesFinalityState()
    {
        var genesisBlock = _blockTree.Genesis!;
        var block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        var block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        var genesisMessageIndex = _specHelper.GenesisBlockNum;
        var block1MessageIndex = genesisMessageIndex + 1;
        var block2MessageIndex = genesisMessageIndex + 2;

        var finalizedData = new ArbitrumFinalityData(block1MessageIndex, block1.Hash!);
        var safeData = new ArbitrumFinalityData(block2MessageIndex, block2.Hash!);

        _syncMonitor.SetFinalityData(safeData, finalizedData, null);

        _blockTree.FinalizedHash.Should().Be(block1.Hash!);
        _blockTree.SafeHash.Should().Be(block2.Hash!);
    }

    [Test]
    public void SetFinalityData_WithHashMismatch_ThrowsInvalidOperationException()
    {
        var genesisBlock = _blockTree.Genesis!;
        var block = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        _blockTree.SuggestBlock(block);

        var genesisMessageIndex = _specHelper.GenesisBlockNum;
        var blockMessageIndex = genesisMessageIndex + 1;

        var finalityData = new ArbitrumFinalityData(blockMessageIndex, KeccakB);

        Action act = () => _syncMonitor.SetFinalityData(null, finalityData, null);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Block hash mismatch for Finalized block {block.Number}: expected={KeccakB}, actual={block.Hash}");
    }

    [Test]
    public void SetFinalityData_WithValidatorWaitEnabled_UsesValidatedBlockAsSafe()
    {
        _config.SafeBlockWaitForValidator = true;

        var genesisBlock = _blockTree.Genesis!;
        var block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        var block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        var genesisMessageIndex = _specHelper.GenesisBlockNum;
        var block1MessageIndex = genesisMessageIndex + 1;
        var block2MessageIndex = genesisMessageIndex + 2;

        var safeData = new ArbitrumFinalityData(block2MessageIndex, block2.Hash!);
        var validatedData = new ArbitrumFinalityData(block1MessageIndex, block1.Hash!);

        _syncMonitor.SetFinalityData(safeData, null, validatedData);

        _blockTree.SafeHash.Should().Be(block1.Hash!);
    }

    [Test]
    public void SetFinalityData_WithValidatorWaitEnabled_UsesValidatedBlockAsFinalized()
    {
        _config.FinalizedBlockWaitForValidator = true;

        var genesisBlock = _blockTree.Genesis!;
        var block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        var block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        var genesisMessageIndex = _specHelper.GenesisBlockNum;
        var block1MessageIndex = genesisMessageIndex + 1;
        var block2MessageIndex = genesisMessageIndex + 2;

        var finalizedData = new ArbitrumFinalityData(block2MessageIndex, block2.Hash!);
        var validatedData = new ArbitrumFinalityData(block1MessageIndex, block1.Hash!);

        _syncMonitor.SetFinalityData(null, finalizedData, validatedData);

        _blockTree.FinalizedHash.Should().Be(block1.Hash!);
    }

    [Test]
    public void SetFinalityData_WithValidatorWaitEnabled_DoesNotUseLaterValidatedBlock()
    {
        _config.SafeBlockWaitForValidator = true;

        var genesisBlock = _blockTree.Genesis!;
        var block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        var block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        var genesisMessageIndex = _specHelper.GenesisBlockNum;
        var block1MessageIndex = genesisMessageIndex + 1;
        var block2MessageIndex = genesisMessageIndex + 2;

        var safeData = new ArbitrumFinalityData(block1MessageIndex, block1.Hash!);
        var validatedData = new ArbitrumFinalityData(block2MessageIndex, block2.Hash!);

        _syncMonitor.SetFinalityData(safeData, null, validatedData);

        _blockTree.SafeHash.Should().Be(block1.Hash!);
    }

    [Test]
    public void SetFinalityData_WithSequentialCalls_UpdatesFinalityStateCorrectly()
    {
        var genesisBlock = _blockTree.Genesis!;
        var block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        var block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;
        var block3 = Build.A.Block.WithNumber(genesisBlock.Number + 3).WithParentHash(block2.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);
        _blockTree.SuggestBlock(block3);

        var genesisMessageIndex = _specHelper.GenesisBlockNum;
        var block1MessageIndex = genesisMessageIndex + 1;
        var block2MessageIndex = genesisMessageIndex + 2;
        var block3MessageIndex = genesisMessageIndex + 3;

        var finalizedData1 = new ArbitrumFinalityData(block1MessageIndex, block1.Hash!);
        var finalizedData2 = new ArbitrumFinalityData(block2MessageIndex, block2.Hash!);
        var safeData = new ArbitrumFinalityData(block3MessageIndex, block3.Hash!);

        _syncMonitor.SetFinalityData(null, finalizedData1, null);
        _blockTree.FinalizedHash.Should().Be(block1.Hash!);
        _blockTree.SafeHash.Should().BeNull();

        _syncMonitor.SetFinalityData(safeData, finalizedData2, null);
        _blockTree.FinalizedHash.Should().Be(block2.Hash!);
        _blockTree.SafeHash.Should().Be(block3.Hash!);
    }

    [Test]
    public void SetFinalityData_WithPartialData_UpdatesOnlyProvidedFinality()
    {
        var genesisBlock = _blockTree.Genesis!;
        var block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        var block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        var genesisMessageIndex = _specHelper.GenesisBlockNum;
        var block1MessageIndex = genesisMessageIndex + 1;
        var block2MessageIndex = genesisMessageIndex + 2;

        var finalizedData = new ArbitrumFinalityData(block1MessageIndex, block1.Hash!);
        var safeData = new ArbitrumFinalityData(block2MessageIndex, block2.Hash!);

        _syncMonitor.SetFinalityData(null, finalizedData, null);
        _blockTree.FinalizedHash.Should().Be(block1.Hash!);
        _blockTree.SafeHash.Should().BeNull();

        _syncMonitor.SetFinalityData(safeData, null, null);
        _blockTree.FinalizedHash.Should().Be(block1.Hash!); // Should remain unchanged
        _blockTree.SafeHash.Should().Be(block2.Hash!);
    }
}
