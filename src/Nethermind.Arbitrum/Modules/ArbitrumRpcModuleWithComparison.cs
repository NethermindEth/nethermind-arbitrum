// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// Extended version of ArbitrumRpcModule that adds comparison functionality.
/// Only instantiated when comparison mode is enabled in configuration.
/// </summary>
public class ArbitrumRpcModuleWithComparison(
    ArbitrumBlockTreeInitializer initializer,
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    ArbitrumRpcTxSource txSource,
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager,
    CachedL1PriceData cachedL1PriceData,
    IBlockProcessingQueue processingQueue,
    IArbitrumConfig arbitrumConfig)
    : ArbitrumRpcModule(initializer, blockTree, trigger, txSource, chainSpec, specHelper, logManager, cachedL1PriceData, processingQueue, arbitrumConfig)
{
    private readonly ArbitrumComparisonRpcClient _comparisonRpcClient = new(arbitrumConfig.ComparisonModeRpcUrl!, logManager.GetClassLogger<ArbitrumRpcModule>());
    private readonly long _comparisonInterval = (long)arbitrumConfig.ComparisonModeInterval;

    // Override DigestMessage to add comparison logic
    public override async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
    {
        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtMessageIndex(parameters.Index);
        if (resultAtMessageIndex.Result == Result.Success)
            return resultAtMessageIndex;

        // Non-blocking attempt to acquire the semaphore.
        if (!await _createBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);

        try
        {
            _ = txSource; // TODO: replace with the actual use

            long blockNumber = (await MessageIndexToBlockNumber(parameters.Index)).Data;
            BlockHeader? headBlockHeader = blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
            {
                return ResultWrapper<MessageResult>.Fail(
                    $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");
            }

            // Check if we should compare on this block
            if (blockNumber % _comparisonInterval == 0)
            {
                return await DigestMessageWithComparisonAsync(parameters.Message, blockNumber, headBlockHeader);
            }

            return await ProduceBlockWhileLockedAsync(parameters.Message, blockNumber, headBlockHeader);
        }
        finally
        {
            // Ensure the semaphore is released, equivalent to Go's `defer Unlock()`.
            _createBlocksSemaphore.Release();
        }
    }

    private async Task<ResultWrapper<MessageResult>> DigestMessageWithComparisonAsync(
        MessageWithMetadata messageWithMetadata,
        long blockNumber,
        BlockHeader? headBlockHeader)
    {
        if (_logger.IsInfo)
            _logger.Info($"Comparison mode: Processing block {blockNumber} with external RPC validation");

        // Execute DigestMessage and external RPC call in parallel
        Task<ResultWrapper<MessageResult>> digestTask = ProduceBlockWhileLockedAsync(messageWithMetadata, blockNumber, headBlockHeader);
        Task<(Hash256? blockHash, Hash256? sendRoot)> externalRpcTask = _comparisonRpcClient.GetBlockDataAsync(blockNumber);

        try
        {
            await Task.WhenAll(digestTask, externalRpcTask);

            ResultWrapper<MessageResult> digestResult = await digestTask;
            (Hash256? externalBlockHash, Hash256? externalSendRoot) = await externalRpcTask;

            // If DigestMessage failed, return the error
            if (digestResult.Result != Result.Success)
            {
                if (_logger.IsError)
                    _logger.Error($"Comparison mode: DigestMessage failed for block {blockNumber}: {digestResult.Result.Error}");

                return digestResult;
            }

            // If external RPC returned null, log warning but don't fail
            if (externalBlockHash == null || externalSendRoot == null)
            {
                if (_logger.IsWarn)
                    _logger.Warn($"Comparison mode: External RPC returned null data for block {blockNumber}, skipping comparison");

                return digestResult;
            }

            // Compare results
            MessageResult internalResult = digestResult.Data;
            bool hashMatch = internalResult.BlockHash.Equals(externalBlockHash);
            bool sendRootMatch = internalResult.SendRoot.Equals(externalSendRoot);

            if (!hashMatch || !sendRootMatch)
            {
                string errorMessage = $"Comparison mode: MISMATCH detected at block {blockNumber}!\n" +
                                     $"  Got BlockHash:  {internalResult.BlockHash}\n" +
                                     $"  Expected BlockHash:  {externalBlockHash}\n" +
                                     $"  Hash Match:          {hashMatch}\n" +
                                     $"  Got SendRoot:   {internalResult.SendRoot}\n" +
                                     $"  Expected SendRoot:   {externalSendRoot}\n" +
                                     $"  SendRoot Match:      {sendRootMatch}";

                if (_logger.IsError)
                    _logger.Error(errorMessage);

                // Trigger graceful shutdown
                TriggerGracefulShutdown(errorMessage);

                throw new InvalidOperationException(errorMessage);
            }

            if (_logger.IsInfo)
                _logger.Info($"Comparison mode: Block {blockNumber} validation PASSED - hashes and sendRoots match");

            return digestResult;
        }
        catch (InvalidOperationException) when (_logger.IsError)
        {
            // Re-throw comparison mismatches
            throw;
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Comparison mode: Unexpected error during comparison for block {blockNumber}: {ex.Message}", ex);

            // On unexpected errors, return the digest result if available
            if (digestTask.IsCompletedSuccessfully)
            {
                return await digestTask;
            }

            throw;
        }
    }

    private void TriggerGracefulShutdown(string reason)
    {
        if (_logger.IsError)
            _logger.Error($"Initiating graceful shutdown due to comparison mismatch: {reason}");

        // Schedule shutdown on a background thread to allow current operation to complete
        Task.Run(async () =>
        {
            try
            {
                // Give time for logs to flush and current operations to complete
                await Task.Delay(TimeSpan.FromSeconds(2));

                if (_logger.IsError)
                    _logger.Error("Shutting down process due to block comparison mismatch");

                // Exit with error code 1 to indicate failure
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                if (_logger.IsError)
                    _logger.Error($"Error during shutdown: {ex.Message}", ex);

                // Force exit if graceful shutdown fails
                Environment.Exit(1);
            }
        });
    }
}
