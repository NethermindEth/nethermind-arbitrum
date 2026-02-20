// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Metric;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Principal;

namespace Nethermind.Arbitrum.Execution;

public class ArbitrumPrewarmerScopeProvider(
    IWorldStateScopeProvider baseProvider,
    IPreBlockCachesWrapper preBlockCachesWrapper,
    bool populatePreBlockCache = true,
    ILogManager? logManager = null)
    : IWorldStateScopeProvider, IPreBlockCaches
{
    public bool HasRoot(BlockHeader? baseBlock) => baseProvider.HasRoot(baseBlock);

    public IWorldStateScopeProvider.IScope BeginScope(BlockHeader? baseBlock) =>
        new ArbitrumScopeWrapper(baseProvider.BeginScope(baseBlock), preBlockCachesWrapper, populatePreBlockCache, logManager);

    public IPreBlockCachesInner Caches => preBlockCachesWrapper.Active;

    public bool IsWarmWorldState => !populatePreBlockCache;

    private sealed class ArbitrumScopeWrapper : IWorldStateScopeProvider.IScope
    {
        private readonly IWorldStateScopeProvider.IScope _baseScope;
        private readonly IPreBlockCachesInner? _preBlockCache;
        private readonly IPreBlockCachesWrapper _preBlockCacheWrapper;
        private readonly bool _populatePreBlockCache;
        private readonly ILogManager? _logManager;
        private readonly IMetricObserver _metricObserver = Db.Metrics.PrewarmerGetTime;
        private readonly bool _measureMetric = Db.Metrics.DetailedMetricsEnabled;
        private readonly PrewarmerGetTimeLabels _labels;
        private readonly object _lock = new();
        private readonly ILogger? _logger;

        public ArbitrumScopeWrapper(IWorldStateScopeProvider.IScope baseScope,
            IPreBlockCachesWrapper preBlockCachesWrapper,
            bool populatePreBlockCache,
            ILogManager? logManager = null)
        {
            _baseScope = baseScope;
            _preBlockCache = populatePreBlockCache ? preBlockCachesWrapper.Next : preBlockCachesWrapper.Active;
            //if (populatePreBlockCache && preBlockCachesWrapper.Next is null)
            //{
            //    int a = 10;
            //    a++;
            //}
            _populatePreBlockCache = populatePreBlockCache;
            _preBlockCacheWrapper = preBlockCachesWrapper;
            _logManager = logManager;
            _labels = populatePreBlockCache ? PrewarmerGetTimeLabels.Prewarmer : PrewarmerGetTimeLabels.NonPrewarmer;
            _logger = _logManager?.GetClassLogger<ArbitrumScopeWrapper>();
            _logger?.Debug(_preBlockCache is null ? $"Using null cache instance" : $"Using cache instance {((SealablePreBlockCaches)_preBlockCache)?.StageId}");
        }

        public void Dispose() => _baseScope.Dispose();

        public IWorldStateScopeProvider.ICodeDb CodeDb => _baseScope.CodeDb;

        public IWorldStateScopeProvider.IStorageTree CreateStorageTree(Address address)
        {
            return new StorageTreeWrapper(
                _baseScope.CreateStorageTree(address),
                _preBlockCache,
                address,
                _populatePreBlockCache,
                _lock,
                _logManager?.GetClassLogger<PrewarmerScopeProvider>());
        }

        public IWorldStateScopeProvider.IWorldStateWriteBatch StartWriteBatch(int estimatedAccountNum)
        {
            IWorldStateScopeProvider.IWorldStateWriteBatch innerWriteBatch =
                _baseScope.StartWriteBatch(estimatedAccountNum);

            // Write through to Next (the prewarm cache for the upcoming block) so that N's actual
            // state changes overwrite any stale N-1 prewarmed values while warming is still in flight.
            // Falls back to Active when no prewarm is running (e.g. MessageForPrefetch was null).
            IPreBlockCachesInner writeTarget = _preBlockCacheWrapper.Next ?? _preBlockCacheWrapper.Active;
            return new CacheCopyWorldStateWriteBatch(writeTarget, innerWriteBatch, _lock, _logManager?.GetClassLogger());
        }

        public void Commit(long blockNumber) => _baseScope.Commit(blockNumber);

        public Hash256 RootHash => _baseScope.RootHash;

        public void UpdateRootHash()
        {
            if (!_measureMetric)
            {
                _baseScope.UpdateRootHash();
                return;
            }

            long sw = Stopwatch.GetTimestamp();
            _baseScope.UpdateRootHash();
            _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.UpdateRootHash);
        }

        public Account? Get(Address address)
        {
            AddressAsKey addressAsKey = address;
            long sw = _measureMetric ? Stopwatch.GetTimestamp() : 0;
            //ConcurrentDictionary<AddressAsKey, Account> preBlockCache = _preBlockCaches.GetStateCache(_populatePreBlockCache);

            if (_populatePreBlockCache && _preBlockCache is not null)
            {
                long priorReads = Db.Metrics.ThreadLocalStateTreeReads;
                Account? account = _preBlockCache.GetOrAdd(addressAsKey, GetFromBaseTree!);
                //lock (_lock)
                //{
                //    account = preBlockCache.GetOrAdd(addressAsKey, GetFromBaseTree!);
                //}

                if (Db.Metrics.ThreadLocalStateTreeReads == priorReads)
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressHit);
                    Db.Metrics.IncrementStateTreeCacheHits();
                    _logger?.Debug($"{Environment.CurrentManagedThreadId} Populate for {address} -> {account} - hit");
                }
                else
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressMiss);
                    _logger?.Debug($"{Environment.CurrentManagedThreadId} Populate for {address} -> {account} - miss");
                }

                return account;
            }
            else
            {
                if (_preBlockCache?.TryGetValue(addressAsKey, out Account? account) == true)
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressHit);
                    _baseScope.HintGet(address, account);
                    Db.Metrics.IncrementStateTreeCacheHits();

                    _logger?.Debug($"{Environment.CurrentManagedThreadId} Reading cache hit {address} -> {account}");
                }
                else
                {
                    account = GetFromBaseTree(addressAsKey);
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressMiss);
                    _logger?.Debug($"{Environment.CurrentManagedThreadId} Reading cache miss {address} -> {account}");
                }
                return account;
            }
        }

        public void HintGet(Address address, Account? account) => _baseScope.HintGet(address, account);

        private Account? GetFromBaseTree(AddressAsKey address)
        {
            return _baseScope.Get(address);
        }
    }

    private sealed class StorageTreeWrapper : IWorldStateScopeProvider.IStorageTree
    {
        private readonly IWorldStateScopeProvider.IStorageTree baseStorageTree;
        private readonly IPreBlockCachesInner? preBlockCache;
        private readonly Address address;
        private readonly bool populatePreBlockCache;
        private readonly ILogger? _logger;
        //private readonly SeqlockCache<StorageCell, byte[]>.ValueFactory _loadFromTreeStorage;
        private readonly IMetricObserver _metricObserver = Db.Metrics.PrewarmerGetTime;
        private readonly bool _measureMetric = Db.Metrics.DetailedMetricsEnabled;
        private readonly PrewarmerGetTimeLabels _labels;
        private readonly object _lock;

        public StorageTreeWrapper(
            IWorldStateScopeProvider.IStorageTree baseStorageTree,
            IPreBlockCachesInner? preBlockCache,
            Address address,
            bool populatePreBlockCache,
            object @lock,
            ILogger? logger)
        {
            this.baseStorageTree = baseStorageTree;
            this.preBlockCache = preBlockCache;
            this.address = address;
            this.populatePreBlockCache = populatePreBlockCache;
            _logger = logger;
            _labels = populatePreBlockCache ? PrewarmerGetTimeLabels.Prewarmer : PrewarmerGetTimeLabels.NonPrewarmer;
            //_loadFromTreeStorage = LoadFromTreeStorage;
            _lock = @lock;
        }

        public Hash256 RootHash => baseStorageTree.RootHash;

        public byte[] Get(in UInt256 index)
        {
            StorageCell
                storageCell = new StorageCell(address, in index); // TODO: Make the dictionary use UInt256 directly
            long sw = _measureMetric ? Stopwatch.GetTimestamp() : 0;

            if (populatePreBlockCache && preBlockCache is not null)
            {
                long priorReads = Db.Metrics.ThreadLocalStorageTreeReads;

                byte[]? value = preBlockCache.GetOrAdd(storageCell, LoadFromTreeStorage);

                if (Db.Metrics.ThreadLocalStorageTreeReads == priorReads)
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.SlotGetHit);
                    // Read from Concurrent Cache
                    Db.Metrics.IncrementStorageTreeCache();

                    _logger?.Debug(
                        $"{Environment.CurrentManagedThreadId} Populate for {storageCell} -> {value?.ToHexString()} - hit");
                }
                else
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.SlotGetMiss);
                    _logger?.Debug(
                        $"{Environment.CurrentManagedThreadId} Populate for {storageCell} -> {value?.ToHexString()} - miss");
                }

                return value ?? [0];
            }
            else
            {
                if (preBlockCache?.TryGetValue(storageCell, out byte[]? value) == true)
                {
                    _logger?.Debug(
                        $"{Environment.CurrentManagedThreadId} Reading cache hit for {storageCell} -> {value?.ToHexString()}");

                    baseStorageTree.HintGet(index, value);
                    Db.Metrics.IncrementStorageTreeCache();
                }
                else
                {
                    value = LoadFromTreeStorage(storageCell);

                    _logger?.Debug(
                        $"{Environment.CurrentManagedThreadId} Reading cache miss for {storageCell} -> {value.ToHexString()}");

                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.SlotGetMiss);
                }

                return value ?? [0];
            }
        }

        public void HintGet(in UInt256 index, byte[]? value) => baseStorageTree.HintGet(in index, value);

        private byte[] LoadFromTreeStorage(StorageCell storageCell)
        {
            Db.Metrics.IncrementStorageTreeReads();

            return !storageCell.IsHash
                ? baseStorageTree.Get(storageCell.Index)
                : baseStorageTree.Get(storageCell.Hash);
        }

        public byte[] Get(in ValueHash256 hash) =>
            // Not a critical path. so we just forward for simplicity
            baseStorageTree.Get(in hash);
    }

    public class CacheCopyWorldStateWriteBatch : IWorldStateScopeProvider.IWorldStateWriteBatch
    {
        //private IPreBlockCachesWrapper _caches;
        private IPreBlockCachesInner _stateCache;
        private IWorldStateScopeProvider.IWorldStateWriteBatch _baseBatch;
        private readonly ILogger? _logger;
        private readonly object _lock;

        public CacheCopyWorldStateWriteBatch(IPreBlockCachesInner caches, IWorldStateScopeProvider.IWorldStateWriteBatch baseBatch, object @lock, ILogger? logger)
        {
            //_caches = caches;
            //_stateCache = _caches.GetStateCache();
            _stateCache = caches;
            _baseBatch = baseBatch;
            _logger = logger;
            _baseBatch.OnAccountUpdated += _baseBatch_OnAccountUpdated;
            _lock = @lock;
        }

        private void _baseBatch_OnAccountUpdated(object? sender, IWorldStateScopeProvider.AccountUpdated e)
        {
            //_logger?.Debug($"_baseBatch_OnAccountUpdated {e.Address} -> {e.Account}");

            //if (_stateCache.TryGetValue(e.Address, out var existing))
            //{
            //    _logger?.Debug($"Update cache on write {e.Address} -> {e.Account}");
            //    _stateCache.Set(e.Address, e.Account);
            //}
            Set(e.Address, e.Account);

            OnAccountUpdated?.Invoke(this, e);
        }

        public void Dispose()
        {
            _baseBatch.Dispose();
        }

        public event EventHandler<IWorldStateScopeProvider.AccountUpdated>? OnAccountUpdated;

        public void Set(Address key, Account? account)
        {
            _baseBatch.Set(key, account);
            //_caches.StateCache[key] = account ?? Account.TotallyEmpty;
            //_logger?.Debug($"Writing {key} -> {account}");
            //if (_stateCache.TryGetValue(key, out var existing))
            //{
            //    _logger?.Debug($"Update cache on write {key} -> {account}");
            //    _stateCache.Set(key, account);
            //}
            SealablePreBlockCaches s_cache = (SealablePreBlockCaches)_stateCache;
            _logger?.Debug($"Update cache {s_cache.StageId} on write {key} -> {account}");
            //lock (_lock)
            //{
            //    //_stateCache.Set(key, account);
            //    if (account is not null)
            //        _stateCache.AddOrUpdate(key, account, (_, _) => account);
            //}
            if (account is not null)
                _stateCache.AddOrUpdate(key, account, (_, _) => account);
            else
                _stateCache.TryRemove(key, out _);
        }

        public IWorldStateScopeProvider.IStorageWriteBatch CreateStorageWriteBatch(Address key, int estimatedEntries)
        {
            IWorldStateScopeProvider.IStorageWriteBatch innerBatch =
                _baseBatch.CreateStorageWriteBatch(key, estimatedEntries);

            return new CacheCopyStorageWriteBatch(_stateCache, innerBatch, key, _lock, _logger);
        }
    }

    public class CacheCopyStorageWriteBatch : IWorldStateScopeProvider.IStorageWriteBatch
    {
        private IPreBlockCachesInner _cache;
        private IWorldStateScopeProvider.IStorageWriteBatch _baseBatch;
        private readonly AddressAsKey _address;
        private readonly ILogger? _logger;
        private readonly object _lock;

        public CacheCopyStorageWriteBatch(IPreBlockCachesInner cache, IWorldStateScopeProvider.IStorageWriteBatch baseBatch, AddressAsKey address, object @lock, ILogger? logger)
        {
            _cache = cache;
            _baseBatch = baseBatch;
            _address = address;
            _logger = logger;
            _lock = @lock;
        }

        public void Dispose()
        {
            _baseBatch.Dispose();
        }

        public void Set(in UInt256 index, byte[] value)
        {
            _baseBatch.Set(index, value);
            //_caches.StorageCache[new StorageCell(_address, in index)] = value;

            var key = new StorageCell(_address, in index);
            //_logger?.Debug($"Writing {key} -> {value.ToHexString()}");
            //if (_cache.TryGetValue(key, out var existing))
            //{
            //    _logger?.Debug($"Update cache on write {key} -> {value.ToHexString()}");
            //    _cache.Set(key, value);
            //}
            SealablePreBlockCaches s_cache = (SealablePreBlockCaches)_cache;

            _logger?.Debug($"Update cache {s_cache.StageId} on write {key} -> {value.ToHexString()}");
            //lock (_lock)
            //{
            //    //_cache.Set(key, value);
            //    _cache.AddOrUpdate(key, value, (_, _) => value);
            //}

            _cache.AddOrUpdate(key, value, (_, _) => value);
        }

        public void Clear()
        {
            _baseBatch.Clear();
        }
    }
}
