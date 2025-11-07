// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Modules;

public sealed class ArbitrumRpcModuleWithComparison : ArbitrumRpcModule
{
    private readonly ArbitrumComparisonRpcClient _comparisonRpcClient;
    private readonly long _verificationInterval;
    private long _lastVerifiedBlock;
    private readonly IBlockTree _blockTree;
    private readonly IBlocksConfig _blocksConfig;

    private Task? _blockPreWarmTask;
    private CancellationTokenSource? _prewarmCancellation;
    private readonly ArbitrumBlockProducer? _blockProducer;
    private readonly IProcessExitSource? _processExitSource;

    public ArbitrumRpcModuleWithComparison(ArbitrumBlockTreeInitializer initializer,
        IBlockTree blockTree,
        IManualBlockProductionTrigger trigger,
        ArbitrumRpcTxSource txSource,
        ChainSpec chainSpec,
        IArbitrumSpecHelper specHelper,
        ILogManager logManager,
        CachedL1PriceData cachedL1PriceData,
        IBlockProcessingQueue processingQueue,
        IArbitrumConfig arbitrumConfig,
        IVerifyBlockHashConfig verifyBlockHashConfig,
        IJsonSerializer jsonSerializer,
        IBlocksConfig blocksConfig,
        IBlockProducer? blockProducer,
        IProcessExitSource? processExitSource = null) : base(initializer, blockTree, trigger, txSource, chainSpec, specHelper, logManager, cachedL1PriceData, processingQueue, arbitrumConfig, blocksConfig, blockProducer)
    {
        _blockTree = blockTree;
        _blockProducer = blockProducer as ArbitrumBlockProducer;
        _processExitSource = processExitSource;
        _comparisonRpcClient = new ArbitrumComparisonRpcClient(verifyBlockHashConfig.ArbNodeRpcUrl!, jsonSerializer, logManager);
        _verificationInterval = (long)verifyBlockHashConfig.VerifyEveryNBlocks;
        _blocksConfig = blocksConfig;
    }

