// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.History;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Extends <see cref="IHistoryPruner"/> with Arbitrum-specific pruning triggers.
/// Allows pruning to be scheduled at the end of a DigestMessage RPC call, which is
/// necessary when <c>BuildBlocksOnMainState</c> is enabled and blocks do not flow through
/// <c>BlockchainProcessor</c> (which normally fires <c>ProcessingQueueEmpty</c>).
/// </summary>
public interface IArbitrumHistoryPruner : IHistoryPruner
{
    /// <summary>
    /// Schedules a pruning pass in the background. Returns immediately.
    /// Internally guarded against concurrent scheduling via <c>HistoryPruner.SchedulePruneHistory</c>.
    /// </summary>
    void SchedulePruning();
}
