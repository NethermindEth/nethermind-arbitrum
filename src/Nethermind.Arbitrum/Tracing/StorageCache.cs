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
    private readonly Dictionary<Hash256, StorageCacheEntry> _cache = new();
    
    // Load adds a value to the cache and returns true if the logger should emit a load opcode.
    public bool Load(Hash256 key, Hash256 value)
    {
        if (_cache.ContainsKey(key))
        {
            return false;
        }

        // The value was not in the cache, so it came from the EVM.
        _cache[key] = new StorageCacheEntry
        {
            Value = value,
            Known = value
        };
        return true;
    }

    // Store updates the value on the cache.
    public void Store(Hash256 key, Hash256 value)
    {
        _cache.TryGetValue(key, out var entry);
        entry.Value = value; 
        _cache[key] = entry;
    }
    
    // Flush returns the store operations that should be logged.
    public IEnumerable<StorageStore> Flush()
    {
        var storesToLog = new List<StorageStore>();
        
        var keys = _cache.Keys.ToList();

        foreach (var key in keys)
        {
            var entry = _cache[key];
            if (!entry.IsDirty()) continue;
            storesToLog.Add(new StorageStore(key, entry.Value));
                
            entry.Known = entry.Value;
            _cache[key] = entry;
        }
        return storesToLog.OrderBy(s => s.Key);
    }
    
    public void Clear()
    {
        _cache.Clear();
    }
}