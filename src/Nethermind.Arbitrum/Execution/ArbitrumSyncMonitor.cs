// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Threading;
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
    ArbitrumSyncMonitorConfig syncConfig,
    ILogger logger)
{
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
            var finalizedBlockHash = ValidateAndGetBlockHash(finalizedFinalityData, FinalityBlockType.Finalized);
            var safeBlockHash = ValidateAndGetBlockHash(safeFinalityData, FinalityBlockType.Safe);
            var validatedBlockHash = ValidateAndGetBlockHash(validatedFinalityData, FinalityBlockType.Validated);

            // Apply validator wait logic - use validated block if it's earlier or equal
            if (validatedFinalityData.HasValue)
            {
                if (syncConfig.SafeBlockWaitForValidator && validatedBlockHash is not null &&
                    (!safeFinalityData.HasValue || validatedFinalityData.Value.MessageIndex <= safeFinalityData.Value.MessageIndex))
                {
                    safeBlockHash = validatedBlockHash;
                    if (logger.IsTrace)
                        logger.Trace($"Using validated block as safe due to validator wait configuration: {validatedBlockHash}");
                }

                if (syncConfig.FinalizedBlockWaitForValidator && validatedBlockHash is not null &&
                    (!finalizedFinalityData.HasValue || validatedFinalityData.Value.MessageIndex <= finalizedFinalityData.Value.MessageIndex))
                {
                    finalizedBlockHash = validatedBlockHash;
                    if (logger.IsTrace)
                        logger.Trace($"Using validated block as finalized due to validator wait configuration: {validatedBlockHash}");
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
                if (logger.IsTrace)
                    logger.Trace("No finality state changes to update");
                return;
            }

            // Apply the changes
            if (logger.IsDebug)
                logger.Debug($"Calling ForkChoiceUpdated with finalizedBlockHash={newFinalizedHash}, safeBlockHash={newSafeHash}");

            blockTree.ForkChoiceUpdated(newFinalizedHash, newSafeHash);

            if (logger.IsTrace)
                logger.Trace($"After ForkChoiceUpdated - FinalizedHash={blockTree.FinalizedHash}, SafeHash={blockTree.SafeHash}");
        }
    }

    /// <summary>
    /// Validates finality data consistency.
    /// </summary>
    /// <returns>Block hash if validation passed, null if header is missing</returns>
    private Hash256? ValidateAndGetBlockHash(
        ArbitrumFinalityData? finalityData,
        FinalityBlockType blockType)
    {
        if (finalityData is null)
            return null;

        var blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(
            finalityData.Value.MessageIndex, specHelper);

        if (logger.IsTrace)
            logger.Trace($"Looking for {blockType} block at number {blockNumber}");

        var header = blockTree.FindHeader(blockNumber, BlockTreeLookupOptions.None);

        if (logger.IsTrace)
            logger.Trace($"Found header for {blockType} block: {header is not null}, hash: {header?.Hash}");

        if (header is null)
        {
            if (logger.IsDebug)
                logger.Debug($"Block header not found for {blockType} block {blockNumber}, skipping validation");
            return null;
        }

        if (header.Hash != finalityData.Value.BlockHash)
        {
            var errorMessage = $"Block hash mismatch for {blockType} block {blockNumber}: expected={finalityData.Value.BlockHash}, actual={header.Hash}";
            if (logger.IsWarn)
                logger.Warn(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        if (logger.IsDebug)
            logger.Debug($"Successfully validated {blockType} block {blockNumber} with hash {header.Hash}");

        return header.Hash;
    }
}

/// <summary>
/// Configuration for ArbitrumSyncMonitor.
/// </summary>
public sealed class ArbitrumSyncMonitorConfig
{
    public bool SafeBlockWaitForValidator { get; set; }
    public bool FinalizedBlockWaitForValidator { get; set; }
}
