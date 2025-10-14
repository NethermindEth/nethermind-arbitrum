// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Represents a transaction queued in the sequencer, with metadata and result tracking.
/// </summary>
public class QueuedTransaction
{
    /// <summary>
    /// The transaction to be processed
    /// </summary>
    public Transaction Transaction { get; init; } = null!;

    /// <summary>
    /// Size in bytes of the marshaled transaction
    /// </summary>
    public int TxSize { get; init; }

    /// <summary>
    /// Conditional execution options for the transaction
    /// </summary>
    public ConditionalOptions? Options { get; init; }

    /// <summary>
    /// Task completion source for returning results to the submitter
    /// </summary>
    public TaskCompletionSource<Hash256> ResultSource { get; init; } = new();

    /// <summary>
    /// When this transaction was first submitted to the queue
    /// </summary>
    public DateTime FirstAppearance { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this transaction was submitted via Time boost
    /// </summary>
    public bool IsTimeBoosted { get; init; }

    /// <summary>
    /// Block number at which a time-boosted transaction was added (for ordering)
    /// </summary>
    public ulong BlockStamp { get; init; }

    /// <summary>
    /// Cancellation token for the original request context
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Thread-safe flag to ensure results are only returned once
    /// </summary>
    private int _resultReturned;

    /// <summary>
    /// Returns the result to the submitter, ensuring it's only called once
    /// </summary>
    /// <param name="result">Transaction hash if successful, or throws exception if error</param>
    public void ReturnResult(Hash256? result = null, Exception? exception = null)
    {
        if (Interlocked.Exchange(ref _resultReturned, 1) == 1)
            // Already returned result
            return;

        try
        {
            if (exception != null)
                ResultSource.TrySetException(exception);
            else if (result != null)
                ResultSource.TrySetResult(result);
            else
                ResultSource.TrySetException(new InvalidOperationException("Transaction failed with unknown error"));
        }
        catch
        {
            // Ignore exceptions from setting result - TaskCompletionSource might be in an invalid state
        }
    }

    /// <summary>
    /// Cancels the transaction if it hasn't completed yet
    /// </summary>
    public void Cancel()
    {
        if (Interlocked.Exchange(ref _resultReturned, 1) == 0)
            ResultSource.TrySetCanceled();
    }

    /// <summary>
    /// Checks if this transaction has timed out based on the configured timeout
    /// </summary>
    /// <param name="timeoutSeconds">Timeout in seconds</param>
    /// <returns>True if the transaction has timed out</returns>
    public bool IsTimedOut(int timeoutSeconds)
    {
        return DateTime.UtcNow - FirstAppearance > TimeSpan.FromSeconds(timeoutSeconds);
    }
}
