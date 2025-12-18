// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Autofac.Features.AttributeFilters;
using Nethermind.Core;
using Nethermind.Core.Caching;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Db;

namespace Nethermind.Arbitrum.Stylus;

/// <summary>
/// Database implementation for WASM code storage with sharded LRU caching.
/// Thread-safe implementation using cache sharding pattern from TxPool.AccountCache for maximum parallelism.
/// </summary>
public sealed class WasmDb : IWasmDb
{
    public const string DbName = "wasm";

    // Cache sizing based on Arbitrum Stylus WASM constraints:
    // - Uncompressed WASM max size: 128KB (hard limit, see https://docs.arbitrum.io/stylus/how-tos/optimizing-binaries)
    // - Typical WASM size: ~20KB average
    //
    // Our sizing: 512 total entries (32 per shard × 16 shards)
    // - Worst case memory: 512 entries × 128KB = 64MB
    private const int CacheShardCount = 16;
    private const int EntriesPerShard = 32;

    private readonly IDb _db;

    private readonly ClockCache<ActivatedKey, byte[]>[] _activatedCodeCaches;

    /// <summary>
    /// Cache key optimized for minimal allocations and fast equality comparison.
    /// </summary>
    private readonly record struct ActivatedKey(string Target, ValueHash256 ModuleHash);

    public WasmDb([KeyFilter(DbName)] IDb db)
    {
        _db = db;

        _activatedCodeCaches = new ClockCache<ActivatedKey, byte[]>[CacheShardCount];
        for (int i = 0; i < CacheShardCount; i++)
        {
            _activatedCodeCaches[i] = new ClockCache<ActivatedKey, byte[]>(EntriesPerShard);
        }
    }

    /// <summary>
    /// High-performance retrieval with sharded cache-first strategy.
    /// Hot path: cache hit returns immediately without contention from other shards.
    /// Cold path: DB read with automatic cache population.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetActivatedAsm(string target, in ValueHash256 moduleHash, out byte[] bytes)
    {
        ActivatedKey cacheKey = new(target, moduleHash);
        ClockCache<ActivatedKey, byte[]> cache = GetCacheForKey(cacheKey);

        if (cache.TryGet(cacheKey, out bytes!))
        {
            return true;
        }

        byte[] dbKey = WasmStoreSchema.GetActivatedKey(target, moduleHash);
        byte[]? asm = _db.Get(dbKey.AsSpan());

        if (asm is not null)
        {
            cache.Set(cacheKey, asm);
            bytes = asm;
            return true;
        }

        bytes = Array.Empty<byte>();
        return false;
    }

    /// <summary>
    /// Writes activated assemblies with cache-write-through strategy.
    /// Database writes occur first to ensure durability, then caches are updated.
    /// </summary>
    public void WriteActivation(in ValueHash256 moduleHash, IReadOnlyDictionary<string, byte[]> asmMap)
    {
        foreach ((string target, byte[] asm) in asmMap)
        {
            byte[] key = WasmStoreSchema.GetActivatedKey(target, in moduleHash);
            _db.Set(key, asm);
        }

        foreach ((string target, byte[] asm) in asmMap)
        {
            ActivatedKey cacheKey = new(target, moduleHash);
            ClockCache<ActivatedKey, byte[]> cache = GetCacheForKey(cacheKey);
            cache.Set(cacheKey, asm);
        }
    }

    /// <summary>
    /// Optimized batch write with single transaction and sharded cache update.
    /// Minimizes DB round trips and updates only relevant cache shards.
    /// </summary>
    public void WriteAllActivations(IReadOnlyDictionary<Hash256AsKey, IReadOnlyDictionary<string, byte[]>> wasms)
    {
        using IWriteBatch batch = _db.StartWriteBatch();

        foreach ((Hash256AsKey moduleHash, IReadOnlyDictionary<string, byte[]> asmMap) in wasms)
        {
            foreach ((string target, byte[] asm) in asmMap)
            {
                byte[] key = WasmStoreSchema.GetActivatedKey(target, in moduleHash.Value.ValueHash256);
                batch.Set(key, asm);
            }
        }

        // Batch committed - now safe to update cache shards
        foreach ((Hash256AsKey moduleHash, IReadOnlyDictionary<string, byte[]> asmMap) in wasms)
        {
            foreach ((string target, byte[] asm) in asmMap)
            {
                ActivatedKey cacheKey = new(target, moduleHash.Value);
                ClockCache<ActivatedKey, byte[]> cache = GetCacheForKey(cacheKey);
                cache.Set(cacheKey, asm);
            }
        }
    }

