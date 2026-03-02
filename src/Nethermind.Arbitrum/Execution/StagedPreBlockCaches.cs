// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;
using System.Collections.Concurrent;
using CollectionExtensions = Nethermind.Core.Collections.CollectionExtensions;

namespace Nethermind.Arbitrum.Execution;

public interface IStagedPreBlockCaches : IPreBlockCachesInner
{
    Account? AddOrUpdate(in AddressAsKey key, Account newValue, Func<AddressAsKey, Account?, Account?> updateFunc);
    bool TryRemove(AddressAsKey key, out Account? account);

    byte[] AddOrUpdate(in StorageCell key, byte[] newValue, Func<StorageCell, byte[], byte[]> updateFunc);

    public void Seal();
    public ulong StageId { get; }
}
public class SealablePreBlockCaches : IStagedPreBlockCaches
{
    private const int InitialCapacity = 4096 * 8;
    private static int LockPartitions => CollectionExtensions.LockPartitions;

    private bool _backgroundSealed;
    private readonly ILogger _logger;

    private readonly ulong _stageId;

    private readonly ConcurrentDictionary<StorageCell, byte[]> _storageCache = new(LockPartitions, InitialCapacity);
    private readonly ConcurrentDictionary<AddressAsKey, Account?> _stateCache = new(LockPartitions, InitialCapacity);
    private readonly ConcurrentDictionary<NodeKey, byte[]?> _rlpCache = new(LockPartitions, InitialCapacity);
    private readonly ConcurrentDictionary<PreBlockCaches.PrecompileCacheKey, Result<byte[]>> _precompileCache = new(LockPartitions, InitialCapacity);

    public SealablePreBlockCaches(ILogger logger, ulong stageId)
    {
        _logger = logger;
        _stageId = stageId;
    }

    public ulong StageId => _stageId;

    public ConcurrentDictionary<StorageCell, byte[]> StorageCache => _storageCache;
    public ConcurrentDictionary<AddressAsKey, Account?> StateCache => _stateCache;
    public ConcurrentDictionary<NodeKey, byte[]?> RlpCache => _rlpCache;
    public ConcurrentDictionary<PreBlockCaches.PrecompileCacheKey, Result<byte[]>> PrecompileCache => _precompileCache;

    public CacheType ClearCaches()
    {
        return CacheType.None;
    }


    public Account? GetOrAdd(in AddressAsKey key, InFactory<AddressAsKey, Account> factory)
    {
        if (Volatile.Read(ref _backgroundSealed))
        {
            _logger.Debug($"{_stageId} GetOrAdd for sealed {key}");
            return _stateCache.TryGetValue(key, out Account? account) ? account : null;
        }
        return _stateCache.GetOrAdd(key, (asKey) => factory(in asKey));
    }

    public Account? AddOrUpdate(in AddressAsKey key, Account newValue, Func<AddressAsKey, Account?, Account?> updateFunc)
    {
        if (!Volatile.Read(ref _backgroundSealed))
        {
            _logger.Debug($"{_stageId} AddOrUpdate not sealed cache {key}");
        }
        return _stateCache.AddOrUpdate(key, newValue, updateFunc);
    }

    public bool TryGetValue(AddressAsKey key, out Account? account)
    {
        if (!Volatile.Read(ref _backgroundSealed))
        {
            _logger.Debug($"{_stageId} Read from not sealed cache {key}");
        }
        return _stateCache.TryGetValue(key, out account);
    }

    public bool TryRemove(AddressAsKey key, out Account? account)
    {
        if (!Volatile.Read(ref _backgroundSealed))
        {
            _logger.Debug($"{_stageId} TryRemove not sealed cache {key}");
        }
        _logger.Debug($"{_stageId} TryRemove {key}");
        return _stateCache.TryRemove(key, out account);
    }

    public byte[] GetOrAdd(in StorageCell key, InFactory<StorageCell, byte[]> factory)
    {
        if (Volatile.Read(ref _backgroundSealed))
        {
            _logger.Debug($"{_stageId} - GetOrAdd for sealed {key}");
            return !_storageCache.TryGetValue(key, out byte[]? data) ? [0] : data;
        }
        return _storageCache.GetOrAdd(key, (cell) => factory(in cell) ?? [0]);
    }

    public bool TryGetValue(in StorageCell key, out byte[] data)
    {
        if (!Volatile.Read(ref _backgroundSealed))
        {
            _logger.Debug($"{_stageId} Read from not sealed cache {key}");
        }
        return _storageCache.TryGetValue(key, out data!);
    }

    public byte[] AddOrUpdate(in StorageCell key, byte[] newValue, Func<StorageCell, byte[], byte[]> updateFunc)
    {
        if (!Volatile.Read(ref _backgroundSealed))
        {
            _logger.Debug($"{_stageId} AddOrUpdate not sealed cache {key}");
        }
        return _storageCache.AddOrUpdate(key, newValue, updateFunc);
    }

    public void Seal()
    {
        Volatile.Write(ref _backgroundSealed, true);
    }
}
public class StagedPreBlockCaches : IPreBlockCachesWrapper
{
    private volatile IStagedPreBlockCaches _active;
    private IStagedPreBlockCaches? _next;
    private readonly ILogger _logger;
    private ulong _nextStageId = 0;

    public StagedPreBlockCaches(ILogManager logManager)
    {
        _logger = logManager.GetClassLogger();

        SealablePreBlockCaches initial = new(_logger, _nextStageId++);
        initial.Seal();
        _active = initial;
    }

    public IPreBlockCachesInner Active => _active;

    public IPreBlockCachesInner? Next => _next;

    public IPreBlockCachesInner CreateNext()
    {
        _next = new SealablePreBlockCaches(_logger, _nextStageId++);
        if (_logger.IsDebug)
            _logger.Debug($"Next is {_next.StageId}");
        return _next;
    }

    public void Promote()
    {
        IStagedPreBlockCaches? next = _next;
        if (next == null)
            throw new InvalidOperationException("Next stage not created.");
        next.Seal();
        _active = next;
        _next = null;
    }
}
