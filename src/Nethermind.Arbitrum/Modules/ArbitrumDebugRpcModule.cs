// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Core;
using Nethermind.Blockchain;
using Nethermind.Core.Caching;
using Nethermind.Core;
using Nethermind.Db;
using Nethermind.Evm.State;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Trie;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// Debug RPC module for Arbitrum system testing with MemDb mode.
/// Provides state reinitialization for test isolation.
/// </summary>
public class ArbitrumDebugRpcModule(
    IDbProvider dbProvider,
    IResettableBlockTree blockTree,
    IEnumerable<IClearableCache> cacheAwareServices,
    ILogManager logManager,
    IBlockhashCache? blockhashCache = null,
    PreBlockCaches? preBlockCaches = null)
    : IArbitrumDebugRpcModule
{
    private readonly IDbProvider _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
    private readonly IResettableBlockTree _blockTree = blockTree ?? throw new ArgumentNullException(nameof(blockTree));
    private readonly IEnumerable<IClearableCache> _cacheAwareServices = cacheAwareServices ?? throw new ArgumentNullException(nameof(cacheAwareServices));
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumDebugRpcModule>();

    public Task<ResultWrapper<bool>> debug_reinitialize(
        int arbosVersion,
        string accountsJson,
        string? maxCodeSize = null)
    {
        try
        {
            if (_logger.IsInfo)
                _logger.Info($"debug_reinitialize called: arbosVersion={arbosVersion}, maxCodeSize={maxCodeSize ?? "default"}");

            if (!string.IsNullOrWhiteSpace(accountsJson) && accountsJson != "{}" && accountsJson != "null")
            {
                if (_logger.IsWarn)
                    _logger.Warn("debug_reinitialize: accountsJson parameter provided but not yet implemented; accounts will NOT be pre-funded. This is expected for comparison mode.");
            }

            if (arbosVersion != 0 && _logger.IsDebug)
                _logger.Debug($"debug_reinitialize: arbosVersion={arbosVersion} ignored (chainspec determines version)");

            if (!string.IsNullOrWhiteSpace(maxCodeSize) && _logger.IsDebug)
                _logger.Debug($"debug_reinitialize: maxCodeSize={maxCodeSize} ignored (chainspec determines limit)");

            // Clear all databases to reset the persistent state
            ClearAllDatabases();

            // Reset BlockTree in-memory state (Genesis, Head, etc.)
            // This is critical for comparison mode where databases are cleared but
            // BlockTree.Genesis remains cached in memory from the previous test
            _blockTree.ResetForTesting();
            if (_logger.IsDebug)
                _logger.Debug("BlockTree in-memory state reset");

            // Clear all static and singleton caches to ensure complete test isolation
            // These caches persist across test reinitialization and can cause flaky tests
            ClearAllCaches();
            if (_logger.IsInfo)
                _logger.Info("debug_reinitialize completed: databases and BlockTree cleared, ready for init message from CL");

            return Task.FromResult(ResultWrapper<bool>.Success(true));
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error("debug_reinitialize failed with exception", ex);
            return Task.FromResult(ResultWrapper<bool>.Fail($"Reinitialization failed: {ex.Message}"));
        }
    }

    private void ClearAllDatabases()
    {
        if (_logger.IsDebug)
            _logger.Debug("Clearing all databases...");

        List<string> failures = [];

        if (!ClearDatabase(_dbProvider.StateDb, "StateDb"))
            failures.Add("StateDb");
        if (!ClearDatabase(_dbProvider.CodeDb, "CodeDb"))
            failures.Add("CodeDb");
        if (!ClearDatabase(_dbProvider.BlocksDb, "BlocksDb"))
            failures.Add("BlocksDb");
        if (!ClearDatabase(_dbProvider.HeadersDb, "HeadersDb"))
            failures.Add("HeadersDb");
        if (!ClearDatabase(_dbProvider.BlockInfosDb, "BlockInfosDb"))
            failures.Add("BlockInfosDb");
        if (!ClearDatabase(_dbProvider.BlockNumbersDb, "BlockNumbersDb"))
            failures.Add("BlockNumbersDb");
        if (!ClearDatabase(_dbProvider.MetadataDb, "MetadataDb"))
            failures.Add("MetadataDb");
        if (!ClearDatabase(_dbProvider.BadBlocksDb, "BadBlocksDb"))
            failures.Add("BadBlocksDb");
        if (!ClearDatabase(_dbProvider.BloomDb, "BloomDb"))
            failures.Add("BloomDb");

        // Clear column databases (ReceiptsDb, BlobTransactionsDb)
        if (!ClearColumnsDatabase(_dbProvider.ReceiptsDb, "ReceiptsDb"))
            failures.Add("ReceiptsDb");
        if (!ClearColumnsDatabase(_dbProvider.BlobTransactionsDb, "BlobTransactionsDb"))
            failures.Add("BlobTransactionsDb");

        if (failures.Count > 0)
            throw new InvalidOperationException($"Failed to clear databases: {string.Join(", ", failures)}");

        if (_logger.IsDebug)
            _logger.Debug("All databases cleared");
    }

    private bool ClearDatabase(IDb? db, string name)
    {
        if (db is null)
            return true;

        try
        {
            db.Clear();
            if (_logger.IsDebug)
                _logger.Debug($"Cleared {name} ({db.GetType().Name})");
            return true;
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Failed to clear {name} ({db.GetType().Name}): {ex.Message}", ex);
            return false;
        }
    }

    private bool ClearColumnsDatabase<T>(IColumnsDb<T>? columnsDb, string name) where T : struct, Enum
    {
        if (columnsDb is null)
            return true;

        try
        {
            foreach (T column in Enum.GetValues<T>())
            {
                columnsDb.GetColumnDb(column).Clear();
            }
            if (_logger.IsDebug)
                _logger.Debug($"Cleared {name} (all {typeof(T).Name} columns)");
            return true;
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Failed to clear {name}: {ex.Message}", ex);
            return false;
        }
    }

    private void ClearAllCaches()
    {
        if (_logger.IsDebug)
            _logger.Debug("Clearing all static and singleton caches...");

        // Auto-discovered caches via IClearableCache (L1BlockCache, TransactionExtensions, CachedL1PriceData, WasmDb)
        foreach (IClearableCache cache in _cacheAwareServices)
        {
            cache.ClearCache();
            if (_logger.IsDebug)
                _logger.Debug($"Cleared {cache.GetType().Name}");
        }

        // Optional caches not managed by DI
        if (blockhashCache is BlockhashCache concreteBlockhashCache)
        {
            concreteBlockhashCache.Clear();
            if (_logger.IsDebug)
                _logger.Debug("Cleared BlockhashCache");
        }

        if (preBlockCaches is not null)
        {
            preBlockCaches.ClearCaches();
            if (_logger.IsDebug)
                _logger.Debug("Cleared PreBlockCaches (state, storage, RLP, precompile)");
        }

        if (_logger.IsDebug)
            _logger.Debug("All caches cleared");
    }
}