    public bool IsEmpty() => !_db.GetAllValues().Any();

    /// <summary>
    /// Gets Wasmer serialization version. Read from DB each time as this is rarely called.
    /// </summary>
    public uint GetWasmerSerializeVersion()
    {
        byte[]? value = _db.Get(WasmStoreSchema.WasmerSerializeVersionKey.Span);
        return value is null ? 0 : BinaryPrimitives.ReadUInt32BigEndian(value);
    }

    /// <summary>
    /// Sets Wasmer serialization version.
    /// </summary>
    public void SetWasmerSerializeVersion(uint version)
    {
        Span<byte> buffer = stackalloc byte[32];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, version);
        _db.PutSpan(WasmStoreSchema.WasmerSerializeVersionKey.Span, buffer);
    }

    /// <summary>
    /// Gets WASM schema version. Read from DB each time as this is rarely called.
    /// </summary>
    public byte GetWasmSchemaVersion()
    {
        byte[]? value = _db.Get(WasmStoreSchema.WasmSchemaVersionKey.Span);
        return value?[0] ?? 0;
    }

    /// <summary>
    /// Sets WASM schema version.
    /// </summary>
    public void SetWasmSchemaVersion(byte version)
    {
        _db.PutSpan(WasmStoreSchema.WasmSchemaVersionKey.Span, [version]);
    }

    /// <summary>
    /// Deletes WASM entries matching given prefixes.
    /// Clears all cache shards after deletion to maintain consistency.
    /// Uses parallel clear pattern from TxPool.AccountCache.RemoveAccounts.
    /// </summary>
    public DeleteWasmResult DeleteWasmEntries(IReadOnlyList<ReadOnlyMemory<byte>> prefixes, int? expectedKeyLength = null)
    {
        int deletedCount = 0;
        int keyLengthMismatchCount = 0;

        foreach (byte[] key in _db.GetAllKeys())
        {
            if (key.Length == 0) continue;

            bool shouldDelete = false;
            foreach (ReadOnlyMemory<byte> prefix in prefixes)
            {
                if (key.Length < prefix.Length)
                    continue;

                if (Bytes.AreEqual(prefix.Span, key.AsSpan(0, prefix.Length)))
                {
                    if (expectedKeyLength.HasValue && key.Length != expectedKeyLength.Value)
                    {
                        keyLengthMismatchCount++;
                        break;
                    }

                    shouldDelete = true;
                    break;
                }
            }

            if (shouldDelete)
            {
                _db.Remove(key);
                deletedCount++;
            }
        }

        // Clear all cache shards after deletion to maintain consistency
        // Parallel clear pattern from TxPool.AccountCache
        if (deletedCount > 0)
        {
            Parallel.For(0, CacheShardCount, i => _activatedCodeCaches[i].Clear());
        }

        return new DeleteWasmResult(deletedCount, keyLengthMismatchCount);
    }

    public byte[]? Get(ReadOnlySpan<byte> key) => _db[key.ToArray()];

    public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value) => _db[key.ToArray()] = value.ToArray();

    public Hash256? GetRebuildingPosition()
    {
        byte[]? data = Get(WasmStoreSchema.RebuildingPositionKey);
        return data?.Length == 32 ? new Hash256(data) : null;
    }

    public void SetRebuildingPosition(Hash256 position) =>
        Set(WasmStoreSchema.RebuildingPositionKey, position.Bytes);

    public Hash256? GetRebuildingStartBlockHash()
    {
        byte[]? data = Get(WasmStoreSchema.RebuildingStartBlockHashKey);
        return data?.Length == 32 ? new Hash256(data) : null;
    }

    public void SetRebuildingStartBlockHash(Hash256 blockHash) =>
        Set(WasmStoreSchema.RebuildingStartBlockHashKey, blockHash.Bytes.ToArray());

    /// <summary>
    /// Selects cache shard based on hash's last nibble for even distribution.
    /// Pattern from TxPool.AccountCache.GetCacheIndex for zero-allocation shard selection.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ClockCache<ActivatedKey, byte[]> GetCacheForKey(in ActivatedKey key)
    {
        int shardIndex = key.ModuleHash.Bytes[^1] & 0xF;
        return _activatedCodeCaches[shardIndex];
    }
}
