// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Queue item wrapping a user transaction with async result notification.
/// When created with a <see cref="TimeSpan"/> timeout, the item owns its
/// <see cref="CancellationTokenSource"/> and disposes it in <see cref="ReturnResult"/>.
/// </summary>
public class TxQueueItem
{
    private readonly CancellationTokenSource? _ownedCts;

    public TxQueueItem(Transaction tx, CancellationToken cancellationToken)
    {
        Tx = tx;
        RlpEncoded = Rlp.Encode(tx).Bytes;
        CancellationToken = cancellationToken;
    }

    public TxQueueItem(Transaction tx, TimeSpan timeout)
    {
        Tx = tx;
        RlpEncoded = Rlp.Encode(tx).Bytes;
        _ownedCts = new CancellationTokenSource(timeout);
        CancellationToken = _ownedCts.Token;
    }

    public Transaction Tx { get; }
    public byte[] RlpEncoded { get; }
    public TaskCompletionSource<Exception?> ResultChannel { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken CancellationToken { get; }
    public DateTime FirstAppearance { get; } = DateTime.UtcNow;

    /// <summary>Whether this transaction was submitted via the express lane.</summary>
    public bool IsTimeboosted { get; init; }

    /// <summary>Retry counter for transient failures (used by auction resolution).</summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Block number when this item entered the timeboost queue; 0 means not timeboosted.
    /// Used for block-based expiry of express lane transactions.
    /// </summary>
    public ulong BlockStamp { get; init; }

    /// <summary>
    /// Returns the result to the caller exactly once. Subsequent calls are no-ops.
    /// Also disposes the owned CancellationTokenSource if present.
    /// </summary>
    public void ReturnResult(Exception? err)
    {
        ResultChannel.TrySetResult(err);
        _ownedCts?.Dispose();
    }

    /// <summary>Creates a timeboosted queue item with the current block number as the stamp.</summary>
    public static TxQueueItem CreateTimeboosted(Transaction tx, CancellationToken ct, ulong blockStamp)
        => new(tx, ct) { IsTimeboosted = true, BlockStamp = blockStamp };

    /// <summary>Creates a timeboosted queue item that owns its CTS with the given timeout.</summary>
    public static TxQueueItem CreateTimeboosted(Transaction tx, TimeSpan timeout, ulong blockStamp)
        => new(tx, timeout) { IsTimeboosted = true, BlockStamp = blockStamp };
}
