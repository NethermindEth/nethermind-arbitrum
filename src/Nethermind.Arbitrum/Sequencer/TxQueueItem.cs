// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Queue item wrapping a user transaction with async result notification.
/// </summary>
public class TxQueueItem(Transaction tx, CancellationToken cancellationToken)
{
    public Transaction Tx { get; } = tx;
    public TaskCompletionSource<Exception?> ResultChannel { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public DateTime FirstAppearance { get; } = DateTime.UtcNow;

    /// <summary>
    /// Returns the result to the caller exactly once. Subsequent calls are no-ops.
    /// </summary>
    public void ReturnResult(Exception? err) => ResultChannel.TrySetResult(err);
}
