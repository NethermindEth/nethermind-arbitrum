// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Logging;

using static Nethermind.Core.Test.Builders.TestItem;

namespace Nethermind.Arbitrum.Test.Execution;

[TestFixture]
public sealed class ArbitrumSyncMonitorTests
{
    private ArbitrumRpcTestBlockchain? _blockchain;
    private IBlockTree _blockTree = null!;
    private IArbitrumConfig _config = null!;
    private IArbitrumSpecHelper _specHelper = null!;
    private ArbitrumSyncMonitor? _syncMonitor;

    [Test]
    public void SetConsensusSyncData_WithNullSyncProgressMap_UpdatesSyncState()
    {
        DateTime updatedAt = DateTime.UtcNow;

        Action act = () => _syncMonitor!.SetConsensusSyncData(true, 100, null, updatedAt);

        act.Should().NotThrow();
    }

    [Test]
    public void SetConsensusSyncData_WithSequentialCalls_UpdatesSyncStateCorrectly()
    {
        DateTime updatedAt1 = DateTime.UtcNow;
        DateTime updatedAt2 = updatedAt1.AddMinutes(1);

        _syncMonitor!.SetConsensusSyncData(true, 100, null, updatedAt1);
        Action act = () => _syncMonitor!.SetConsensusSyncData(true, 200, null, updatedAt2);

        act.Should().NotThrow();
    }

    [Test]
    public void SetConsensusSyncData_WithSyncedFalse_UpdatesSyncState()
    {
        DateTime updatedAt = DateTime.UtcNow;

        Action act = () => _syncMonitor!.SetConsensusSyncData(false, 50, null, updatedAt);

        act.Should().NotThrow();
    }

    [Test]
    public void SetConsensusSyncData_WithValidData_UpdatesSyncState()
    {
        Dictionary<string, object> syncProgressMap = new() { { "key1", "value1" } };
        DateTime updatedAt = DateTime.UtcNow;

        Action act = () => _syncMonitor!.SetConsensusSyncData(true, 100, syncProgressMap, updatedAt);

        act.Should().NotThrow();
    }

    [Test]
    public void SetConsensusSyncData_WithZeroMaxMessageCount_UpdatesSyncState()
    {
        DateTime updatedAt = DateTime.UtcNow;

        Action act = () => _syncMonitor!.SetConsensusSyncData(true, 0, null, updatedAt);

        act.Should().NotThrow();
    }

