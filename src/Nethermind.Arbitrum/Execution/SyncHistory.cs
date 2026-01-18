// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Maintains a time-based sliding window of consensus sync data for calculating sync targets.
/// Thread-safe for concurrent access using reader-writer locks.
/// </summary>
public sealed class SyncHistory : IDisposable
{
    private readonly Queue<SyncDataEntry> _entries = [];
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly TimeSpan _msgLag;
    private bool _disposed;

    public SyncHistory(TimeSpan msgLag)
    {
        if (msgLag <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(msgLag), msgLag, "msgLag must be positive");

        _msgLag = msgLag;
    }

    /// <summary>
    /// Adds a new entry and trims old entries beyond msgLag window.
    /// </summary>
    public void Add(ulong maxMessageCount, DateTimeOffset timestamp)
    {
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            _entries.Enqueue(new SyncDataEntry(maxMessageCount, timestamp));

            DateTimeOffset cutoff = timestamp - _msgLag;
            while (_entries.Count > 0 && _entries.Peek().Timestamp < cutoff)
                _entries.Dequeue();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _lock.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Gets the sync target based on msgLag timing.
    /// Returns the maxMessageCount from the oldest entry within the msgLag window.
    /// </summary>
    public ulong GetSyncTarget(DateTimeOffset now)
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            if (_entries.Count == 0)
                return 0;

            DateTimeOffset windowStart = now - _msgLag;

            foreach (SyncDataEntry entry in _entries)
                if (entry.Timestamp >= windowStart)
                    return entry.MaxMessageCount;

            return 0;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SyncHistory));
    }

    private record SyncDataEntry(ulong MaxMessageCount, DateTimeOffset Timestamp);
}
