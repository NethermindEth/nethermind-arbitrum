// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Core;
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
        private readonly IStagedPreBlockCaches? _preBlockCache;
        private readonly IPreBlockCachesWrapper _preBlockCacheWrapper;
        private readonly bool _populatePreBlockCache;
        private readonly ILogManager? _logManager;
        private readonly IMetricObserver _metricObserver = Db.Metrics.PrewarmerGetTime;
        private readonly bool _measureMetric = Db.Metrics.DetailedMetricsEnabled;
        private readonly PrewarmerGetTimeLabels _labels;
        private readonly ILogger? _logger;

        public ArbitrumScopeWrapper(IWorldStateScopeProvider.IScope baseScope,
            IPreBlockCachesWrapper preBlockCachesWrapper,
            bool populatePreBlockCache,
            ILogManager? logManager = null)
        {
            _baseScope = baseScope;
            _preBlockCache = populatePreBlockCache ? (IStagedPreBlockCaches?)preBlockCachesWrapper.Next : (IStagedPreBlockCaches)preBlockCachesWrapper.Active;
            _populatePreBlockCache = populatePreBlockCache;
            _preBlockCacheWrapper = preBlockCachesWrapper;
            _logManager = logManager;
            _labels = populatePreBlockCache ? PrewarmerGetTimeLabels.Prewarmer : PrewarmerGetTimeLabels.NonPrewarmer;
            _logger = _logManager?.GetClassLogger<ArbitrumScopeWrapper>();
            _logger?.Debug(_preBlockCache is null ? $"Using null cache instance for {populatePreBlockCache}" : $"Using cache instance {_preBlockCache?.StageId} for {populatePreBlockCache}");
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
                _logManager?.GetClassLogger<PrewarmerScopeProvider>());
        }

        public IWorldStateScopeProvider.IWorldStateWriteBatch StartWriteBatch(int estimatedAccountNum)
        {
            IWorldStateScopeProvider.IWorldStateWriteBatch innerWriteBatch =
                _baseScope.StartWriteBatch(estimatedAccountNum);

            return new CacheCopyWorldStateWriteBatch((IStagedPreBlockCaches)_preBlockCacheWrapper.Active, innerWriteBatch, _logManager?.GetClassLogger());
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

            if (_populatePreBlockCache && _preBlockCache is not null)
            {
                long priorReads = Db.Metrics.ThreadLocalStateTreeReads;
                Account? account = _preBlockCache.GetOrAdd(addressAsKey, GetFromBaseTree!);

                if (Db.Metrics.ThreadLocalStateTreeReads == priorReads)
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressHit);
                    Db.Metrics.IncrementStateTreeCacheHits();
                    if (_logger?.IsDebug == true)
                        _logger?.Debug($"{_preBlockCache.StageId} - Populate for {address} -> {account} - hit");
                }
                else
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressMiss);
                    if (_logger?.IsDebug == true)
                        _logger?.Debug($"{_preBlockCache.StageId} - Populate for {address} -> {account} - miss");
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

                    if (_logger?.IsDebug == true)
                        _logger?.Debug($"{_preBlockCache?.StageId} - Reading cache hit {address} -> {account}");
                }
                else
                {
                    account = GetFromBaseTree(addressAsKey);
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.AddressMiss);
                    if (_logger?.IsDebug == true)
                        _logger?.Debug($"{_preBlockCache?.StageId} - Reading cache miss {address} -> {account}");
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
        private readonly IWorldStateScopeProvider.IStorageTree _baseStorageTree;
        private readonly IStagedPreBlockCaches? _preBlockCache;
        private readonly Address _address;
        private readonly bool _populatePreBlockCache;
        private readonly ILogger? _logger;
        private readonly IMetricObserver _metricObserver = Db.Metrics.PrewarmerGetTime;
        private readonly bool _measureMetric = Db.Metrics.DetailedMetricsEnabled;
        private readonly PrewarmerGetTimeLabels _labels;

        public StorageTreeWrapper(
            IWorldStateScopeProvider.IStorageTree baseStorageTree,
            IStagedPreBlockCaches? preBlockCache,
            Address address,
            bool populatePreBlockCache,
            ILogger? logger)
        {
            _baseStorageTree = baseStorageTree;
            _preBlockCache = preBlockCache;
            _address = address;
            _populatePreBlockCache = populatePreBlockCache;
            _logger = logger;
            _labels = populatePreBlockCache ? PrewarmerGetTimeLabels.Prewarmer : PrewarmerGetTimeLabels.NonPrewarmer;
        }

        public Hash256 RootHash => _baseStorageTree.RootHash;

        public byte[] Get(in UInt256 index)
        {
            StorageCell
                storageCell = new StorageCell(_address, in index); // TODO: Make the dictionary use UInt256 directly
            long sw = _measureMetric ? Stopwatch.GetTimestamp() : 0;

            if (_populatePreBlockCache && _preBlockCache is not null)
            {
                long priorReads = Db.Metrics.ThreadLocalStorageTreeReads;

                byte[]? value = _preBlockCache.GetOrAdd(storageCell, LoadFromTreeStorage);

                if (Db.Metrics.ThreadLocalStorageTreeReads == priorReads)
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.SlotGetHit);
                    // Read from Concurrent Cache
                    Db.Metrics.IncrementStorageTreeCache();

                    if (_logger?.IsDebug == true)
                        _logger?.Debug($"{_preBlockCache.StageId} - Populate for {storageCell} -> {value?.ToHexString()} - hit");
                }
                else
                {
                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.SlotGetMiss);
                    _logger?.Debug(
                        $"{_preBlockCache.StageId} - Populate for {storageCell} -> {value?.ToHexString()} - miss");
                }

                return value ?? [0];
            }
            else
            {
                if (_preBlockCache?.TryGetValue(storageCell, out byte[]? value) == true)
                {
                    if (_logger?.IsDebug == true)
                        _logger?.Debug($"{_preBlockCache?.StageId} - Reading cache hit for {storageCell} -> {value?.ToHexString()}");

                    _baseStorageTree.HintGet(index, value);
                    Db.Metrics.IncrementStorageTreeCache();
                }
                else
                {
                    value = LoadFromTreeStorage(storageCell);

                    if (_logger?.IsDebug == true)
                        _logger?.Debug($"{_preBlockCache?.StageId} - Reading cache miss for {storageCell} -> {value.ToHexString()}");

                    if (_measureMetric)
                        _metricObserver.Observe(Stopwatch.GetTimestamp() - sw, _labels.SlotGetMiss);
                }

                return value ?? [0];
            }
        }

        public void HintGet(in UInt256 index, byte[]? value) => _baseStorageTree.HintGet(in index, value);

        private byte[] LoadFromTreeStorage(in StorageCell storageCell)
        {
            Db.Metrics.IncrementStorageTreeReads();

            return !storageCell.IsHash
                ? _baseStorageTree.Get(storageCell.Index)
                : _baseStorageTree.Get(storageCell.Hash);
        }

        public byte[] Get(in ValueHash256 hash) =>
            // Not a critical path. so we just forward for simplicity
            _baseStorageTree.Get(in hash);
    }

    public class CacheCopyWorldStateWriteBatch : IWorldStateScopeProvider.IWorldStateWriteBatch
    {
        private readonly IStagedPreBlockCaches _cache;
        private readonly IWorldStateScopeProvider.IWorldStateWriteBatch _baseBatch;
        private readonly ILogger? _logger;

        public CacheCopyWorldStateWriteBatch(IStagedPreBlockCaches cache, IWorldStateScopeProvider.IWorldStateWriteBatch baseBatch, ILogger? logger)
        {
            _cache = cache;
            _baseBatch = baseBatch;
            _baseBatch.OnAccountUpdated += _baseBatch_OnAccountUpdated;
            _logger = logger;
        }

        private void _baseBatch_OnAccountUpdated(object? sender, IWorldStateScopeProvider.AccountUpdated e)
        {
            Set(e.Address, e.Account);
            OnAccountUpdated?.Invoke(this, e);
        }

        public void Dispose()
        {
            _baseBatch.Dispose();
            _baseBatch.OnAccountUpdated -= _baseBatch_OnAccountUpdated;
        }

        public event EventHandler<IWorldStateScopeProvider.AccountUpdated>? OnAccountUpdated;

        public void Set(Address key, Account? account)
        {
            _baseBatch.Set(key, account);

            if (_logger?.IsDebug == true)
                _logger?.Debug($"Update cache {_cache.StageId} on write {key} -> {account}");
            
            if (account is not null)
                _cache.AddOrUpdate(key, account, (_, _) => account);
            else
                _cache.TryRemove(key, out _);
        }

        public IWorldStateScopeProvider.IStorageWriteBatch CreateStorageWriteBatch(Address key, int estimatedEntries)
        {
            IWorldStateScopeProvider.IStorageWriteBatch innerBatch =
                _baseBatch.CreateStorageWriteBatch(key, estimatedEntries);

            return new CacheCopyStorageWriteBatch(_cache, innerBatch, key, _logger);
        }
    }

    public class CacheCopyStorageWriteBatch : IWorldStateScopeProvider.IStorageWriteBatch
    {
        private IStagedPreBlockCaches _cache;
        private IWorldStateScopeProvider.IStorageWriteBatch _baseBatch;
        private readonly AddressAsKey _address;
        private readonly ILogger? _logger;

        public CacheCopyStorageWriteBatch(IStagedPreBlockCaches cache, IWorldStateScopeProvider.IStorageWriteBatch baseBatch, AddressAsKey address, ILogger? logger)
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

            StorageCell key = new(_address, in index);

            if (_logger?.IsDebug == true)
                _logger?.Debug($"Update cache {_cache.StageId} on write {key} -> {value.ToHexString()}");

            _cache.AddOrUpdate(key, value, (_, _) => value);
        }

        public void Clear()
        {
            _baseBatch.Clear();
        }
    }
}
