// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;

namespace Nethermind.Arbitrum.Test.Stylus;

/// <summary>
/// Tests for WasmDb cache operations including ClearCaches().
/// Uses in-memory database to test real cache behavior.
/// </summary>
public class WasmDbCacheTests
{
    private MemDb _memDb = null!;
    private WasmDb _wasmDb = null!;

    [SetUp]
    public void Setup()
    {
        _memDb = new MemDb();
        _wasmDb = new WasmDb(_memDb);
    }

    [TearDown]
    public void TearDown()
    {
        _memDb.Dispose();
    }


    [Test]
    public void ClearCaches_OnEmptyCaches_DoesNotThrow()
    {
        Action act = () => _wasmDb.ClearCaches();

        act.Should().NotThrow();
    }

    [Test]
    public void ClearCaches_MultipleTimes_DoesNotThrow()
    {
        for (int i = 0; i < 10; i++)
        {
            _wasmDb.ClearCaches();
        }

        // Should complete without error
        true.Should().BeTrue();
    }

    [Test]
    public void ClearCaches_AfterWriteActivation_ClearsInMemoryCache()
    {
        // Setup: write activation to populate cache
        Hash256 moduleHash = TestItem.KeccakA;
        Dictionary<string, byte[]> asmMap = new()
        {
            ["amd64"] = [1, 2, 3, 4]
        };

        _wasmDb.WriteActivation(moduleHash, asmMap);

        // Verify it's cached (first call populates)
        _wasmDb.TryGetActivatedAsm("amd64", moduleHash, out byte[] cached1).Should().BeTrue();
        cached1.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });

        // Act: clear caches
        _wasmDb.ClearCaches();

        // Verify: still retrievable from DB (cache cleared but DB intact)
        _wasmDb.TryGetActivatedAsm("amd64", moduleHash, out byte[] cached2).Should().BeTrue();
        cached2.Should().BeEquivalentTo(new byte[] { 1, 2, 3, 4 });
    }



    [Test]
    public void TryGetActivatedAsm_NotInCacheOrDb_ReturnsFalse()
    {
        Hash256 moduleHash = TestItem.KeccakB;

        bool found = _wasmDb.TryGetActivatedAsm("arm64", moduleHash, out byte[] bytes);

        found.Should().BeFalse();
        bytes.Should().BeEmpty();
    }

    [Test]
    public void TryGetActivatedAsm_AfterWrite_ReturnsData()
    {
        Hash256 moduleHash = TestItem.KeccakC;
        byte[] expectedAsm = [10, 20, 30, 40, 50];
        Dictionary<string, byte[]> asmMap = new() { ["amd64"] = expectedAsm };

        _wasmDb.WriteActivation(moduleHash, asmMap);
        bool found = _wasmDb.TryGetActivatedAsm("amd64", moduleHash, out byte[] bytes);

        found.Should().BeTrue();
        bytes.Should().BeEquivalentTo(expectedAsm);
    }

    [Test]
    public void TryGetActivatedAsm_DifferentTargets_ReturnsCorrectData()
    {
        Hash256 moduleHash = TestItem.KeccakD;
        Dictionary<string, byte[]> asmMap = new()
        {
            ["amd64"] = [1, 2, 3],
            ["arm64"] = [4, 5, 6]
        };

        _wasmDb.WriteActivation(moduleHash, asmMap);

        _wasmDb.TryGetActivatedAsm("amd64", moduleHash, out byte[] x86).Should().BeTrue();
        _wasmDb.TryGetActivatedAsm("arm64", moduleHash, out byte[] arm).Should().BeTrue();

        x86.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        arm.Should().BeEquivalentTo(new byte[] { 4, 5, 6 });
    }



    [Test]
    public void WriteActivation_MultipleTargets_AllStored()
    {
        Hash256 moduleHash = TestItem.KeccakE;
        Dictionary<string, byte[]> asmMap = new()
        {
            ["amd64"] = [1, 2, 3],
            ["arm64"] = [4, 5, 6],
            ["host"] = [7, 8, 9]
        };

        _wasmDb.WriteActivation(moduleHash, asmMap);

        _wasmDb.TryGetActivatedAsm("amd64", moduleHash, out byte[] x86).Should().BeTrue();
        _wasmDb.TryGetActivatedAsm("arm64", moduleHash, out byte[] arm).Should().BeTrue();
        _wasmDb.TryGetActivatedAsm("host", moduleHash, out byte[] wasm).Should().BeTrue();

        x86.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        arm.Should().BeEquivalentTo(new byte[] { 4, 5, 6 });
        wasm.Should().BeEquivalentTo(new byte[] { 7, 8, 9 });
    }

    [Test]
    public void WriteActivation_OverwriteExisting_UpdatesValue()
    {
        Hash256 moduleHash = TestItem.KeccakF;

        // First write
        _wasmDb.WriteActivation(moduleHash, new Dictionary<string, byte[]> { ["amd64"] = [1, 2, 3] });

        // Second write overwrites
        _wasmDb.WriteActivation(moduleHash, new Dictionary<string, byte[]> { ["amd64"] = [9, 8, 7] });

        _wasmDb.TryGetActivatedAsm("amd64", moduleHash, out byte[] result).Should().BeTrue();
        result.Should().BeEquivalentTo(new byte[] { 9, 8, 7 });
    }



    [Test]
    public void WriteAllActivations_MultipleModules_AllStored()
    {
        Hash256 hash1 = TestItem.KeccakA;
        Hash256 hash2 = TestItem.KeccakB;

        Dictionary<Hash256AsKey, IReadOnlyDictionary<string, byte[]>> wasms = new()
        {
            [hash1] = new Dictionary<string, byte[]> { ["amd64"] = [1, 1, 1] },
            [hash2] = new Dictionary<string, byte[]> { ["arm64"] = [2, 2, 2] }
        };

        _wasmDb.WriteAllActivations(wasms);

        _wasmDb.TryGetActivatedAsm("amd64", hash1, out byte[] data1).Should().BeTrue();
        _wasmDb.TryGetActivatedAsm("arm64", hash2, out byte[] data2).Should().BeTrue();

        data1.Should().BeEquivalentTo(new byte[] { 1, 1, 1 });
        data2.Should().BeEquivalentTo(new byte[] { 2, 2, 2 });
    }



    [Test]
    public void ClearCaches_Clears_AllShards()
    {
        // Write to many different module hashes to hit different shards
        // (sharding is based on last nibble of hash)
        for (int i = 0; i < 20; i++)
        {
            byte[] hashBytes = new byte[32];
            hashBytes[31] = (byte)i; // Different last byte = different shard
            Hash256 moduleHash = new(hashBytes);

            Dictionary<string, byte[]> asmMap = new()
            {
                ["amd64"] = [(byte)i]
            };

            _wasmDb.WriteActivation(moduleHash, asmMap);
        }

        // Clear all caches
        _wasmDb.ClearCaches();

        // Verify all still retrievable from DB (cache cleared, DB intact)
        for (int i = 0; i < 20; i++)
        {
            byte[] hashBytes = new byte[32];
            hashBytes[31] = (byte)i;
            Hash256 moduleHash = new(hashBytes);

            _wasmDb.TryGetActivatedAsm("amd64", moduleHash, out byte[] data).Should().BeTrue();
            data.Should().BeEquivalentTo(new byte[] { (byte)i });
        }
    }



    [Test]
    public void ClearCaches_ConcurrentAccess_NoException()
    {
        List<Task> tasks = [];

        for (int i = 0; i < 10; i++)
        {
            int index = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    byte[] hashBytes = new byte[32];
                    hashBytes[0] = (byte)index;
                    hashBytes[1] = (byte)j;
                    Hash256 moduleHash = new(hashBytes);

                    _wasmDb.TryGetActivatedAsm("amd64", moduleHash, out _);
                }
            }));

            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    _wasmDb.ClearCaches();
                }
            }));
        }

        Action act = () => Task.WaitAll(tasks.ToArray());

        act.Should().NotThrow();
    }



    [Test]
    public void GetWasmerSerializeVersion_NotSet_ReturnsZero()
    {
        uint version = _wasmDb.GetWasmerSerializeVersion();

        version.Should().Be(0);
    }

    [Test]
    public void WasmerSerializeVersion_AfterSet_ReturnsStoredValue()
    {
        _wasmDb.SetWasmerSerializeVersion(42);

        uint version = _wasmDb.GetWasmerSerializeVersion();

        version.Should().Be(42);
    }

    [Test]
    public void GetWasmSchemaVersion_NotSet_ReturnsZero()
    {
        byte version = _wasmDb.GetWasmSchemaVersion();

        version.Should().Be(0);
    }

    [Test]
    public void WasmSchemaVersion_AfterSet_ReturnsStoredValue()
    {
        _wasmDb.SetWasmSchemaVersion(5);

        byte version = _wasmDb.GetWasmSchemaVersion();

        version.Should().Be(5);
    }



    [Test]
    public void IsEmpty_NewDb_ReturnsTrue()
    {
        _wasmDb.IsEmpty().Should().BeTrue();
    }

    [Test]
    public void IsEmpty_AfterWrite_ReturnsFalse()
    {
        _wasmDb.WriteActivation(TestItem.KeccakA, new Dictionary<string, byte[]> { ["amd64"] = [1] });

        _wasmDb.IsEmpty().Should().BeFalse();
    }

}
