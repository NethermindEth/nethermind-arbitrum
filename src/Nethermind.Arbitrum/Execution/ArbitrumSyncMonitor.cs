// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Math;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Monitors and manages synchronization state between Arbitrum consensus and Nethermind execution layers.
/// Provides finality tracking and sync status evaluation with lag tolerance.
/// Thread-safe for concurrent access.
/// </summary>
public sealed class ArbitrumSyncMonitor : IDisposable
{
    private readonly IBlockTree _blockTree;
    private readonly IArbitrumSpecHelper _specHelper;
    private readonly IArbitrumConfig _arbitrumConfig;
    private readonly ILogger _logger;
    private readonly ReaderWriterLockSlim _lock = new();

    private readonly SyncHistory _syncHistory;
    private readonly TimeSpan _msgLag;

    private bool _synced;
    private ulong _maxMessageCount;
    private Dictionary<string, object>? _syncProgressMap;
    private DateTimeOffset _updatedAt;
    private bool _disposed;

    public ArbitrumSyncMonitor(
        IBlockTree blockTree,
        IArbitrumSpecHelper specHelper,
        IArbitrumConfig arbitrumConfig,
        ILogManager logManager)
    {
        _blockTree = blockTree ?? throw new ArgumentNullException(nameof(blockTree));
        _specHelper = specHelper ?? throw new ArgumentNullException(nameof(specHelper));
        _arbitrumConfig = arbitrumConfig ?? throw new ArgumentNullException(nameof(arbitrumConfig));
        _logger = logManager?.GetClassLogger<ArbitrumSyncMonitor>() ?? throw new ArgumentNullException(nameof(logManager));

        _msgLag = TimeSpan.FromMilliseconds(_arbitrumConfig.MessageLagMs);
        _syncHistory = new SyncHistory(_msgLag);
    }

