// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Runtime.InteropServices;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Tracing;

public readonly struct StorageStore(Hash256 key, Hash256 value)
{
    public Hash256 Key { get; } = key;
    public Hash256 Value { get; } = value;
}

public struct StorageCacheEntry
{
    public Hash256 Value { get; set; }
    public Hash256? Known { get; set; }

    public bool IsDirty()
    {
        return Known == null || !Value.Equals(Known);
    }
}

// storageCache mirrors the stylus storage cache on arbos when tracing a call.
// This is useful for correctly reporting the SLOAD and SSTORE opcodes.
public class StorageCache
{
    public readonly Dictionary<Hash256AsKey, StorageCacheEntry> Cache = new();

    // Load adds a value to the cache and returns true if the logger should emit a load opcode.
    public bool Load(Hash256AsKey key, Hash256AsKey value)
    {
        if (Cache.ContainsKey(key))
            return false;

        // The value was not in the cache, so it came from the EVM.
        Cache[key] = new StorageCacheEntry
        {
            Value = value,
            Known = value
        };
        return true;
    }

    // Store updates the value on the cache.
    public void Store(Hash256AsKey key, Hash256AsKey value)
    {
        StorageCacheEntry entry = CollectionsMarshal.GetValueRefOrAddDefault(Cache, key, out _);
        entry.Value = value;
        Cache[key] = entry;
    }

    // Flush returns the store operations that should be logged.
    public IEnumerable<StorageStore> Flush()
    {
        List<StorageStore> storesToLog = new();

        List<Hash256AsKey> keys = Cache.Keys.ToList();

        foreach (Hash256AsKey key in keys)
        {
            StorageCacheEntry entry = Cache[key];
            if (!entry.IsDirty())
                continue;
            storesToLog.Add(new StorageStore(key, entry.Value));

            entry.Known = entry.Value;
            Cache[key] = entry;
        }

        return storesToLog.OrderBy(s => s.Key);
    }

    public void Clear()
    {
        Cache.Clear();
    }
}
