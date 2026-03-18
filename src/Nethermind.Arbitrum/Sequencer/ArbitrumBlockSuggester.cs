// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Sequencer;

public sealed class ArbitrumBlockSuggester : IProducedBlockSuggester
{
    private readonly IBlockTree _blockTree;
    private readonly IBlockProducerRunner _blockProducerRunner;
    private readonly IBlocksConfig _blocksConfig;
    private readonly ILogger _logger;

    private volatile bool _deferNext;

    public ArbitrumBlockSuggester(
        IBlockTree blockTree,
        IBlockProducerRunner blockProducerRunner,
        IBlocksConfig blocksConfig,
        ILogManager logManager)
    {
        _blockTree = blockTree;
        _blockProducerRunner = blockProducerRunner;
        _blocksConfig = blocksConfig;
        _logger = logManager.GetClassLogger<ArbitrumBlockSuggester>();

        _blockProducerRunner.BlockProduced += OnBlockProduced;
    }

    public void DeferNextBlock()
    {
        _deferNext = true;
    }

    public void ResetDefer()
    {
        _deferNext = false;
    }

    private void OnBlockProduced(object? sender, BlockEventArgs e)
    {
        if (_deferNext)
        {
            _deferNext = false;

            if (_logger.IsDebug)
                _logger.Debug($"Deferred block {e.Block.Number} ({e.Block.Hash}) — not suggested to tree.");

            return;
        }

        SuggestBlockImmediate(e.Block);
    }

    public void SuggestBlockImmediate(Block block)
    {
        if (_blocksConfig.BuildBlocksOnMainState)
        {
            if (_blockTree.SuggestBlock(block, BlockTreeSuggestOptions.None) == AddBlockResult.Added)
                _blockTree.UpdateMainChain([block], true);
        }
        else
        {
            _blockTree.SuggestBlock(block);
        }
    }

    public void Dispose()
    {
        _blockProducerRunner.BlockProduced -= OnBlockProduced;
    }
}
