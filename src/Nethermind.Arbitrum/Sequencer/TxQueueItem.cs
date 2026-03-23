// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
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

    private TxQueueItem(Transaction tx, ConditionalOptions? options, TimeSpan? timeout, bool isTimeboosted, ulong blockStamp)
    {
        Tx = tx;
        Options = options;
        RlpEncoded = Rlp.Encode(tx).Bytes;
        _ownedCts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null;
        CancellationToken = _ownedCts?.Token ?? CancellationToken.None;
        IsTimeboosted = isTimeboosted;
        BlockStamp = blockStamp;
    }

    public Transaction Tx { get; }
    public ConditionalOptions? Options { get; }
    public byte[] RlpEncoded { get; }
    public TaskCompletionSource<Exception?> ResultChannel { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken CancellationToken { get; }
    public DateTime FirstAppearance { get; } = DateTime.UtcNow;

    /// <summary>Whether this transaction was submitted via the express lane.</summary>
    public bool IsTimeboosted { get; }

    /// <summary>Retry counter for transient failures (used by auction resolution).</summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Block number when this item entered the timeboost queue; 0 means not timeboosted.
    /// Used for block-based expiry of express lane transactions.
    /// </summary>
    public ulong BlockStamp { get; }

    /// <summary>
    /// Returns the result to the caller exactly once. Subsequent calls are no-ops.
    /// Also disposes the owned CancellationTokenSource if present.
    /// </summary>
    public void ReturnResult(Exception? err)
    {
        ResultChannel.TrySetResult(err);
        _ownedCts?.Dispose();
    }

    public static TxQueueItem CreateRegular(Transaction tx, TimeSpan? timeout = null, ConditionalOptions? options = null)
        => new(tx, options, timeout, isTimeboosted: false, blockStamp: 0);

    public static TxQueueItem CreateTimeboosted(Transaction tx, ulong blockStamp, TimeSpan? timeout = null, ConditionalOptions? options = null)
        => new(tx, options, timeout, isTimeboosted: true, blockStamp: blockStamp);
}
