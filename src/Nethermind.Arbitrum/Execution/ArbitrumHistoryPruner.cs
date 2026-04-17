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
public sealed class ArbitrumHistoryPruner(HistoryPruner inner, IBlocksConfig blocksConfig) : IHistoryPruner
{
    private readonly bool _buildBlocksOnMainState = blocksConfig.BuildBlocksOnMainState;

    public long? CutoffBlockNumber => inner.CutoffBlockNumber;
    public BlockHeader? OldestBlockHeader => inner.OldestBlockHeader;

    public event EventHandler<OnNewOldestBlockArgs>? NewOldestBlock
    {
        add => inner.NewOldestBlock += value;
        remove => inner.NewOldestBlock -= value;
    }

    /// <remarks>
    /// Only schedules when <c>BuildBlocksOnMainState</c> is enabled. When disabled, the existing
    /// <c>ProcessingQueueEmpty</c> subscription on the inner <see cref="HistoryPruner"/> handles
    /// triggering, so calling this would result in double-scheduling.
    /// </remarks>
    public void SchedulePruneHistory(CancellationToken cancellationToken)
    {
        if (!_buildBlocksOnMainState)
            return;
        inner.SchedulePruneHistory(cancellationToken);
    }
}