    public override async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
    {
        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtMessageIndex(parameters.Index);
        if (resultAtMessageIndex.Result == Result.Success)
            return resultAtMessageIndex;

        if (!await CreateBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);

        try
        {
            long blockNumber = (await MessageIndexToBlockNumber(parameters.Index)).Data;
            BlockHeader? headBlockHeader = _blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
                return ResultWrapper<MessageResult>.Fail(
                    $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");

            if (blockNumber % _verificationInterval == 0)
                return await DigestMessageWithComparisonAsync(parameters.Message, blockNumber, headBlockHeader, parameters.MessageForPrefetch);

            return await ProduceBlock(parameters.Message, blockNumber, headBlockHeader, parameters.MessageForPrefetch);
        }
        finally
        {
            CreateBlocksSemaphore.Release();
        }
    }

    private async Task<ResultWrapper<MessageResult>> DigestMessageWithComparisonAsync(
        MessageWithMetadata messageWithMetadata,
        long blockNumber,
        BlockHeader? headBlockHeader, MessageWithMetadata? messageForPrefetch)
    {
        if (Logger.IsInfo)
            Logger.Info($"Comparison mode: Processing block {blockNumber} with external RPC validation");

        Task<ResultWrapper<MessageResult>> digestTask = ProduceBlock(messageWithMetadata, blockNumber, headBlockHeader, messageForPrefetch);
        Task<ResultWrapper<MessageResult>> externalRpcTask = _comparisonRpcClient.GetBlockDataAsync(blockNumber);

        try
        {
            await Task.WhenAll(digestTask, externalRpcTask);

            ResultWrapper<MessageResult> digestResult = await digestTask;
            ResultWrapper<MessageResult> rpcResult = await externalRpcTask;

            if (digestResult.Result != Result.Success)
            {
                if (Logger.IsError)
                    Logger.Error($"Comparison mode: DigestMessage failed for block {blockNumber}: {digestResult.Result.Error}");
                return digestResult;
            }

            if (rpcResult.Result != Result.Success)
            {
                if (Logger.IsError)
                    Logger.Error($"Comparison mode: RPC call failed for block {blockNumber}: {rpcResult.Result.Error}");
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

                if (Logger.IsError)
                    Logger.Error(errorMessage);

                TriggerGracefulShutdown(errorMessage);
                return ResultWrapper<MessageResult>.Fail("Block comparison mismatch detected - shutting down", ErrorCodes.InternalError);
            }

            _lastVerifiedBlock = blockNumber;

            if (Logger.IsInfo)
                Logger.Info($"Comparison mode: Block {blockNumber} validation PASSED - hashes and sendRoots match");

            return digestResult;
        }
        catch (Exception ex)
        {
            if (Logger.IsError)
                Logger.Error($"Comparison mode: Unexpected error during comparison for block {blockNumber}: {ex.Message}", ex);

            if (digestTask.IsCompletedSuccessfully)
                return await digestTask;

            throw;
        }
    }

    private async Task<long> BinarySearchFirstMismatchAsync(long startBlock, long endBlock)
    {
        if (Logger.IsInfo)
            Logger.Info($"Binary search: Finding first mismatch between blocks {startBlock} and {endBlock}");

        return await BinarySearchAsync(startBlock, endBlock, IsBlockMismatchDetected, result: endBlock);
    }

    private async Task<long> BinarySearchAsync(long left, long right, Func<long, Task<bool>> predicateAsync, long result)
    {
        while (left < right)
        {
            long mid = left + (right - left) / 2;

            if (Logger.IsDebug)
                Logger.Debug($"Binary search: Checking index {mid} (range: {left}-{right})");

            bool predicateResult = await predicateAsync(mid);

            if (predicateResult)
            {
                if (Logger.IsDebug)
                    Logger.Debug($"Binary search: Condition met at {mid}, searching left half");
                result = mid;
                right = mid;
            }
            else
            {
                if (Logger.IsDebug)
                    Logger.Debug($"Binary search: Condition not met at {mid}, searching right half");
                left = mid + 1;
            }
        }

        if (Logger.IsInfo)
            Logger.Info($"Binary search: Result found at index {result}");

        return result;
    }

    private async Task<bool> IsBlockMismatchDetected(long blockNumber)
    {
        Task<ResultWrapper<MessageResult>> internalTask = ResultAtMessageIndex((ulong)blockNumber);
        Task<ResultWrapper<MessageResult>> externalTask = _comparisonRpcClient.GetBlockDataAsync(blockNumber);

        await Task.WhenAll(internalTask, externalTask);

        ResultWrapper<MessageResult> internalResult = await internalTask;
        ResultWrapper<MessageResult> externalResult = await externalTask;

        if (internalResult.Result != Result.Success)
        {
            if (Logger.IsError)
                Logger.Error($"Internal block {blockNumber} not found or failed");
            return true;
        }

        if (externalResult.Result != Result.Success)
        {
            if (Logger.IsError)
                Logger.Error($"External RPC for block {blockNumber} failed");
            return true;
        }

        return !internalResult.Data.Equals(externalResult.Data);
    }

    private void TriggerGracefulShutdown(string reason)
    {
        if (Logger.IsError)
            Logger.Error($"Initiating graceful shutdown due to comparison mismatch: {reason}");

        if (_processExitSource is not null)
        {
            if (Logger.IsInfo)
                Logger.Info("Triggering graceful shutdown via ProcessExitSource");

            _processExitSource.Exit(ExitCodes.GeneralError);
        }
        else
        {
            if (Logger.IsWarn)
                Logger.Warn("ProcessExitSource not available, using Environment.Exit as fallback");

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));

                    if (Logger.IsError)
                        Logger.Error("Shutting down process due to block comparison mismatch");

                    Environment.Exit(ExitCodes.GeneralError);
                }
                catch (Exception ex)
                {
                    if (Logger.IsError)
                        Logger.Error($"Error during shutdown: {ex.Message}", ex);

                    Environment.Exit(ExitCodes.GeneralError);
                }
            });
        }
    }

    private async Task<ResultWrapper<MessageResult>> ProduceBlock(MessageWithMetadata messageWithMetadata, long blockNumber,
        BlockHeader? headBlockHeader, MessageWithMetadata? messageForPrefetch)
    {
        if (_blockProducer is not null)
        {
            if (_prewarmCancellation is not null)
            {
                CancellationTokenExtensions.CancelDisposeAndClear(ref _prewarmCancellation);
                _prewarmCancellation = null;
            }
            _blockPreWarmTask?.GetAwaiter().GetResult();
            _blockPreWarmTask = null;
        }

        ResultWrapper<MessageResult> result = _blocksConfig.BuildBlocksOnMainState
            ? await ProduceBlockWithoutWaitingOnProcessingQueueAsync(messageWithMetadata, blockNumber, headBlockHeader)
            : await ProduceBlockWhileLockedAsync(messageWithMetadata, blockNumber, headBlockHeader);

        if (_blockProducer is not null)
        {
            _blockProducer.ClearPreWarmCaches();

            if (result.Result != Result.Success || messageForPrefetch is null)
                return result;

            headBlockHeader = _blockTree.Head?.Header;
            ArbitrumPayloadAttributes payload = new()
            {
                MessageWithMetadata = messageForPrefetch,
                Number = blockNumber + 1
            };
            _prewarmCancellation = new();
            _blockPreWarmTask = _blockProducer.PreWarmBlock(headBlockHeader, null, payload, IBlockProducer.Flags.None, _prewarmCancellation.Token);
        }
        return result;
    }
}
