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
using System.Diagnostics;

namespace Nethermind.Arbitrum.Execution;

public class ArbitrumPrewarmerScopeProvider(
    IWorldStateScopeProvider baseProvider,
    IPreBlockCachesInner preBlockCaches,
    bool populatePreBlockCache = true,
    ILogManager? logManager = null)
    : IWorldStateScopeProvider, IPreBlockCaches
{
    public bool HasRoot(BlockHeader? baseBlock) => baseProvider.HasRoot(baseBlock);

    public IWorldStateScopeProvider.IScope BeginScope(BlockHeader? baseBlock) =>
        new ArbitrumScopeWrapper(baseProvider.BeginScope(baseBlock), preBlockCaches, populatePreBlockCache, logManager);

    public IPreBlockCachesInner Caches => preBlockCaches;

    public bool IsWarmWorldState => !populatePreBlockCache;

    private sealed class ArbitrumScopeWrapper : IWorldStateScopeProvider.IScope
    {
        private readonly IWorldStateScopeProvider.IScope _baseScope;
        private readonly IPreBlockCachesInner _preBlockCaches;
        private readonly bool _populatePreBlockCache;
        private readonly ILogManager? _logManager;
        private readonly IMetricObserver _metricObserver = Db.Metrics.PrewarmerGetTime;
        private readonly bool _measureMetric = Db.Metrics.DetailedMetricsEnabled;
        private readonly PrewarmerGetTimeLabels _labels;

        public ArbitrumScopeWrapper(IWorldStateScopeProvider.IScope baseScope,
            IPreBlockCachesInner preBlockCaches,
            bool populatePreBlockCache,
            ILogManager? logManager = null)
        {
            _baseScope = baseScope;
            _preBlockCaches = preBlockCaches;
            _populatePreBlockCache = populatePreBlockCache;
            _logManager = logManager;
            _labels = populatePreBlockCache ? PrewarmerGetTimeLabels.Prewarmer : PrewarmerGetTimeLabels.NonPrewarmer;
        }

        public void Dispose() => _baseScope.Dispose();

        public IWorldStateScopeProvider.ICodeDb CodeDb => _baseScope.CodeDb;

        public IWorldStateScopeProvider.IStorageTree CreateStorageTree(Address address)
        {
            SeqlockCache<StorageCell, byte[]> cacheInstance = _preBlockCaches.GetStorageCache(_populatePreBlockCache);

            return new StorageTreeWrapper(
                _baseScope.CreateStorageTree(address),
                cacheInstance,
                address,
                _populatePreBlockCache,
                _logManager?.GetClassLogger<PrewarmerScopeProvider>());
        }

        public IWorldStateScopeProvider.IWorldStateWriteBatch StartWriteBatch(int estimatedAccountNum)
        {
            IWorldStateScopeProvider.IWorldStateWriteBatch innerWriteBatch =
                _baseScope.StartWriteBatch(estimatedAccountNum);

            return new CacheCopyWorlStateWriteBatch(_preBlockCaches, innerWriteBatch, _logManager?.GetClassLogger());
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
            ILogger? logger = _logManager?.GetClassLogger<PrewarmerScopeProvider>();

            AddressAsKey addressAsKey = address;
            long sw = _measureMetric ? Stopwatch.GetTimestamp() : 0;
            SeqlockCache<AddressAsKey, Account>? preBlockCache = _preBlockCaches.GetStateCache(_populatePreBlockCache);

            if (_populatePreBlockCache)
            {
                long priorReads = Db.Metrics.ThreadLocalStateTreeReads;
                Account? account = preBlockCache.GetOrAdd(in addressAsKey, GetFromBaseTree);

                if (Db.Metrics.ThreadLocalStateTreeReads == priorReads)
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressHit);
                    Db.Metrics.IncrementStateTreeCacheHits();
                    logger?.Debug($"{Environment.CurrentManagedThreadId} Populate for {address} -> {account} - hit");
                }
                else
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressMiss);
                    logger?.Debug($"{Environment.CurrentManagedThreadId} Populate for {address} -> {account} - miss");
                }

                return account;
            }
            else
            {
                if (preBlockCache.TryGetValue(in addressAsKey, out Account? account))
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressHit);
                    _baseScope.HintGet(address, account);
                    Db.Metrics.IncrementStateTreeCacheHits();

                    logger?.Debug($"{Environment.CurrentManagedThreadId} Reading cache hit {address} -> {account}");
                }
                else
                {
                    account = GetFromBaseTree(in addressAsKey);
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressMiss);
                    logger?.Debug($"{Environment.CurrentManagedThreadId} Reading cache miss {address} -> {account}");
                }
                return account;
            }
        }

        public void HintGet(Address address, Account? account) => _baseScope.HintGet(address, account);

        private Account? GetFromBaseTree(in AddressAsKey address)
        {
            return _baseScope.Get(address);
        }
    }

    private sealed class StorageTreeWrapper : IWorldStateScopeProvider.IStorageTree
    {
        private readonly IWorldStateScopeProvider.IStorageTree baseStorageTree;
        private readonly SeqlockCache<StorageCell, byte[]> preBlockCache;
        private readonly Address address;
        private readonly bool populatePreBlockCache;
        private readonly ILogger? _logger;
        private readonly SeqlockCache<StorageCell, byte[]>.ValueFactory _loadFromTreeStorage;
        private readonly IMetricObserver _metricObserver = Db.Metrics.PrewarmerGetTime;
        private readonly bool _measureMetric = Db.Metrics.DetailedMetricsEnabled;
        private readonly PrewarmerGetTimeLabels _labels;

        public StorageTreeWrapper(
            IWorldStateScopeProvider.IStorageTree baseStorageTree,
            SeqlockCache<StorageCell, byte[]> preBlockCache,
            Address address,
            bool populatePreBlockCache,
            ILogger? logger)
        {
            this.baseStorageTree = baseStorageTree;
            this.preBlockCache = preBlockCache;
            this.address = address;
            this.populatePreBlockCache = populatePreBlockCache;
            _logger = logger;
            _labels = populatePreBlockCache ? PrewarmerGetTimeLabels.Prewarmer : PrewarmerGetTimeLabels.NonPrewarmer;
            _loadFromTreeStorage = LoadFromTreeStorage;
        }

        public Hash256 RootHash => baseStorageTree.RootHash;

        public byte[] Get(in UInt256 index)
        {
            StorageCell
                storageCell = new StorageCell(address, in index); // TODO: Make the dictionary use UInt256 directly
            long sw = _measureMetric ? Stopwatch.GetTimestamp() : 0;

            if (populatePreBlockCache)
            {
                long priorReads = Db.Metrics.ThreadLocalStorageTreeReads;

                byte[]? value = preBlockCache.GetOrAdd(in storageCell, _loadFromTreeStorage);

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

                return value ?? [];
            }
            else
            {
                if (preBlockCache.TryGetValue(in storageCell, out byte[]? value))
                {
                    _logger?.Debug(
                        $"{Environment.CurrentManagedThreadId} Reading cache hit for {storageCell} -> {value?.ToHexString()}");

                    baseStorageTree.HintGet(index, value);
                    Db.Metrics.IncrementStorageTreeCache();
                }
                else
                {
                    value = LoadFromTreeStorage(in storageCell);

                    _logger?.Debug(
                        $"{Environment.CurrentManagedThreadId} Reading cache miss for {storageCell} -> {value.ToHexString()}");

                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.SlotGetMiss);
                }

                return value ?? [];
            }
        }

        public void HintGet(in UInt256 index, byte[]? value) => baseStorageTree.HintGet(in index, value);

        private byte[] LoadFromTreeStorage(in StorageCell storageCell)
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

    public class CacheCopyWorlStateWriteBatch : IWorldStateScopeProvider.IWorldStateWriteBatch
    {
        private IPreBlockCachesInner _caches;
        private SeqlockCache<AddressAsKey, Account> _stateCache;
        private IWorldStateScopeProvider.IWorldStateWriteBatch _baseBatch;
        private readonly ILogger? _logger;

        public CacheCopyWorlStateWriteBatch(IPreBlockCachesInner caches, IWorldStateScopeProvider.IWorldStateWriteBatch baseBatch, ILogger? logger)
        {
            _caches = caches;
            _stateCache = _caches.GetStateCache();
            _baseBatch = baseBatch;
            _logger = logger;
            _baseBatch.OnAccountUpdated += _baseBatch_OnAccountUpdated;
        }

        private void _baseBatch_OnAccountUpdated(object? sender, IWorldStateScopeProvider.AccountUpdated e)
        {
            _logger?.Debug($"_baseBatch_OnAccountUpdated {e.Address} -> {e.Account}");

            if (_stateCache.TryGetValue(e.Address, out var existing))
            {
                _logger?.Debug($"Update cache on write {e.Address} -> {e.Account}");
                _stateCache.Set(e.Address, e.Account);
            }

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
            _logger?.Debug($"Writing {key} -> {account}");
            if (_stateCache.TryGetValue(key, out var existing))
            {
                _logger?.Debug($"Update cache on write {key} -> {account}");
                _stateCache.Set(key, account);
            }
        }

        public IWorldStateScopeProvider.IStorageWriteBatch CreateStorageWriteBatch(Address key, int estimatedEntries)
        {
            IWorldStateScopeProvider.IStorageWriteBatch innerBatch =
                _baseBatch.CreateStorageWriteBatch(key, estimatedEntries);

            return new CacheCopyStorageWriteBatch(_caches.GetStorageCache(), innerBatch, key, _logger);
        }
    }

    public class CacheCopyStorageWriteBatch : IWorldStateScopeProvider.IStorageWriteBatch
    {
        private SeqlockCache<StorageCell, byte[]> _cache;
        private IWorldStateScopeProvider.IStorageWriteBatch _baseBatch;
        private readonly AddressAsKey _address;
        private readonly ILogger? _logger;

        public CacheCopyStorageWriteBatch(SeqlockCache<StorageCell, byte[]> cache, IWorldStateScopeProvider.IStorageWriteBatch baseBatch, AddressAsKey address, ILogger? logger)
        {
            _cache = cache;
            _baseBatch = baseBatch;
            _address = address;
            _logger = logger;
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
            _logger?.Debug($"Writing {key} -> {value.ToHexString()}");
            if (_cache.TryGetValue(key, out var existing))
            {
                _logger?.Debug($"Update cache on write {key} -> {value.ToHexString()}");
                _cache.Set(key, value);
            }
        }

        public void Clear()
        {
            _baseBatch.Clear();
        }
    }
}
