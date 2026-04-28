// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System;
using Nethermind.Arbitrum.Config;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.History;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Decorator for <see cref="HistoryPruner"/> that adds an explicit pruning trigger for the
/// Arbitrum execution path.
/// <para>
/// When <c>BuildBlocksOnMainState</c> is enabled, blocks bypass <c>BlockchainProcessor</c> so
/// <c>ProcessingQueueEmpty</c> is never fired and the inner <see cref="HistoryPruner"/> would
/// never prune. <see cref="SchedulePruning"/> is called at the end of each successful
/// <c>DigestMessage</c> to compensate.
/// </para>
/// <para>
/// When <c>BuildBlocksOnMainState</c> is disabled, blocks go through the normal processing queue
/// and <c>ProcessingQueueEmpty</c> already triggers the inner pruner, so <see cref="SchedulePruning"/>
/// is a no-op to avoid double-scheduling.
/// </para>
/// <para>
/// Also configures the inner pruner so that the Arbitrum genesis block (which may not be block 0)
/// is never deleted, using <see cref="IArbitrumSpecHelper.GenesisBlockNum"/> from the chain spec.
/// </para>
/// </summary>
public sealed class ArbitrumHistoryPruner : IArbitrumHistoryPruner
{
    private readonly HistoryPruner _inner;
    private readonly IProcessExitSource _processExitSource;
    private readonly bool _buildBlocksOnMainState;

    public ArbitrumHistoryPruner(HistoryPruner inner, IProcessExitSource processExitSource, IArbitrumSpecHelper specHelper, IBlocksConfig blocksConfig)
    {
        // Genesis block must never be pruned. In Arbitrum the genesis is not always block 0,
        // so we set the first deletable block to GenesisBlockNum + 1.
        inner.SetMinDeletableBlockNumber((long)(specHelper.GenesisBlockNum + 1));
        _inner = inner;
        _processExitSource = processExitSource;
        _buildBlocksOnMainState = blocksConfig.BuildBlocksOnMainState;
    }

    public long? CutoffBlockNumber => _inner.CutoffBlockNumber;
    public BlockHeader? OldestBlockHeader => _inner.OldestBlockHeader;

    public event EventHandler<OnNewOldestBlockArgs>? NewOldestBlock
    {
        add => _inner.NewOldestBlock += value;
        remove => _inner.NewOldestBlock -= value;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Only schedules when <c>BuildBlocksOnMainState</c> is enabled. When disabled, the existing
    /// <c>ProcessingQueueEmpty</c> subscription on the inner <see cref="HistoryPruner"/> handles
    /// triggering, so calling this would result in double-scheduling.
    /// </remarks>
    public void SchedulePruning()
    {
        if (_buildBlocksOnMainState)
            _inner.SchedulePruneHistory(_processExitSource.Token);
    }
}
