// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Temporary hold for transactions with too-high nonces, waiting for predecessor.
/// Uses insertion-ordered linked list for O(1) capacity eviction — since all entries
/// share the same expiry duration, insertion order equals expiry order.
/// </summary>
public class NonceFailureCache(int maxSize, TimeSpan? expiry = null)
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(1);

    private readonly Dictionary<(Address Sender, ulong Nonce), LinkedListNode<CacheEntry>> _map = new();
    private readonly LinkedList<CacheEntry> _list = new();
    private readonly TimeSpan _expiry = expiry ?? DefaultExpiry;

    public void Add(Address sender, ulong nonce, TxQueueItem queueItem)
    {
        (Address, ulong) key = (sender, nonce);
        DateTime expiryTime = queueItem.FirstAppearance + _expiry;

        if (_map.ContainsKey(key) || DateTime.UtcNow > expiryTime)
        {
            queueItem.ReturnResult(new InvalidOperationException($"Nonce too high: sender={sender}, nonce={nonce}"));
            return;
        }

        if (_map.Count >= maxSize)
            EvictOldest();

        CacheEntry entry = new(key, queueItem, expiryTime);
        LinkedListNode<CacheEntry> node = _list.AddLast(entry);
        _map[key] = node;
    }

    public bool TryRevive(Address sender, ulong nonce, out TxQueueItem? queueItem)
    {
        queueItem = null;
        (Address, ulong) key = (sender, nonce);

        if (!_map.Remove(key, out LinkedListNode<CacheEntry>? node))
            return false;

        _list.Remove(node);
        queueItem = node.Value.Item;
        return true;
    }

    public void EvictExpired()
    {
        DateTime now = DateTime.UtcNow;

        while (_list.First is not null)
        {
            CacheEntry entry = _list.First.Value;
            if (now <= entry.Expiry)
                break;

            entry.Item.ReturnResult(new InvalidOperationException($"Nonce failure expired: sender={entry.Key.Sender}, nonce={entry.Key.Nonce}"));
            _map.Remove(entry.Key);
            _list.RemoveFirst();
        }
    }

    public void Clear()
    {
        foreach (CacheEntry entry in _list)
            entry.Item.ReturnResult(new InvalidOperationException("Nonce failure cache cleared"));

        _map.Clear();
        _list.Clear();
    }

    private void EvictOldest()
    {
        LinkedListNode<CacheEntry> oldest = _list.First!;
        oldest.Value.Item.ReturnResult(new InvalidOperationException("Nonce failure cache overflow"));
        _map.Remove(oldest.Value.Key);
        _list.RemoveFirst();
    }

    private record CacheEntry((Address Sender, ulong Nonce) Key, TxQueueItem Item, DateTime Expiry);
}
