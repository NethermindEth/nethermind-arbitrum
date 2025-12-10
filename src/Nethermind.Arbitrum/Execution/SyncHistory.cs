// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Maintains a time-based sliding window of consensus sync data for calculating sync targets.
/// Thread-safe for concurrent access using reader-writer locks.
/// </summary>
public sealed class SyncHistory : IDisposable
{
    private readonly List<SyncDataEntry> _entries = new();
    private readonly TimeSpan _msgLag;
    private readonly ReaderWriterLockSlim _lock = new();
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
            _entries.Add(new SyncDataEntry(maxMessageCount, timestamp));

            DateTimeOffset cutoff = timestamp - _msgLag;
            int i = 0;
            while (i < _entries.Count && _entries[i].Timestamp < cutoff)
            {
                i++;
            }
            if (i > 0)
            {
                _entries.RemoveRange(0, i);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
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

            foreach (var entry in _entries)
            {
                if (entry.Timestamp >= windowStart)
                {
                    return entry.MaxMessageCount;
                }
            }

            return 0;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _lock.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SyncHistory));
    }

    private record SyncDataEntry(ulong MaxMessageCount, DateTimeOffset Timestamp);
}
