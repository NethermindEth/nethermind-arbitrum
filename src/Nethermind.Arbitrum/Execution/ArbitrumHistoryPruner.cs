// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.History;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Decorator for <see cref="IHistoryPruner"/> that adds an explicit pruning trigger for the
/// Arbitrum execution path.
/// <para>
/// When <c>BuildBlocksOnMainState</c> is enabled, blocks bypass <c>BlockchainProcessor</c> so
/// <c>ProcessingQueueEmpty</c> is never fired and the inner <see cref="HistoryPruner"/> would
/// never prune. <see cref="SchedulePruneHistory"/> is called at the end of each successful
/// <c>DigestMessage</c> to compensate.
/// </para>
/// <para>
/// When <c>BuildBlocksOnMainState</c> is disabled, blocks go through the normal processing queue
/// and <c>ProcessingQueueEmpty</c> already triggers the inner pruner, so <see cref="SchedulePruneHistory"/>
/// is a no-op to avoid double-scheduling.
/// </para>
/// </summary>
public sealed class ArbitrumHistoryPruner(IHistoryPruner inner, IBlocksConfig blocksConfig) : IHistoryPruner
{
    private readonly bool _buildBlocksOnMainState = blocksConfig.BuildBlocksOnMainState;

    public long? CutoffBlockNumber => inner.CutoffBlockNumber;
    public long? BalCutoffBlockNumber => inner.BalCutoffBlockNumber;
    public BlockHeader? OldestBlockHeader => inner.OldestBlockHeader;

    public event EventHandler<OnNewOldestBlockArgs>? NewOldestBlock
    {
        add => inner.NewOldestBlock += value;
        remove => inner.NewOldestBlock -= value;
    }

    /// <summary>
    /// Schedules history pruning when Arbitrum requires an explicit trigger.
    /// </summary>
    /// <remarks>
    /// Only schedules when <c>BuildBlocksOnMainState</c> is enabled. When disabled, the existing
    /// <c>ProcessingQueueEmpty</c> subscription on the inner <see cref="HistoryPruner"/> handles
    /// triggering, so calling this would result in double-scheduling.
    /// </remarks>
    public void SchedulePruneHistory()
    {
        if (!_buildBlocksOnMainState)
            return;
        inner.SchedulePruneHistory();
    }

    public long GetRetentionBlocks(long retentionEpochs) => inner.GetRetentionBlocks(retentionEpochs);
}
