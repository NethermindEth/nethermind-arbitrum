// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Math;
using Nethermind.Blockchain;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumRpcModule(
    ArbitrumBlockTreeInitializer initializer,
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    ArbitrumRpcTxSource txSource,
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager,
    CachedL1PriceData cachedL1PriceData,
    IBlockProcessingQueue processingQueue,
    IArbitrumConfig arbitrumConfig) : IArbitrumRpcModule
{
    protected readonly SemaphoreSlim CreateBlocksSemaphore = new(1, 1);

    protected readonly ILogger Logger = logManager.GetClassLogger<ArbitrumRpcModule>();
    private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, arbitrumConfig, logManager);

    public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
    {
        if (message.InitialL1BaseFee.IsZero)
            return ResultWrapper<MessageResult>.Fail("InitialL1BaseFee must be greater than zero", ErrorCodes.InvalidParams);

        if (message.SerializedChainConfig is null || message.SerializedChainConfig.Length == 0)
            return ResultWrapper<MessageResult>.Fail("SerializedChainConfig must not be empty.", ErrorCodes.InvalidParams);

        if (!TryDeserializeChainConfig(message.SerializedChainConfig, out ChainConfig? chainConfig))
            return ResultWrapper<MessageResult>.Fail("Failed to deserialize ChainConfig.", ErrorCodes.InvalidParams);

        ParsedInitMessage initMessage = new(chainSpec.ChainId, message.InitialL1BaseFee, chainConfig, message.SerializedChainConfig);
        BlockHeader genesisHeader = initializer.Initialize(initMessage);

        return ResultWrapper<MessageResult>.Success(new()
        {
            BlockHash = genesisHeader.Hash ?? throw new InvalidOperationException("Genesis block hash must not be null"),
            SendRoot = Hash256.Zero
        });
    }

    public virtual async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
    {
        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtMessageIndex(parameters.Index);
        if (resultAtMessageIndex.Result == Result.Success)
            return resultAtMessageIndex;

        // Non-blocking attempt to acquire the semaphore.
        if (!await CreateBlocksSemaphore.WaitAsync(0))
            return ResultWrapper<MessageResult>.Fail("CreateBlock mutex held.", ErrorCodes.InternalError);

        try
        {
            _ = txSource; // TODO: replace with the actual use

            long blockNumber = (await MessageIndexToBlockNumber(parameters.Index)).Data;
            BlockHeader? headBlockHeader = blockTree.Head?.Header;

            if (headBlockHeader is not null && headBlockHeader.Number + 1 != blockNumber)
                return ResultWrapper<MessageResult>.Fail(
                    $"Wrong block number in digest got {blockNumber} expected {headBlockHeader.Number}");

            return await ProduceBlockWhileLockedAsync(parameters.Message, blockNumber, headBlockHeader);
        }
        finally
        {
            // Ensure the semaphore is released, equivalent to Go's `defer Unlock()`.
            CreateBlocksSemaphore.Release();
        }
    }

    public async Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = await MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Unknown error converting message index");

            BlockHeader? blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.None);
            if (blockHeader == null)
                return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound);

            if (Logger.IsTrace)
                Logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, Logger);
            return ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = blockHeader.Hash ?? Hash256.Zero,
                SendRoot = headerInfo.SendRoot,
            });
        }
        catch (Exception ex)
        {
            if (Logger.IsError)
                Logger.Error($"Error processing ResultAtMessageIndex for message index {messageIndex}: {ex.Message}", ex);
            return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }
    public Task<ResultWrapper<ulong>> HeadMessageIndex()
    {
        BlockHeader? header = blockTree.FindLatestHeader();

        return header is null
            ? ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError)
            : BlockNumberToMessageIndex((ulong)header.Number);
    }

    public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
    {
        try
        {
            long blockNumber = MessageBlockConverter.MessageIndexToBlockNumber(messageIndex, specHelper);
            return ResultWrapper<long>.Success(blockNumber);
        }
        catch (OverflowException)
        {
            return ResultWrapper<long>.Fail(ArbitrumRpcErrors.Overflow);
        }
    }

    public Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber)
    {
        try
        {
            ulong messageIndex = MessageBlockConverter.BlockNumberToMessageIndex(blockNumber, specHelper);
            return ResultWrapper<ulong>.Success(messageIndex);
        }
        catch (ArgumentOutOfRangeException)
        {
            ulong genesis = specHelper.GenesisBlockNum;
            return ResultWrapper<ulong>.Fail(
                $"blockNumber {blockNumber} < genesis {genesis}");
        }
    }

    public ResultWrapper<string> SetFinalityData(SetFinalityDataParams? parameters)
    {
        if (parameters is null)
            return ResultWrapper<string>.Fail(ArbitrumRpcErrors.FormatNullParameters(), ErrorCodes.InvalidParams);

        try
        {
            if (Logger.IsDebug)
                Logger.Debug($"SetFinalityData called: safe={parameters.SafeFinalityData?.MsgIdx}, " +
                              $"finalized={parameters.FinalizedFinalityData?.MsgIdx}, " +
                              $"validated={parameters.ValidatedFinalityData?.MsgIdx}");

            // Convert RPC parameters to internal types
            ArbitrumFinalityData? safeFinalityData = parameters.SafeFinalityData?.ToArbitrumFinalityData();
            ArbitrumFinalityData? finalizedFinalityData = parameters.FinalizedFinalityData?.ToArbitrumFinalityData();
            ArbitrumFinalityData? validatedFinalityData = parameters.ValidatedFinalityData?.ToArbitrumFinalityData();

            // Set finality data
            _syncMonitor.SetFinalityData(safeFinalityData, finalizedFinalityData, validatedFinalityData);

            if (Logger.IsDebug)
                Logger.Debug("SetFinalityData completed successfully");

            return ResultWrapper<string>.Success("OK");
        }
        catch (Exception ex)
        {
            if (Logger.IsError)
                Logger.Error($"SetFinalityData failed: {ex.Message}", ex);

            return ResultWrapper<string>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<string> MarkFeedStart(ulong to)
    {
        try
        {
            cachedL1PriceData.MarkFeedStart(to);
            return ResultWrapper<string>.Success("OK");
        }
        catch (Exception ex)
        {
            if (Logger.IsError)
                Logger.Error($"MarkFeedStart failed: {ex.Message}", ex);

            return ResultWrapper<string>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    protected async Task<ResultWrapper<MessageResult>> ProduceBlockWhileLockedAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
    {
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber
        };

        try
        {
            Block? block = await trigger.BuildBlock(parentHeader: headBlockHeader, payloadAttributes: payload);
            if (block?.Hash is null)
                return ResultWrapper<MessageResult>.Fail("Failed to build block or block has no hash.", ErrorCodes.InternalError);

            return ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = block.Hash!,
                SendRoot = GetSendRootFromBlock(block)
            });
        }
        catch (TimeoutException)
        {
            return ResultWrapper<MessageResult>.Fail("Timeout waiting for block processing result.", ErrorCodes.Timeout);
        }
    }

    private Hash256 GetSendRootFromBlock(Block block)
    {
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, Logger);

        // ArbitrumBlockHeaderInfo.Deserialize returns Empty if deserialization fails
        if (headerInfo == ArbitrumBlockHeaderInfo.Empty && Logger.IsWarn)
            Logger.Warn($"Block header info deserialization returned empty result for block {block.Hash}");

        return headerInfo.SendRoot;
    }

    private bool TryDeserializeChainConfig(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out ChainConfig? chainConfig)
    {
        try
        {
            chainConfig = JsonSerializer.Deserialize<ChainConfig>(bytes);
            return chainConfig != null;
        }
        catch (Exception exception)
        {
            Logger.Error("Failed to deserialize ChainConfig from bytes.", exception);
            chainConfig = null;
            return false;
        }
    }
}