    [Test]
    public void SetFinalityData_WithHashMismatch_ThrowsInvalidOperationException()
    {
        BlockHeader genesisBlock = _blockTree.Genesis!;
        Block block = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        _blockTree.SuggestBlock(block);

        ulong genesisMessageIndex = _specHelper.GenesisBlockNum;
        ulong blockMessageIndex = genesisMessageIndex + 1;

        ArbitrumFinalityData finalityData = new(blockMessageIndex, KeccakB);

        Action act = () => _syncMonitor!.SetFinalityData(null, finalityData, null);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"Block hash mismatch for Finalized block {block.Number}: expected={KeccakB}, actual={block.Hash}");
    }

    [Test]
    public void SetFinalityData_WithMissingHeaders_HandlesGracefully()
    {
        ArbitrumFinalityData finalityData = new(999, KeccakA);
        Action act = () => _syncMonitor!.SetFinalityData(null, finalityData, null);
        act.Should().NotThrow();
        _blockTree.FinalizedHash.Should().BeNull();
        _blockTree.SafeHash.Should().BeNull();
    }

    [Test]
    public void SetFinalityData_WithNullData_DoesNotUpdateFinalityState()
    {
        _syncMonitor!.SetFinalityData(null, null, null);

        _blockTree.FinalizedHash.Should().BeNull();
        _blockTree.SafeHash.Should().BeNull();
    }

    [Test]
    public void SetFinalityData_WithPartialData_UpdatesOnlyProvidedFinality()
    {
        BlockHeader genesisBlock = _blockTree.Genesis!;
        Block block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        Block block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        ulong genesisMessageIndex = _specHelper.GenesisBlockNum;
        ulong block1MessageIndex = genesisMessageIndex + 1;
        ulong block2MessageIndex = genesisMessageIndex + 2;

        ArbitrumFinalityData finalizedData = new(block1MessageIndex, block1.Hash!);
        ArbitrumFinalityData safeData = new(block2MessageIndex, block2.Hash!);

        _syncMonitor!.SetFinalityData(null, finalizedData, null);
        _blockTree.FinalizedHash.Should().Be(block1.Hash!);
        _blockTree.SafeHash.Should().BeNull();

        _syncMonitor!.SetFinalityData(safeData, null, null);
        _blockTree.FinalizedHash.Should().Be(block1.Hash!); // Should remain unchanged
        _blockTree.SafeHash.Should().Be(block2.Hash!);
    }

    [Test]
    public void SetFinalityData_WithSequentialCalls_UpdatesFinalityStateCorrectly()
    {
        BlockHeader genesisBlock = _blockTree.Genesis!;
        Block block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        Block block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;
        Block block3 = Build.A.Block.WithNumber(genesisBlock.Number + 3).WithParentHash(block2.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);
        _blockTree.SuggestBlock(block3);

        ulong genesisMessageIndex = _specHelper.GenesisBlockNum;
        ulong block1MessageIndex = genesisMessageIndex + 1;
        ulong block2MessageIndex = genesisMessageIndex + 2;
        ulong block3MessageIndex = genesisMessageIndex + 3;

        ArbitrumFinalityData finalizedData1 = new(block1MessageIndex, block1.Hash!);
        ArbitrumFinalityData finalizedData2 = new(block2MessageIndex, block2.Hash!);
        ArbitrumFinalityData safeData = new(block3MessageIndex, block3.Hash!);

        _syncMonitor!.SetFinalityData(null, finalizedData1, null);
        _blockTree.FinalizedHash.Should().Be(block1.Hash!);
        _blockTree.SafeHash.Should().BeNull();

        _syncMonitor!.SetFinalityData(safeData, finalizedData2, null);
        _blockTree.FinalizedHash.Should().Be(block2.Hash!);
        _blockTree.SafeHash.Should().Be(block3.Hash!);
    }

    [Test]
    public void SetFinalityData_WithValidatorWaitEnabled_DoesNotUseLaterValidatedBlock()
    {
        _config.SafeBlockWaitForValidator = true;

        BlockHeader genesisBlock = _blockTree.Genesis!;
        Block block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        Block block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        ulong genesisMessageIndex = _specHelper.GenesisBlockNum;
        ulong block1MessageIndex = genesisMessageIndex + 1;
        ulong block2MessageIndex = genesisMessageIndex + 2;

        ArbitrumFinalityData safeData = new(block1MessageIndex, block1.Hash!);
        ArbitrumFinalityData validatedData = new(block2MessageIndex, block2.Hash!);

        _syncMonitor!.SetFinalityData(safeData, null, validatedData);

        _blockTree.SafeHash.Should().Be(block1.Hash!);
    }

    [Test]
    public void SetFinalityData_WithValidatorWaitEnabled_UsesValidatedBlockAsFinalized()
    {
        _config.FinalizedBlockWaitForValidator = true;

        BlockHeader genesisBlock = _blockTree.Genesis!;
        Block block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        Block block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        ulong genesisMessageIndex = _specHelper.GenesisBlockNum;
        ulong block1MessageIndex = genesisMessageIndex + 1;
        ulong block2MessageIndex = genesisMessageIndex + 2;

        ArbitrumFinalityData finalizedData = new(block2MessageIndex, block2.Hash!);
        ArbitrumFinalityData validatedData = new(block1MessageIndex, block1.Hash!);

        _syncMonitor!.SetFinalityData(null, finalizedData, validatedData);

        _blockTree.FinalizedHash.Should().Be(block1.Hash!);
    }

    [Test]
    public void SetFinalityData_WithValidatorWaitEnabled_UsesValidatedBlockAsSafe()
    {
        _config.SafeBlockWaitForValidator = true;

        BlockHeader genesisBlock = _blockTree.Genesis!;
        Block block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        Block block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        ulong genesisMessageIndex = _specHelper.GenesisBlockNum;
        ulong block1MessageIndex = genesisMessageIndex + 1;
        ulong block2MessageIndex = genesisMessageIndex + 2;

        ArbitrumFinalityData safeData = new(block2MessageIndex, block2.Hash!);
        ArbitrumFinalityData validatedData = new(block1MessageIndex, block1.Hash!);

        _syncMonitor!.SetFinalityData(safeData, null, validatedData);

        _blockTree.SafeHash.Should().Be(block1.Hash!);
    }

    [Test]
    public void SetFinalityData_WithValidBlocks_UpdatesFinalityState()
    {
        BlockHeader genesisBlock = _blockTree.Genesis!;
        Block block1 = Build.A.Block.WithNumber(genesisBlock.Number + 1).WithParentHash(genesisBlock.Hash!).TestObject;
        Block block2 = Build.A.Block.WithNumber(genesisBlock.Number + 2).WithParentHash(block1.Hash!).TestObject;

        _blockTree.SuggestBlock(block1);
        _blockTree.SuggestBlock(block2);

        ulong genesisMessageIndex = _specHelper.GenesisBlockNum;
        ulong block1MessageIndex = genesisMessageIndex + 1;
        ulong block2MessageIndex = genesisMessageIndex + 2;

        ArbitrumFinalityData finalizedData = new(block1MessageIndex, block1.Hash!);
        ArbitrumFinalityData safeData = new(block2MessageIndex, block2.Hash!);

        _syncMonitor!.SetFinalityData(safeData, finalizedData, null);

        _blockTree.FinalizedHash.Should().Be(block1.Hash!);
        _blockTree.SafeHash.Should().Be(block2.Hash!);
    }

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
            Block genesisBlock = Build.A.Block.WithNumber(0).WithDifficulty(0).TestObject;
            _blockTree.SuggestBlock(genesisBlock);
        }
    }

    [TearDown]
    public void TearDown()
    {
        _syncMonitor?.Dispose();
        _blockchain?.Dispose();
    }
}
