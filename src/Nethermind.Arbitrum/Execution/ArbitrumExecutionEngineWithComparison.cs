// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Modules;
using Nethermind.Config;
using Nethermind.Core;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Decorator that adds block hash comparison against external RPC for validation.
/// Wraps DigestMessageAsync to compare results every N blocks.
/// </summary>
public sealed class ArbitrumExecutionEngineWithComparison(
    IArbitrumExecutionEngine innerEngine,
    IVerifyBlockHashConfig verifyBlockHashConfig,
    IJsonSerializer jsonSerializer,
    ILogManager logManager,
    IProcessExitSource? processExitSource = null)
    : IArbitrumExecutionEngine
{
    private readonly ArbitrumComparisonRpcClient _comparisonRpcClient = new(verifyBlockHashConfig.ArbNodeRpcUrl!, jsonSerializer, logManager);
    private readonly long _verificationInterval = (long)verifyBlockHashConfig.VerifyEveryNBlocks;
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumExecutionEngineWithComparison>();

    private long _lastVerifiedBlock;

    public Task<ResultWrapper<MessageResult[]>> ReorgAsync(ReorgParameters parameters)
        => innerEngine.ReorgAsync(parameters);

    public Task<ResultWrapper<MessageResult>> ResultAtMessageIndexAsync(ulong messageIndex)
        => innerEngine.ResultAtMessageIndexAsync(messageIndex);

    public Task<ResultWrapper<ulong>> HeadMessageIndexAsync()
        => innerEngine.HeadMessageIndexAsync();

    public ResultWrapper<long> MessageIndexToBlockNumber(ulong messageIndex)
        => innerEngine.MessageIndexToBlockNumber(messageIndex);

    public ResultWrapper<ulong> BlockNumberToMessageIndex(ulong blockNumber)
        => innerEngine.BlockNumberToMessageIndex(blockNumber);

    public ResultWrapper<EmptyResponse> SetFinalityData(SetFinalityDataParams parameters)
        => innerEngine.SetFinalityData(parameters);

    public ResultWrapper<EmptyResponse> MarkFeedStart(ulong to)
        => innerEngine.MarkFeedStart(to);

    public Task<ResultWrapper<string>> TriggerMaintenanceAsync()
        => innerEngine.TriggerMaintenanceAsync();

    public Task<ResultWrapper<bool>> ShouldTriggerMaintenanceAsync()
        => innerEngine.ShouldTriggerMaintenanceAsync();

    public Task<ResultWrapper<MaintenanceStatus>> MaintenanceStatusAsync()
        => innerEngine.MaintenanceStatusAsync();

    public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
        => innerEngine.DigestInitMessage(message);

    public ResultWrapper<EmptyResponse> SetConsensusSyncData(SetConsensusSyncDataParams? parameters)
        => innerEngine.SetConsensusSyncData(parameters);

    public ResultWrapper<bool> Synced()
        => innerEngine.Synced();

    public ResultWrapper<Dictionary<string, object>> FullSyncProgressMap()
        => innerEngine.FullSyncProgressMap();

    public Task<ResultWrapper<ulong>> ArbOSVersionForMessageIndexAsync(ulong messageIndex)
        => innerEngine.ArbOSVersionForMessageIndexAsync(messageIndex);

    public Task<ResultWrapper<StartSequencingResult>> StartSequencingAsync()
        => innerEngine.StartSequencingAsync();

    public ResultWrapper<EmptyResponse> EndSequencing(string? error)
        => innerEngine.EndSequencing(error);

    public Task<ResultWrapper<EmptyResponse>> AppendLastSequencedBlockAsync()
        => innerEngine.AppendLastSequencedBlockAsync();

    public ResultWrapper<EmptyResponse> EnqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx)
        => innerEngine.EnqueueDelayedMessages(messages, firstMsgIdx);

    public ResultWrapper<ulong> NextDelayedMessageNumber()
        => innerEngine.NextDelayedMessageNumber();

    public Task<ResultWrapper<SequencedMsg?>> ResequenceReorgedMessageAsync(MessageWithMetadata? msg)
        => innerEngine.ResequenceReorgedMessageAsync(msg);

    public ResultWrapper<EmptyResponse> Pause()
        => innerEngine.Pause();

    public ResultWrapper<EmptyResponse> Activate()
        => innerEngine.Activate();

    public ResultWrapper<EmptyResponse> ForwardTo(string url)
        => innerEngine.ForwardTo(url);

    public async Task<ResultWrapper<MessageResult>> DigestMessageAsync(DigestMessageParameters parameters)
    {
        // Get a block number for comparison interval check
        ResultWrapper<long> blockNumberResult = MessageIndexToBlockNumber(parameters.Index);
        if (blockNumberResult.Result != Result.Success)
            return await innerEngine.DigestMessageAsync(parameters);

        long blockNumber = blockNumberResult.Data;

        // Check if this block needs comparison
        if (blockNumber % _verificationInterval != 0)
            return await innerEngine.DigestMessageAsync(parameters);

        // Perform comparison
        return await DigestMessageWithComparisonAsync(parameters, blockNumber);
    }

    private async Task<ResultWrapper<MessageResult>> DigestMessageWithComparisonAsync(
        DigestMessageParameters parameters,
        long blockNumber)
    {
        if (_logger.IsInfo)
            _logger.Info($"Comparison mode: Processing block {blockNumber} with external RPC validation");

        Task<ResultWrapper<MessageResult>> digestTask = innerEngine.DigestMessageAsync(parameters);
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
                long firstMismatchBlock = await BinarySearchFirstMismatchAsync(_lastVerifiedBlock + 1, blockNumber);

                string errorMessage = $"Comparison mode: MISMATCH detected!\n" +
                                      $"  First mismatch at block: {firstMismatchBlock}\n" +
                                      $"  Detected at block:       {blockNumber}\n" +
                                      $"  Last good block:         {_lastVerifiedBlock}\n" +
                                      $"  Got BlockHash:           {digestResultData.BlockHash}\n" +
                                      $"  Expected BlockHash:      {rpcResultData.BlockHash}\n" +
                                      $"  Got SendRoot:            {digestResultData.SendRoot}\n" +
                                      $"  Expected SendRoot:       {rpcResultData.SendRoot}\n";

                if (_logger.IsError)
                    _logger.Error(errorMessage);

                TriggerGracefulShutdown(errorMessage);
                return ResultWrapper<MessageResult>.Fail("Block comparison mismatch detected - shutting down", ErrorCodes.InternalError);
            }

            _lastVerifiedBlock = blockNumber;

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

    private async Task<long> BinarySearchFirstMismatchAsync(long startBlock, long endBlock)
    {
        if (_logger.IsInfo)
            _logger.Info($"Binary search: Finding first mismatch between blocks {startBlock} and {endBlock}");

        return await BinarySearchAsync(startBlock, endBlock, IsBlockMismatchDetected, result: endBlock);
    }

    private async Task<long> BinarySearchAsync(long left, long right, Func<long, Task<bool>> predicateAsync, long result)
    {
        while (left < right)
        {
            long mid = left + (right - left) / 2;

            if (_logger.IsDebug)
                _logger.Debug($"Binary search: Checking index {mid} (range: {left}-{right})");

            bool predicateResult = await predicateAsync(mid);

            if (predicateResult)
            {
                if (_logger.IsDebug)
                    _logger.Debug($"Binary search: Condition met at {mid}, searching left half");
                result = mid;
                right = mid;
            }
            else
            {
                if (_logger.IsDebug)
                    _logger.Debug($"Binary search: Condition not met at {mid}, searching right half");
                left = mid + 1;
            }
        }

        if (_logger.IsInfo)
            _logger.Info($"Binary search: Result found at index {result}");

        return result;
    }

    private async Task<bool> IsBlockMismatchDetected(long blockNumber)
    {
        // Convert block number to message index for internal lookup
        ResultWrapper<ulong> messageIndexResult = BlockNumberToMessageIndex((ulong)blockNumber);
        if (messageIndexResult.Result != Result.Success)
        {
            if (_logger.IsError)
                _logger.Error($"Failed to convert block {blockNumber} to message index");
            return true;
        }

        Task<ResultWrapper<MessageResult>> internalTask = ResultAtMessageIndexAsync(messageIndexResult.Data);
        Task<ResultWrapper<MessageResult>> externalTask = _comparisonRpcClient.GetBlockDataAsync(blockNumber);

        await Task.WhenAll(internalTask, externalTask);

        ResultWrapper<MessageResult> internalResult = await internalTask;
        ResultWrapper<MessageResult> externalResult = await externalTask;

        if (internalResult.Result != Result.Success)
        {
            if (_logger.IsError)
                _logger.Error($"Internal block {blockNumber} not found or failed");
            return true;
        }

        if (externalResult.Result != Result.Success)
        {
            if (_logger.IsError)
                _logger.Error($"External RPC for block {blockNumber} failed");
            return true;
        }

        return !internalResult.Data.Equals(externalResult.Data);
    }

    private void TriggerGracefulShutdown(string reason)
    {
        if (_logger.IsError)
            _logger.Error($"Initiating graceful shutdown due to comparison mismatch: {reason}");

        if (processExitSource is not null)
        {
            if (_logger.IsInfo)
                _logger.Info("Triggering graceful shutdown via ProcessExitSource");

            processExitSource.Exit(ExitCodes.GeneralError);
        }
        else
        {
            if (_logger.IsWarn)
                _logger.Warn("ProcessExitSource not available, using Environment.Exit as fallback");

            Task.Run(async () =>
            {
                try
                {
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
