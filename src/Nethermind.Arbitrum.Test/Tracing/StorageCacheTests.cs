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

    private static Hash256 RandomHash()
    {
        return new Hash256(RandomNumberGenerator.GetBytes(32));
    }

    [Test]
    public void Load_HasValueInCache_ReturnsFalse()
    {
        var cache = new StorageCache();
        bool emitLog = cache.Load(_keys[0], _values[0]);
        emitLog.Should().BeTrue();
        emitLog = cache.Load(_keys[0], _values[0]);
        emitLog.Should().BeFalse();
    }
    
    [Test]
    public void Store_DifferentValueIsSet_MakesValueDirty()
    {
        var cache = new StorageCache();
        _ = cache.Load(_keys[2], _values[0]);
        cache.Store(_keys[2], _values[2]);
        
        cache.Cache[_keys[2]].IsDirty().Should().BeTrue();
        cache.Cache[_keys[2]].Value.Should().BeEquivalentTo(_values[2]);
    }
    
    [Test]
    public void Load_StoreValueInCache_ShouldNotEmitLog()
    {
        var cache = new StorageCache();
        cache.Store(_keys[0], _values[0]);
        var emitLog = cache.Load(_keys[0], _values[0]);
        
        emitLog.Should().BeFalse();
    }
    
    [Test]
    public void Flush_LoadAndStoreDifferentValues_UpdateCacheWithStoredValues()
    {
        var cache = new StorageCache();
        cache.Store(_keys[0], _values[0]);
        _ =  cache.Load(_keys[0], _values[0]);
        
        _ = cache.Load(_keys[1], _values[1]);
        cache.Store(_keys[2], _values[2]);
        var stores = cache.Flush();
        
        var expected = new List<StorageStore>
        {
            new(_keys[0], _values[0]),
            new(_keys[2], _values[2])
        };
        
        stores.ToArray().Should().BeEquivalentTo(expected);

        for (int i = 0; i < _keys.Length; i++)
        {
            var entry = cache.Cache[_keys[i]];
            entry.IsDirty().Should().BeFalse();
            entry.Value.Should().BeEquivalentTo(_values[i]);
        }
        
    }

    [Test]
    public void Flush_UnchangedValues_ShouldNotUpdateCache()
    {
        var cache = new StorageCache();
        cache.Load(_keys[0], _values[0]); 
        cache.Store(_keys[0], _values[0]);
        var stores = cache.Flush();
        stores.Count().Should().Be(0);
    }
}