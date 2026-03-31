// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Sequencer;

[TestFixture]
public class SequencerBlockSuggesterTests
{
    private static readonly BlocksConfig BlocksConfig = new() { BuildBlocksOnMainState = true };

    [Test]
    public void ImmediateMode_BlockProduced_SuggestsToTree()
    {
        Block genesis = Build.A.Block.Genesis.TestObject;
        IBlockTree blockTree = Build.A.BlockTree().WithBlocks(genesis).TestObject;
        FakeBlockProducerRunner runner = new();

        using ArbitrumSequencerBlockSuggester suggester = new(blockTree, runner, BlocksConfig, NullLogManager.Instance);

        Block block = Build.A.Block.WithParent(genesis).TestObject;
        runner.RaiseBlockProduced(block);

        blockTree.Head!.Hash.Should().Be(block.Hash!);
    }

    [Test]
    public void DeferredMode_BlockProduced_DoesNotSuggest()
    {
        Block genesis = Build.A.Block.Genesis.TestObject;
        IBlockTree blockTree = Build.A.BlockTree().WithBlocks(genesis).TestObject;
        FakeBlockProducerRunner runner = new();

        using ArbitrumSequencerBlockSuggester suggester = new(blockTree, runner, BlocksConfig, NullLogManager.Instance);

        long headBefore = blockTree.Head!.Number;

        suggester.DeferNextBlock();
        Block block = Build.A.Block.WithParent(genesis).TestObject;
        runner.RaiseBlockProduced(block);

        blockTree.Head!.Number.Should().Be(headBefore);
    }

    [Test]
    public void DeferFlag_SecondBlockProduced_SuggestsImmediately()
    {
        Block genesis = Build.A.Block.Genesis.TestObject;
        IBlockTree blockTree = Build.A.BlockTree().WithBlocks(genesis).TestObject;
        FakeBlockProducerRunner runner = new();

        using ArbitrumSequencerBlockSuggester suggester = new(blockTree, runner, BlocksConfig, NullLogManager.Instance);

        suggester.DeferNextBlock();
        Block block1 = Build.A.Block.WithParent(genesis).TestObject;
        runner.RaiseBlockProduced(block1);

        blockTree.Head!.Number.Should().Be(genesis.Number);

        Block block2 = Build.A.Block.WithParent(blockTree.Head).TestObject;
        runner.RaiseBlockProduced(block2);

        blockTree.Head!.Number.Should().Be(genesis.Number + 1);
    }

    [Test]
    public void ResetDefer_AfterDeferNextBlock_ClearsFlag()
    {
        Block genesis = Build.A.Block.Genesis.TestObject;
        IBlockTree blockTree = Build.A.BlockTree().WithBlocks(genesis).TestObject;
        FakeBlockProducerRunner runner = new();

        using ArbitrumSequencerBlockSuggester suggester = new(blockTree, runner, BlocksConfig, NullLogManager.Instance);

        suggester.DeferNextBlock();
        suggester.ResetDefer();

        Block block = Build.A.Block.WithParent(genesis).TestObject;
        runner.RaiseBlockProduced(block);

        blockTree.Head!.Hash.Should().Be(block.Hash!);
    }

    private sealed class FakeBlockProducerRunner : IBlockProducerRunner
    {
        public event EventHandler<BlockEventArgs>? BlockProduced;

        public void RaiseBlockProduced(Block block) => BlockProduced?.Invoke(this, new BlockEventArgs(block));

        public void Start() { }
        public Task StopAsync() => Task.CompletedTask;
        public bool IsProducingBlocks(ulong? maxProducingInterval) => false;
    }
}
