// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only


namespace Nethermind.Arbitrum.Sequencer;
public sealed class SynchronizedTxQueue
{
    private readonly Queue<QueuedTransaction> _queue = new();
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// Adds an item to the queue
    /// </summary>
    public void Push(QueuedTransaction item)
    {
        _lock.EnterWriteLock();
        try
        {
            _queue.Enqueue(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes and returns an item from the queue
    /// </summary>
    public QueuedTransaction Pop()
    {
        _lock.EnterWriteLock();
        try
        {
            return _queue.Dequeue();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Gets the number of items in the queue
    /// </summary>
    public int Len()
    {
        _lock.EnterReadLock();
        try
        {
            return _queue.Count;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Checks if the queue has any items
    /// </summary>
    public bool HasItems()
    {
        _lock.EnterReadLock();
        try
        {
            return _queue.Count > 0;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Clears all items from the queue and returns them for cleanup
    /// </summary>
    public QueuedTransaction[] Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            QueuedTransaction[] items = new QueuedTransaction[_queue.Count];
            _queue.CopyTo(items, 0);
            _queue.Clear();
            return items;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
