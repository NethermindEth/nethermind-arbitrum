using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Test.Tracing;

[TestFixture]
public class StorageCacheTests
{
    private static Hash256[] _keys = null!;
    private static Hash256[] _values = null!;

    [Test]
    public void Flush_LoadAndStoreDifferentValues_UpdateCacheWithStoredValues()
    {
        StorageCache cache = new StorageCache();
        cache.Store(_keys[0], _values[0]);
        _ = cache.Load(_keys[0], _values[0]);

        _ = cache.Load(_keys[1], _values[1]);
        cache.Store(_keys[2], _values[2]);
        IEnumerable<StorageStore> stores = cache.Flush();

        List<StorageStore> expected = new List<StorageStore>
        {
            new(_keys[0], _values[0]),
            new(_keys[2], _values[2])
        };

        stores.ToArray().Should().BeEquivalentTo(expected);

        for (int i = 0; i < _keys.Length; i++)
        {
            StorageCacheEntry entry = cache.Cache[_keys[i]];
            entry.IsDirty().Should().BeFalse();
            entry.Value.Should().BeEquivalentTo(_values[i]);
        }
    }

    [Test]
    public void Flush_UnchangedValues_ShouldNotUpdateCache()
    {
        StorageCache cache = new StorageCache();
        cache.Load(_keys[0], _values[0]);
        cache.Store(_keys[0], _values[0]);
        IEnumerable<StorageStore> stores = cache.Flush();
        stores.Count().Should().Be(0);
    }

    [Test]
    public void Load_HasValueInCache_ReturnsFalse()
    {
        StorageCache cache = new StorageCache();
        bool emitLog = cache.Load(_keys[0], _values[0]);
        emitLog.Should().BeTrue();
        emitLog = cache.Load(_keys[0], _values[0]);
        emitLog.Should().BeFalse();
    }

    [Test]
    public void Load_StoreValueInCache_ShouldNotEmitLog()
    {
        StorageCache cache = new StorageCache();
        cache.Store(_keys[0], _values[0]);
        bool emitLog = cache.Load(_keys[0], _values[0]);

        emitLog.Should().BeFalse();
    }

    // A one-time setup for the entire test class, equivalent to Go's initial setup.
    [SetUp]
    public void SetUp()
    {
        _keys = new Hash256[3];
        _values = new Hash256[3];
        for (int i = 0; i < _keys.Length; i++)
        {
            _keys[i] = RandomHash();
            _values[i] = RandomHash();
        }
    }

    [Test]
    public void Store_DifferentValueIsSet_MakesValueDirty()
    {
        StorageCache cache = new StorageCache();
        _ = cache.Load(_keys[2], _values[0]);
        cache.Store(_keys[2], _values[2]);

        cache.Cache[_keys[2]].IsDirty().Should().BeTrue();
        cache.Cache[_keys[2]].Value.Should().BeEquivalentTo(_values[2]);
    }

    private static Hash256 RandomHash()
    {
        return new Hash256(RandomNumberGenerator.GetBytes(Hash256.Size));
    }
}
