// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Core;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Temporary hold for transactions with too-high nonces, waiting for predecessor.
/// </summary>
public class NonceFailureCache(int maxSize, TimeSpan? expiry = null)
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(1);

    private readonly Dictionary<(Address Sender, ulong Nonce), Entry> _cache = new();
    private readonly TimeSpan _expiry = expiry ?? DefaultExpiry;

    public void Add(Address sender, ulong nonce, TxQueueItem queueItem)
    {
        (Address, ulong) key = (sender, nonce);
        DateTime expiryTime = queueItem.FirstAppearance + _expiry;

        if (_cache.ContainsKey(key) || DateTime.UtcNow > expiryTime)
        {
            queueItem.ReturnResult(new InvalidOperationException($"Nonce too high: sender={sender}, nonce={nonce}"));
            return;
        }

        if (_cache.Count >= maxSize)
            EvictOldest();

        _cache[key] = new Entry(queueItem, expiryTime);
    }

    public bool TryRevive(Address sender, ulong nonce, out TxQueueItem? queueItem)
    {
        queueItem = null;
        (Address, ulong) key = (sender, nonce);

        if (!_cache.Remove(key, out Entry? entry))
            return false;

        queueItem = entry.Item;
        return true;
    }

    public void EvictExpired()
    {
        DateTime now = DateTime.UtcNow;
        List<(Address, ulong)>? toRemove = null;

        foreach (KeyValuePair<(Address Sender, ulong Nonce), Entry> kvp in _cache)
        {
            if (now > kvp.Value.Expiry)
            {
                kvp.Value.Item.ReturnResult(new InvalidOperationException($"Nonce failure expired: sender={kvp.Key.Sender}, nonce={kvp.Key.Nonce}"));
                toRemove ??= new List<(Address, ulong)>();
                toRemove.Add(kvp.Key);
            }
        }

        if (toRemove is null)
            return;

        foreach ((Address, ulong) key in toRemove)
            _cache.Remove(key);
    }

    public void Clear()
    {
        foreach (Entry entry in _cache.Values)
            entry.Item.ReturnResult(new InvalidOperationException("Nonce failure cache cleared"));

        _cache.Clear();
    }

    private void EvictOldest()
    {
        (Address, ulong) oldestKey = default;
        DateTime oldestExpiry = DateTime.MaxValue;

        foreach (KeyValuePair<(Address Sender, ulong Nonce), Entry> kvp in _cache)
        {
            if (kvp.Value.Expiry < oldestExpiry)
            {
                oldestExpiry = kvp.Value.Expiry;
                oldestKey = kvp.Key;
            }
        }

        if (_cache.TryGetValue(oldestKey, out Entry? evicted))
            evicted.Item.ReturnResult(new InvalidOperationException("Nonce failure cache overflow"));

        _cache.Remove(oldestKey);
    }

    private record Entry(TxQueueItem Item, DateTime Expiry);
}
