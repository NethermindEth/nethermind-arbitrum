// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Modules;

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
    IArbitrumConfig arbitrumConfig,
    IProcessExitSource? processExitSource = null)
    : ArbitrumRpcModule(initializer, blockTree, trigger, txSource, chainSpec, specHelper, logManager, cachedL1PriceData, processingQueue, arbitrumConfig)
{
    private readonly ArbitrumComparisonRpcClient _comparisonRpcClient = new(arbitrumConfig.ComparisonModeRpcUrl!, logManager.GetClassLogger<ArbitrumRpcModule>());
    private readonly long _comparisonInterval = (long)arbitrumConfig.ComparisonModeInterval;
    private long _lastComparedBlock;
    private readonly IBlockTree _blockTree = blockTree;
    private readonly ArbitrumRpcTxSource _txSource = txSource;

    public override async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
    {
        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtMessageIndex(parameters.Index);
        if (resultAtMessageIndex.Result == Result.Success)
            return resultAtMessageIndex;

        if (!await _createBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);

        try
        {
            _ = _txSource; // TODO: replace with the actual use

            long blockNumber = (await MessageIndexToBlockNumber(parameters.Index)).Data;
            BlockHeader? headBlockHeader = _blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
                return ResultWrapper<MessageResult>.Fail(
                    $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");

            if (blockNumber % _comparisonInterval == 0)
                return await DigestMessageWithComparisonAsync(parameters.Message, blockNumber, headBlockHeader);

            return await ProduceBlockWhileLockedAsync(parameters.Message, blockNumber, headBlockHeader);
        }
        finally
        {
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

        Task<ResultWrapper<MessageResult>> digestTask = ProduceBlockWhileLockedAsync(messageWithMetadata, blockNumber, headBlockHeader);
        Task<ResultWrapper<MessageResult>> externalRpcTask = _comparisonRpcClient.GetBlockDataAsync(blockNumber);

        try
        {
            await Task.WhenAll(digestTask, externalRpcTask);

            ResultWrapper<MessageResult> digestResult = await digestTask;
            ResultWrapper<MessageResult> rpcResult = await externalRpcTask;

            if (digestResult.Result != Result.Success)
            {
                if (_logger.IsError)
                    _logger.Error($"Comparison mode: DigestMessage failed for block {blockNumber}: {digestResult.Result.Error}");

                return digestResult;
            }

            if (rpcResult.Result != Result.Success)
            {
                if (_logger.IsError)
                    _logger.Error($"Comparison mode: RPC call failed for block {blockNumber}: {rpcResult.Result.Error}");

                return digestResult;
            }

            MessageResult digestResultData = digestResult.Data;
            MessageResult rpcResultData = rpcResult.Data;

            if (!digestResultData.Equals(rpcResultData))
            {
                long firstMismatchBlock = await BinarySearchFirstMismatchAsync(_lastComparedBlock + 1, blockNumber);

                string errorMessage = $"Comparison mode: MISMATCH detected!\n" +
                                      $"  First mismatch at block: {firstMismatchBlock}\n" +
                                      $"  Detected at block:       {blockNumber}\n" +
                                      $"  Last good block:         {_lastComparedBlock}\n" +
                                      $"  Got BlockHash:           {digestResultData.BlockHash}\n" +
                                      $"  Expected BlockHash:      {rpcResultData.BlockHash}\n" +
                                      $"  Got SendRoot:            {digestResultData.SendRoot}\n" +
                                      $"  Expected SendRoot:       {rpcResultData.SendRoot}\n";

                if (_logger.IsError)
                    _logger.Error(errorMessage);

                TriggerGracefulShutdown(errorMessage);
                return ResultWrapper<MessageResult>.Fail("Block comparison mismatch detected - shutting down", ErrorCodes.InternalError);
            }

            _lastComparedBlock = blockNumber;

            if (_logger.IsInfo)
                _logger.Info($"Comparison mode: Block {blockNumber} validation PASSED - hashes and sendRoots match");

            return digestResult;
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Comparison mode: Unexpected error during comparison for block {blockNumber}: {ex.Message}", ex);

            if (digestTask.IsCompletedSuccessfully)
                return await digestTask;

            throw;
        }
    }


    /// <summary>
    /// Binary search to find the first block where the mismatch occurred
    /// </summary>
    private async Task<long> BinarySearchFirstMismatchAsync(long startBlock, long endBlock)
    {
        if (_logger.IsInfo)
            _logger.Info($"Binary search: Finding first mismatch between blocks {startBlock} and {endBlock}");

        long left = startBlock;
        long right = endBlock;
        long firstMismatch = endBlock;

        while (left < right)
        {
            long mid = left + (right - left) / 2;

            if (_logger.IsDebug)
                _logger.Debug($"Binary search: Checking block {mid} (range: {left}-{right})");

            Task<ResultWrapper<MessageResult>> internalTask = ResultAtMessageIndex((ulong)mid);
            Task<ResultWrapper<MessageResult>> externalTask = _comparisonRpcClient.GetBlockDataAsync(mid);

            await Task.WhenAll(internalTask, externalTask);

            ResultWrapper<MessageResult> internalResult = await internalTask;
            ResultWrapper<MessageResult> externalResult = await externalTask;

            // Check if both succeeded
            if (internalResult.Result != Result.Success)
            {
                if (_logger.IsError)
                    _logger.Error($"Internal block {mid} not found or failed");
                return 0;
            }

            if (externalResult.Result != Result.Success)
            {
                if (_logger.IsError)
                    _logger.Error($"External RPC for block {mid} failed");
                return 0;
            }

            MessageResult internalResultData = internalResult.Data;
            MessageResult externalResultData = externalResult.Data;

            if (!internalResultData.Equals(externalResultData))
            {
                // Mismatch found, search left half
                if (_logger.IsDebug)
                    _logger.Debug($"Binary search: Mismatch at block {mid}, searching left half");
                firstMismatch = mid;
                right = mid;
            }
            else
            {
                // Match found, search right half
                if (_logger.IsDebug)
                    _logger.Debug($"Binary search: Match at block {mid}, searching right half");
                left = mid + 1;
            }
        }

        if (_logger.IsInfo)
            _logger.Info($"Binary search: First mismatch found at block {firstMismatch}");

        return firstMismatch;
    }

    private void TriggerGracefulShutdown(string reason)
    {
        if (_logger.IsError)
            _logger.Error($"Initiating graceful shutdown due to comparison mismatch: {reason}");

        // Use Nethermind's proper shutdown mechanism if available
        if (processExitSource is not null)
        {
            if (_logger.IsInfo)
                _logger.Info("Triggering graceful shutdown via ProcessExitSource");

            processExitSource.Exit(ExitCodes.GeneralError);
        }
        else
        {
            // Fallback to Environment.Exit if ProcessExitSource is not available (e.g., in tests)
            if (_logger.IsWarn)
                _logger.Warn("ProcessExitSource not available, using Environment.Exit as fallback");

            Task.Run(async () =>
            {
                try
                {
                    // Give minimal time for logs to flush
                    await Task.Delay(TimeSpan.FromMilliseconds(500));

                    if (_logger.IsError)
                        _logger.Error("Shutting down process due to block comparison mismatch");

                    Environment.Exit(ExitCodes.GeneralError);
                }
                catch (Exception ex)
                {
                    if (_logger.IsError)
                        _logger.Error($"Error during shutdown: {ex.Message}", ex);

                    Environment.Exit(ExitCodes.GeneralError);
                }
            });
        }
    }
}
