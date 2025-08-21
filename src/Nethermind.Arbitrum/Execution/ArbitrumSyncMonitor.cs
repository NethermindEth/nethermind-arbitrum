// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Math;
using Nethermind.Blockchain;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Monitors and manages finality data synchronization between Arbitrum consensus and Nethermind execution.
/// </summary>
public sealed class ArbitrumSyncMonitor(
    IBlockTree blockTree,
    IArbitrumSpecHelper specHelper,
    IArbitrumConfig arbitrumConfig,
    ILogManager logManager)
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumSyncMonitor>();
    private readonly Lock _lock = new();

    /// <summary>
    /// Sets finality data for safe and finalized blocks.
    /// </summary>
    /// <param name="safeFinalityData">Safe block finality data</param>
    /// <param name="finalizedFinalityData">Finalized block finality data</param>
    /// <param name="validatedFinalityData">Validated block finality data (for validator wait logic)</param>
    public void SetFinalityData(
        ArbitrumFinalityData? safeFinalityData,
        ArbitrumFinalityData? finalizedFinalityData,
        ArbitrumFinalityData? validatedFinalityData)
    {
        lock (_lock)
        {
            var finalizedBlockHash = ValidateAndGetBlockHash(finalizedFinalityData, "finalized");
            var safeBlockHash = ValidateAndGetBlockHash(safeFinalityData, "safe");
            var validatedBlockHash = ValidateAndGetBlockHash(validatedFinalityData, "validated");

            if (arbitrumConfig.SafeBlockWaitForValidator && safeFinalityData.HasValue)
            {
                if (validatedFinalityData is null)
                {
                    throw new InvalidOperationException("Block validator not set");
                }

                if (safeFinalityData.Value.MessageIndex > validatedFinalityData.Value.MessageIndex)
                {
                    safeBlockHash = validatedBlockHash;
                    if (_logger.IsTrace)
                        _logger.Trace($"Using validated block as safe due to validator wait configuration: {validatedBlockHash}");
                }
            }

            if (arbitrumConfig.FinalizedBlockWaitForValidator && finalizedFinalityData.HasValue)
            {
                if (validatedFinalityData is null)
                {
                    throw new InvalidOperationException("Block validator not set");
                }

                if (finalizedFinalityData.Value.MessageIndex > validatedFinalityData.Value.MessageIndex)
                {
                    finalizedBlockHash = validatedBlockHash;
                    if (_logger.IsTrace)
                        _logger.Trace($"Using validated block as finalized due to validator wait configuration: {validatedBlockHash}");
                }
            }

            // Get current state and compute new values
            var currentFinalizedHash = blockTree.FinalizedHash;
            var currentSafeHash = blockTree.SafeHash;
            var newFinalizedHash = finalizedBlockHash ?? currentFinalizedHash;
            var newSafeHash = safeBlockHash ?? currentSafeHash;

            // Early return if no changes needed
            if (newFinalizedHash == currentFinalizedHash && newSafeHash == currentSafeHash)
            {
                if (_logger.IsTrace)
                    _logger.Trace("No finality state changes to update");
                return;
            }

            // Apply the changes
            if (_logger.IsDebug)
                _logger.Debug($"Calling ForkChoiceUpdated with finalizedBlockHash={newFinalizedHash}, safeBlockHash={newSafeHash}");

            blockTree.ForkChoiceUpdated(newFinalizedHash, newSafeHash);

            if (_logger.IsTrace)
                _logger.Trace($"After ForkChoiceUpdated - FinalizedHash={blockTree.FinalizedHash}, SafeHash={blockTree.SafeHash}");
        }
    }

    /// <summary>
    /// Validates finality data consistency.
    /// </summary>
    /// <returns>Block hash if validation passed, null if header is missing</returns>
    private Hash256? ValidateAndGetBlockHash(
        ArbitrumFinalityData? finalityData,
        string blockType)
    {
        if (finalityData is null)
            return null;

        var blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(
            finalityData.Value.MessageIndex, specHelper);

        if (_logger.IsTrace)
            _logger.Trace($"Looking for {blockType} block at number {blockNumber}");

        var header = blockTree.FindHeader(blockNumber, BlockTreeLookupOptions.None);

        if (_logger.IsTrace)
            _logger.Trace($"Found header for {blockType} block: {header is not null}, hash: {header?.Hash}");

        if (header is null)
        {
            if (_logger.IsDebug)
                _logger.Debug($"Block header not found for {blockType} block {blockNumber}, skipping validation");
            return null;
        }

        if (header.Hash != finalityData.Value.BlockHash)
        {
            var errorMessage = $"Block hash mismatch for {blockType} block {blockNumber}: expected={finalityData.Value.BlockHash}, actual={header.Hash}";
            if (_logger.IsWarn)
                _logger.Warn(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        if (_logger.IsDebug)
            _logger.Debug($"Successfully validated {blockType} block {blockNumber} with hash {header.Hash}");

        return header.Hash;
    }
}
