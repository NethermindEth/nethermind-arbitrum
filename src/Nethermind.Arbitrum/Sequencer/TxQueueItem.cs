// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Queue item wrapping a user transaction with async result notification.
/// </summary>
public class TxQueueItem(Transaction tx, CancellationToken cancellationToken)
{
    public Transaction Tx { get; } = tx;
    public byte[] RlpEncoded { get; } = Rlp.Encode(tx).Bytes;
    public TaskCompletionSource<Exception?> ResultChannel { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public DateTime FirstAppearance { get; } = DateTime.UtcNow;

    /// <summary>Whether this transaction was submitted via the express lane.</summary>
    public bool IsTimeboosted { get; init; }

    /// <summary>
    /// Block number when this item entered the timeboost queue; 0 means not timeboosted.
    /// Used for block-based expiry of express lane transactions.
    /// </summary>
    public ulong BlockStamp { get; init; }

    /// <summary>
    /// Returns the result to the caller exactly once. Subsequent calls are no-ops.
    /// </summary>
    public void ReturnResult(Exception? err) => ResultChannel.TrySetResult(err);

    /// <summary>Creates a timeboosted queue item with the current block number as the stamp.</summary>
    public static TxQueueItem CreateTimeboosted(Transaction tx, CancellationToken ct, ulong blockStamp)
        => new(tx, ct) { IsTimeboosted = true, BlockStamp = blockStamp };
}