    /// <summary>
    /// Sets consensus sync data pushed from the consensus layer.
    /// This method is called approximately every 300ms by the consensus layer.
    /// </summary>
    public void SetConsensusSyncData(
        bool synced,
        ulong maxMessageCount,
        Dictionary<string, object>? syncProgressMap,
        DateTimeOffset updatedAt)
    {
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            _synced = synced;
            _maxMessageCount = maxMessageCount;
            _syncProgressMap = syncProgressMap;
            _updatedAt = updatedAt;

            if (maxMessageCount > 0)
            {
                DateTimeOffset syncTime = DateTimeOffset.UtcNow;
                if (syncTime > updatedAt) syncTime = updatedAt;

                _syncHistory.Add(maxMessageCount, syncTime);
            }

            if (_logger.IsTrace)
                _logger.Trace($"Consensus sync data updated: synced={synced}, maxMessageCount={maxMessageCount}, updatedAt={updatedAt:O}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Actively evaluates if execution is synced by comparing execution state with consensus sync target.
    /// Uses a sliding window approach to calculate a stable sync target with lag tolerance.
    /// </summary>
    public bool IsSynced()
    {
        ThrowIfDisposed();

        bool consensusSynced;
        DateTimeOffset consensusUpdatedAt;

        _lock.EnterReadLock();
        try
        {
            consensusSynced = _synced;
            consensusUpdatedAt = _updatedAt;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        TimeSpan dataAge = now - consensusUpdatedAt;
        if (dataAge > _msgLag)
        {
            if (_logger.IsTrace)
                _logger.Trace($"Sync data is stale: age={dataAge.TotalSeconds:F2}s, msgLag={_msgLag.TotalSeconds:F2}s");
            return false;
        }

        if (!consensusSynced)
        {
            if (_logger.IsTrace)
                _logger.Trace("Consensus reports not synced");
            return false;
        }

        BlockHeader? head = _blockTree.Head?.Header;
        if (head is null)
        {
            if (_logger.IsTrace)
                _logger.Trace("No head block found");
            return false;
        }

        ulong builtMessageIndex;
        try
        {
            builtMessageIndex = MessageBlockConverter.BlockNumberToMessageIndex(
                (ulong)head.Number, _specHelper);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error converting block number {head.Number} to message index: {ex.Message}");
            return false;
        }

        ulong syncTarget = _syncHistory.GetSyncTarget(now);
        if (syncTarget == 0)
        {
            if (_logger.IsTrace)
                _logger.Trace("No valid sync target available yet (history window empty)");
            return false;
        }

        bool isSynced = builtMessageIndex + 1 >= syncTarget;

        if (_logger.IsDebug)
            _logger.Debug($"Sync evaluation: builtMessageIndex={builtMessageIndex}, syncTarget={syncTarget}, isSynced={isSynced}");

        return isSynced;
    }

    /// <summary>
    /// Gets detailed sync progress information combining consensus and execution state.
    /// </summary>
    public Dictionary<string, object> GetFullSyncProgressMap()
    {
        ThrowIfDisposed();

        Dictionary<string, object> result = new();

        Dictionary<string, object>? consensusProgressMap;
        ulong consensusMaxMessageCount;

        _lock.EnterReadLock();
        try
        {
            consensusProgressMap = _syncProgressMap;
            consensusMaxMessageCount = _maxMessageCount;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        if (consensusProgressMap != null)
            foreach (KeyValuePair<string, object> kvp in consensusProgressMap) result[kvp.Key] = kvp.Value;

        result["consensusMaxMessageCount"] = consensusMaxMessageCount;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ulong syncTarget = _syncHistory.GetSyncTarget(now);
        result["executionSyncTarget"] = syncTarget;

        BlockHeader? head = _blockTree.Head?.Header;
        if (head != null)
        {
            result["blockNum"] = head.Number;
            try
            {
                ulong messageIndex = MessageBlockConverter.BlockNumberToMessageIndex(
                    (ulong)head.Number, _specHelper);
                result["messageOfLastBlock"] = messageIndex;
            }
            catch (Exception ex)
            {
                result["messageOfLastBlockError"] = ex.Message;
            }
        }
        else
            result["currentHeaderError"] = "No head block found";

        return result;
    }

    /// <summary>
    /// Sets finality data for safe and finalized blocks.
    /// </summary>
    public void SetFinalityData(
        ArbitrumFinalityData? safeFinalityData,
        ArbitrumFinalityData? finalizedFinalityData,
        ArbitrumFinalityData? validatedFinalityData)
    {
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            Hash256? finalizedBlockHash = ValidateAndGetBlockHash(finalizedFinalityData, "finalized");
            Hash256? safeBlockHash = ValidateAndGetBlockHash(safeFinalityData, "safe");
            Hash256? validatedBlockHash = ValidateAndGetBlockHash(validatedFinalityData, "validated");

            if (_arbitrumConfig.SafeBlockWaitForValidator && safeFinalityData.HasValue)
            {
                if (validatedFinalityData is null)
                    throw new InvalidOperationException(
                        "SafeBlockWaitForValidator is enabled but validated finality data is not provided");

                if (safeFinalityData.Value.MessageIndex > validatedFinalityData.Value.MessageIndex)
                {
                    safeBlockHash = validatedBlockHash;
                    if (_logger.IsTrace)
                        _logger.Trace($"Using validated block {validatedBlockHash} as safe due to validator wait configuration");
                }
            }

            if (_arbitrumConfig.FinalizedBlockWaitForValidator && finalizedFinalityData.HasValue)
            {
                if (validatedFinalityData is null)
                    throw new InvalidOperationException(
                        "FinalizedBlockWaitForValidator is enabled but validated finality data is not provided");

                if (finalizedFinalityData.Value.MessageIndex > validatedFinalityData.Value.MessageIndex)
                {
                    finalizedBlockHash = validatedBlockHash;
                    if (_logger.IsTrace)
                        _logger.Trace($"Using validated block {validatedBlockHash} as finalized due to validator wait configuration");
                }
            }

            Hash256? currentFinalizedHash = _blockTree.FinalizedHash;
            Hash256? currentSafeHash = _blockTree.SafeHash;
            Hash256? newFinalizedHash = finalizedBlockHash ?? currentFinalizedHash;
            Hash256? newSafeHash = safeBlockHash ?? currentSafeHash;

            if (newFinalizedHash == currentFinalizedHash && newSafeHash == currentSafeHash)
            {
                if (_logger.IsTrace)
                    _logger.Trace("No finality state changes to update");
                return;
            }

            if (_logger.IsDebug)
                _logger.Debug($"Updating finality: finalized={newFinalizedHash}, safe={newSafeHash}");

            _blockTree.ForkChoiceUpdated(newFinalizedHash, newSafeHash);

            if (_logger.IsTrace)
                _logger.Trace($"Finality updated: FinalizedHash={_blockTree.FinalizedHash}, SafeHash={_blockTree.SafeHash}");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private Hash256? ValidateAndGetBlockHash(
        ArbitrumFinalityData? finalityData,
        string blockType)
    {
        if (finalityData is null)
            return null;

        long blockNumber;
        try
        {
            blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(
                finalityData.Value.MessageIndex, _specHelper);
        }
        catch (Exception ex)
        {
            if (_logger.IsWarn)
                _logger.Warn($"Error converting message index {finalityData.Value.MessageIndex} to block number for {blockType}: {ex.Message}");
            return null;
        }

        if (_logger.IsTrace)
            _logger.Trace($"Validating {blockType} block at number {blockNumber}, messageIndex={finalityData.Value.MessageIndex}");

        BlockHeader? header = _blockTree.FindHeader(blockNumber, BlockTreeLookupOptions.None);

        if (header is null)
        {
            if (_logger.IsDebug)
                _logger.Debug($"Block header not found for {blockType} block {blockNumber} (may not be synced yet)");
            return null;
        }

        if (header.Hash != finalityData.Value.BlockHash)
        {
            string errorMessage = $"Block hash mismatch for {blockType} block {blockNumber}: " +
                                 $"expected={finalityData.Value.BlockHash}, actual={header.Hash}";
            if (_logger.IsWarn)
                _logger.Warn(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        if (_logger.IsTrace)
            _logger.Trace($"Successfully validated {blockType} block {blockNumber} with hash {header.Hash}");

        return header.Hash;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _lock.Dispose();
        _syncHistory.Dispose();
        _disposed = true;

        if (_logger.IsDebug)
            _logger.Debug("ArbitrumSyncMonitor disposed");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ArbitrumSyncMonitor));
    }
}
