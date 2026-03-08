// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Int256;
using Nethermind.Blockchain;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.State;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Core execution engine containing all Arbitrum block production and state management logic.
/// </summary>
public sealed class ArbitrumExecutionEngine(
    ArbitrumBlockTreeInitializer initializer,
    IBlockTree blockTree,
    IManualBlockProductionTrigger trigger,
    ChainSpec chainSpec,
    IArbitrumSpecHelper specHelper,
    ILogManager logManager,
    CachedL1PriceData cachedL1PriceData,
    IArbitrumConfig arbitrumConfig,
    IStateReader stateReader,
    ArbitrumBlockFactory arbitrumBlockFactory)
    : IArbitrumExecutionEngine
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumExecutionEngine>();

    public IBlockTree BlockTree { get; } = blockTree;

    private readonly SemaphoreSlim _createBlocksSemaphore = new(1, 1);
    private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, arbitrumConfig, logManager);

    private IExpressLaneService? _expressLaneService;
    private AuctionResolutionQueue? _auctionResolutionQueue;

    private ArbitrumSequencerEngine? _sequencerEngine;

    public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
    {
        ResultWrapper<MessageResult>? existingGenesisResult = TryGetExistingGenesisResult("Genesis already initialized, skipping DigestInitMessage");
        if (existingGenesisResult is not null)
            return existingGenesisResult;

        if (message.InitialL1BaseFee.IsZero)
            return ResultWrapper<MessageResult>.Fail("InitialL1BaseFee must be greater than zero", ErrorCodes.InvalidParams);

        if (message.SerializedChainConfig is null || message.SerializedChainConfig.Length == 0)
            return ResultWrapper<MessageResult>.Fail("SerializedChainConfig must not be empty.", ErrorCodes.InvalidParams);

        ResultWrapper<ParsedInitMessage> initMessageResult = TryBuildInitMessage(
            chainSpec.ChainId,
            message.InitialL1BaseFee,
            message.SerializedChainConfig,
            "Failed to deserialize ChainConfig.");

        return initMessageResult.Result != Result.Success ?
            ResultWrapper<MessageResult>.Fail(initMessageResult.Result.Error!, initMessageResult.ErrorCode) :
            InitializeGenesisFromMessage(initMessageResult.Data, handleExceptions: false);
    }

    public async Task<ResultWrapper<MessageResult>> DigestMessageAsync(DigestMessageParameters parameters)
    {
        // Handle init message (Kind = Initialize) - used by external consensus layers like Nitro
        if (parameters.Message.Message.Header.Kind == ArbitrumL1MessageKind.Initialize)
            return HandleInitMessageFromDigest(parameters);

        ResultWrapper<long> blockNumberResult = MessageIndexToBlockNumber(parameters.Index);
        if (blockNumberResult.Result != Result.Success)
            return ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error!);

        return await arbitrumBlockFactory.DigestMessageAsync(blockNumberResult.Data, parameters.Message);
    }

    public async Task<ResultWrapper<MessageResult[]>> ReorgAsync(ReorgParameters parameters)
    {
        if (parameters.MsgIdxOfFirstMsgToAdd == 0)
            return ResultWrapper<MessageResult[]>.Fail("Cannot reorg to genesis", ErrorCodes.InternalError);

        ResultWrapper<long> blockNumResult = MessageIndexToBlockNumber(parameters.MsgIdxOfFirstMsgToAdd - 1);
        if (blockNumResult.Result != Result.Success)
            return ResultWrapper<MessageResult[]>.Fail(blockNumResult.Result.Error ?? "Unknown error converting message index", blockNumResult.ErrorCode);

        return await arbitrumBlockFactory.ReorgAsync(blockNumResult.Data, parameters.NewMessages);
    }

    public Task<ResultWrapper<MessageResult>> ResultAtMessageIndexAsync(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return Task.FromResult(ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Unknown error converting message index"));

            BlockHeader? blockHeader = BlockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.RequireCanonical);
            if (blockHeader == null)
                return Task.FromResult(ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound(blockNumberResult.Data)));

            if (_logger.IsTrace)
                _logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, _logger);
            return Task.FromResult(ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = blockHeader.Hash ?? Hash256.Zero,
                SendRoot = headerInfo.SendRoot,
            }));
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error processing ResultAtMessageIndex for message index {messageIndex}: {ex.Message}", ex);
            return Task.FromResult(ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError));
        }
    }

    public Task<ResultWrapper<ulong>> HeadMessageIndexAsync()
    {
        BlockHeader? header = BlockTree.FindLatestHeader();

        return header is null
            ? Task.FromResult(ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError))
            : Task.FromResult(BlockNumberToMessageIndex((ulong)header.Number));
    }

    public ResultWrapper<long> MessageIndexToBlockNumber(ulong messageIndex)
    {
        return MessageBlockConverter.MessageIndexToBlockNumber(messageIndex, specHelper);
    }

    public ResultWrapper<ulong> BlockNumberToMessageIndex(ulong blockNumber)
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

    public ResultWrapper<EmptyResponse> SetFinalityData(SetFinalityDataParams parameters)
    {
        try
        {
            if (_logger.IsDebug)
                _logger.Debug($"SetFinalityData called: safe={parameters.SafeFinalityData?.MsgIdx}, " +
                              $"finalized={parameters.FinalizedFinalityData?.MsgIdx}, " +
                              $"validated={parameters.ValidatedFinalityData?.MsgIdx}");

            // Convert RPC parameters to internal types
            ArbitrumFinalityData? safeFinalityData = parameters.SafeFinalityData?.ToArbitrumFinalityData();
            ArbitrumFinalityData? finalizedFinalityData = parameters.FinalizedFinalityData?.ToArbitrumFinalityData();
            ArbitrumFinalityData? validatedFinalityData = parameters.ValidatedFinalityData?.ToArbitrumFinalityData();

            // Set finality data
            _syncMonitor.SetFinalityData(safeFinalityData, finalizedFinalityData, validatedFinalityData);

            if (_logger.IsDebug)
                _logger.Debug("SetFinalityData completed successfully");

            return ResultWrapper<EmptyResponse>.Success(default);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"SetFinalityData failed: {ex.Message}", ex);

            return ResultWrapper<EmptyResponse>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<EmptyResponse> MarkFeedStart(ulong to)
    {
        try
        {
            cachedL1PriceData.MarkFeedStart(to);
            return ResultWrapper<EmptyResponse>.Success(default);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"MarkFeedStart failed: {ex.Message}", ex);

            return ResultWrapper<EmptyResponse>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<EmptyResponse> SetConsensusSyncData(SetConsensusSyncDataParams? parameters)
    {
        if (parameters is null)
            return ResultWrapper<EmptyResponse>.Fail("Parameters cannot be null", ErrorCodes.InvalidParams);

        try
        {
            _syncMonitor.SetConsensusSyncData(
                parameters.Synced,
                parameters.MaxMessageCount,
                parameters.SyncProgressMap,
                parameters.UpdatedAt);

            return ResultWrapper<EmptyResponse>.Success(default);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"SetConsensusSyncData failed: {ex.Message}", ex);

            return ResultWrapper<EmptyResponse>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<bool> Synced()
    {
        try
        {
            return ResultWrapper<bool>.Success(_syncMonitor.IsSynced());
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Synced failed: {ex.Message}", ex);
            return ResultWrapper<bool>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public ResultWrapper<Dictionary<string, object>> FullSyncProgressMap()
    {
        try
        {
            Dictionary<string, object> progressMap = _syncMonitor.GetFullSyncProgressMap();
            return ResultWrapper<Dictionary<string, object>>.Success(progressMap);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"FullSyncProgressMap failed: {ex.Message}", ex);
            return ResultWrapper<Dictionary<string, object>>.Fail(ArbitrumRpcErrors.InternalError);
        }
    }

    public Task<ResultWrapper<ulong>> ArbOSVersionForMessageIndexAsync(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return Task.FromResult(ResultWrapper<ulong>.Fail(
                    blockNumberResult.Result.Error ?? "Failed to convert message index to block number"));

            BlockHeader? blockHeader = BlockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.RequireCanonical);
            if (blockHeader == null)
                return Task.FromResult(ResultWrapper<ulong>.Fail(ArbitrumRpcErrors.BlockNotFound(blockNumberResult.Data)));

            if (_logger.IsTrace)
                _logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

            ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, _logger);

            return Task.FromResult(ResultWrapper<ulong>.Success(headerInfo.ArbOSFormatVersion));
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Error processing ArbOSVersionForMessageIndex for message index {messageIndex}: {ex.Message}", ex);
            return Task.FromResult(ResultWrapper<ulong>.Fail(ArbitrumRpcErrors.InternalError));
        }
    }

    public Task<ResultWrapper<MaintenanceStatus>> MaintenanceStatusAsync()
        => Task.FromResult(ResultWrapper<MaintenanceStatus>.Success(new MaintenanceStatus { IsRunning = false }));

    public Task<ResultWrapper<bool>> ShouldTriggerMaintenanceAsync()
        => Task.FromResult(ResultWrapper<bool>.Success(false));

    public Task<ResultWrapper<string>> TriggerMaintenanceAsync()
        => Task.FromResult(ResultWrapper<string>.Success("OK"));

    public TransactionQueue? TransactionQueue => _sequencerEngine?.TransactionQueue;

    public void InitializeSequencer(
        DelayedMessageQueue delayedMessageQueue,
        SequencerState sequencerState,
        IExpressLaneService? expressLaneService = null,
        AuctionResolutionQueue? auctionResolutionQueue = null,
        TransactionQueue? transactionQueue = null)
    {
        _expressLaneService = expressLaneService;
        _auctionResolutionQueue = auctionResolutionQueue;

        transactionQueue ??= new(1024, arbitrumConfig.SequencerMaxTxDataSize, arbitrumConfig.SequencerAwaitTxResult);

        _sequencerEngine = new ArbitrumSequencerEngine(
            BlockTree,
            trigger,
            specHelper,
            delayedMessageQueue,
            sequencerState,
            _createBlocksSemaphore,
            cachedL1PriceData,
            logManager,
            arbitrumConfig,
            stateReader,
            transactionQueue,
            expressLaneService,
            auctionResolutionQueue);
    }

    public Task<ResultWrapper<StartSequencingResult>> StartSequencingAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp)
        => RunSequencerOpAsync(seq => seq.StartSequencingAsync(l1BlockNumber, l1Timestamp, timestamp), nameof(StartSequencingAsync));

    public ResultWrapper<EmptyResponse> EndSequencing(string? error)
        => RunSequencerAction(seq => seq.EndSequencing(error), nameof(EndSequencing));

    public Task<ResultWrapper<EmptyResponse>> AppendLastSequencedBlockAsync()
        => RunSequencerActionAsync(seq => seq.AppendLastSequencedBlockAsync(), nameof(AppendLastSequencedBlockAsync));

    public ResultWrapper<EmptyResponse> EnqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx)
        => RunSequencerAction(seq => seq.EnqueueDelayedMessages(messages, firstMsgIdx), nameof(EnqueueDelayedMessages));

    public ResultWrapper<ulong> NextDelayedMessageNumber()
        => RunSequencerOp(seq => seq.NextDelayedMessageNumber(), nameof(NextDelayedMessageNumber));

    public Task<ResultWrapper<SequencedMsg?>> ResequenceReorgedMessageAsync(MessageWithMetadata? msg)
        => RunSequencerOpAsync(seq => seq.ResequenceReorgedMessageAsync(msg), nameof(ResequenceReorgedMessageAsync));

    public ResultWrapper<EmptyResponse> Pause()
        => RunSequencerAction(seq => seq.Pause(), nameof(Pause));

    public ResultWrapper<EmptyResponse> Activate()
        => RunSequencerAction(seq => seq.Activate(), nameof(Activate));

    public ResultWrapper<EmptyResponse> ForwardTo(string url)
        => RunSequencerAction(seq => seq.ForwardTo(url), nameof(ForwardTo));

    public async Task<ResultWrapper<bool>> PublishAuctionResolutionTransactionAsync(byte[] rlpTransaction)
    {
        if (!arbitrumConfig.TimeboostEnabled)
            return ResultWrapper<bool>.Fail("Timeboost is not enabled");

        if (_auctionResolutionQueue is null || _expressLaneService is null)
            return ResultWrapper<bool>.Fail("Timeboost not initialized");

        Transaction tx;
        try
        {
            tx = Rlp.Decode<Transaction>(rlpTransaction);
        }
        catch (Exception ex)
        {
            return ResultWrapper<bool>.Fail($"Failed to decode transaction: {ex.Message}");
        }

        if (tx.To != _expressLaneService.AuctionContractAddress)
            return ResultWrapper<bool>.Fail($"Transaction must target the auction contract {_expressLaneService.AuctionContractAddress}");

        if (string.IsNullOrEmpty(arbitrumConfig.TimeboostAuctioneerAddress))
            return ResultWrapper<bool>.Fail("TimeboostAuctioneerAddress is not configured");

        Address expectedAuctioneer = new(arbitrumConfig.TimeboostAuctioneerAddress);
        Address? sender = new EthereumEcdsa(chainSpec.ChainId).RecoverAddress(tx);
        if (sender != expectedAuctioneer)
            return ResultWrapper<bool>.Fail($"Transaction sender {sender} is not the authorized auctioneer {expectedAuctioneer}");

        if (!_expressLaneService.IsWithinAuctionCloseWindow(DateTime.UtcNow))
            return ResultWrapper<bool>.Fail("Not within the auction close window");

        TxQueueItem item = new(tx, CancellationToken.None);
        await _auctionResolutionQueue.Writer.WriteAsync(item);
        return ResultWrapper<bool>.Success(true);
    }

    public async Task<ResultWrapper<bool>> PublishExpressLaneTransactionAsync(ExpressLaneSubmissionForRpc rpcSubmission)
    {
        if (!arbitrumConfig.TimeboostEnabled)
            return ResultWrapper<bool>.Fail("Timeboost is not enabled");

        if (_expressLaneService is null || _sequencerEngine is null)
            return ResultWrapper<bool>.Fail("Timeboost not initialized");

        Transaction tx;
        try
        {
            tx = Rlp.Decode<Transaction>(rpcSubmission.Transaction);
        }
        catch (Exception ex)
        {
            return ResultWrapper<bool>.Fail($"Failed to decode transaction: {ex.Message}");
        }

        ExpressLaneSubmission submission = new()
        {
            Transaction = tx,
            Round = rpcSubmission.Round,
            SequenceNumber = rpcSubmission.SequenceNumber,
            Signature = rpcSubmission.Signature,
            ChainId = rpcSubmission.ChainId,
            AuctionContractAddress = rpcSubmission.AuctionContractAddress
        };

        ulong currentBlock = (ulong)BlockTree.Head!.Header.Number;

        try
        {
            await _expressLaneService.SequenceAsync(submission, currentBlock);
            return ResultWrapper<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return ResultWrapper<bool>.Fail(ex.Message);
        }
    }

    private ResultWrapper<T> RunSequencerOp<T>(Func<ArbitrumSequencerEngine, T> action, string opName)
    {
        if (_sequencerEngine is null)
            return ResultWrapper<T>.Fail("Sequencer not enabled");

        try
        {
            T result = action(_sequencerEngine);
            return ResultWrapper<T>.Success(result);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"{opName} failed: {ex.Message}", ex);
            return ResultWrapper<T>.Fail(ArbitrumRpcErrors.InternalError, ErrorCodes.InternalError);
        }
    }

    private ResultWrapper<EmptyResponse> RunSequencerAction(Action<ArbitrumSequencerEngine> action, string opName)
    {
        _logger.Warn($"Sequencer: {opName}");

        if (_sequencerEngine is null)
            return ResultWrapper<EmptyResponse>.Fail("Sequencer not enabled");

        try
        {
            action(_sequencerEngine);
            return ResultWrapper<EmptyResponse>.Success(default);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"{opName} failed: {ex.Message}", ex);
            return ResultWrapper<EmptyResponse>.Fail(ArbitrumRpcErrors.InternalError, ErrorCodes.InternalError);
        }
    }

    private async Task<ResultWrapper<T>> RunSequencerOpAsync<T>(Func<ArbitrumSequencerEngine, Task<T>> action, string opName)
    {
        _logger.Warn($"Sequencer: {opName}");

        if (_sequencerEngine is null)
            return ResultWrapper<T>.Fail("Sequencer not enabled");

        try
        {
            T result = await action(_sequencerEngine);
            return ResultWrapper<T>.Success(result);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"{opName} failed: {ex.Message}", ex);
            return ResultWrapper<T>.Fail(ArbitrumRpcErrors.InternalError, ErrorCodes.InternalError);
        }
    }

    private async Task<ResultWrapper<EmptyResponse>> RunSequencerActionAsync(Func<ArbitrumSequencerEngine, Task> action, string opName)
    {
        _logger.Warn($"Sequencer: {opName}");

        if (_sequencerEngine is null)
            return ResultWrapper<EmptyResponse>.Fail("Sequencer not enabled");

        try
        {
            await action(_sequencerEngine);
            return ResultWrapper<EmptyResponse>.Success(default);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"{opName} failed: {ex.Message}", ex);
            return ResultWrapper<EmptyResponse>.Fail(ArbitrumRpcErrors.InternalError, ErrorCodes.InternalError);
        }
    }

    public async Task<ResultWrapper<MessageResult>> ProduceBlockWithoutWaitingOnProcessingQueueAsync(MessageWithMetadata messageWithMetadata, long blockNumber, BlockHeader? headBlockHeader)
    {
        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = messageWithMetadata,
            Number = blockNumber,
            PreviousArbosVersion = headBlockHeader != null ? ArbitrumBlockHeaderInfo.Deserialize(headBlockHeader, _logger).ArbOSFormatVersion : 0
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
        ArbitrumBlockHeaderInfo headerInfo = ArbitrumBlockHeaderInfo.Deserialize(block.Header, _logger);

        // ArbitrumBlockHeaderInfo.Deserialize returns Empty if deserialization fails
        if (headerInfo == ArbitrumBlockHeaderInfo.Empty && _logger.IsWarn)
            _logger.Warn($"Block header info deserialization returned empty result for block {block.Hash}");

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
            _logger.Error("Failed to deserialize ChainConfig from bytes.", exception);
            chainConfig = null;
            return false;
        }
    }

    private ResultWrapper<MessageResult> HandleInitMessageFromDigest(DigestMessageParameters parameters)
    {
        ResultWrapper<MessageResult>? existingGenesisResult = TryGetExistingGenesisResult(
            $"Genesis already initialized, returning existing hash: {BlockTree.Genesis?.Hash}");
        if (existingGenesisResult is not null)
            return existingGenesisResult;

        // Parse L2Msg: [32-byte chainId][1-byte version][remaining: config/basefee]
        byte[]? l2Msg = parameters.Message.Message.L2Msg;
        if (l2Msg is null || l2Msg.Length < 33)
            return ResultWrapper<MessageResult>.Fail("Invalid init message: L2Msg too short", ErrorCodes.InvalidParams);

        // Extract chainId (first 32 bytes)
        UInt256 chainId = new(l2Msg.AsSpan(0, 32), isBigEndian: true);

        byte version = l2Msg[32];
        byte[] serializedChainConfig;
        UInt256 initialL1BaseFee;

        switch (version)
        {
            case 0:
                {
                    // Version 0: chainId(32) + version(1) + JSON config
                    serializedChainConfig = l2Msg[33..];
                    initialL1BaseFee = specHelper.InitialL1BaseFee;
                    if (_logger.IsDebug)
                        _logger.Debug($"Init message v0: chainId={chainId}, using default L1BaseFee={initialL1BaseFee}");
                    break;
                }
            // Version 1: chainId(32) + version(1) + basefee(32) + JSON config
            case 1 when l2Msg.Length < 65:
                return ResultWrapper<MessageResult>.Fail("Invalid init message v1: too short for basefee", ErrorCodes.InvalidParams);
            case 1:
                {
                    initialL1BaseFee = new UInt256(l2Msg.AsSpan(33, 32), isBigEndian: true);
                    serializedChainConfig = l2Msg[65..];
                    if (_logger.IsDebug)
                        _logger.Debug($"Init message v1: chainId={chainId}, L1BaseFee={initialL1BaseFee}");
                    break;
                }
            default:
                return ResultWrapper<MessageResult>.Fail($"Unknown init message version: {version}", ErrorCodes.InvalidParams);
        }

        ResultWrapper<ParsedInitMessage> initMessageResult = TryBuildInitMessage(
            (ulong)chainId,
            initialL1BaseFee,
            serializedChainConfig,
            "Failed to deserialize ChainConfig from init message");

        return initMessageResult.Result != Result.Success ?
            ResultWrapper<MessageResult>.Fail(initMessageResult.Result.Error!, initMessageResult.ErrorCode) :
            InitializeGenesisFromMessage(initMessageResult.Data, handleExceptions: true);
    }

    private ResultWrapper<MessageResult>? TryGetExistingGenesisResult(string debugMessage)
    {
        BlockHeader? existingGenesis = BlockTree.Genesis;
        if (existingGenesis is null)
            return null;

        if (_logger.IsDebug)
            _logger.Debug(debugMessage);

        return ResultWrapper<MessageResult>.Success(new MessageResult
        {
            BlockHash = existingGenesis.Hash ?? throw new InvalidOperationException("Genesis hash is null"),
            SendRoot = Hash256.Zero
        });
    }

    private ResultWrapper<ParsedInitMessage> TryBuildInitMessage(
        ulong chainId,
        UInt256 initialL1BaseFee,
        byte[] serializedChainConfig,
        string deserializeErrorMessage)
    {
        if (!TryDeserializeChainConfig(serializedChainConfig, out ChainConfig? chainConfig))
            return ResultWrapper<ParsedInitMessage>.Fail(deserializeErrorMessage, ErrorCodes.InvalidParams);

        return ResultWrapper<ParsedInitMessage>.Success(new ParsedInitMessage(chainId, initialL1BaseFee, chainConfig, serializedChainConfig));
    }

    /// <summary>
    /// Initializes genesis block from parsed init message data.
    /// </summary>
    private ResultWrapper<MessageResult> InitializeGenesisFromMessage(ParsedInitMessage initMessage, bool handleExceptions)
    {
        if (!handleExceptions)
            return InitializeGenesisFromMessageInternal(initMessage);

        try
        {
            return InitializeGenesisFromMessageInternal(initMessage);
        }
        catch (Exception ex)
        {
            if (_logger.IsError)
                _logger.Error($"Failed to initialize genesis from digestMessage: {ex.Message}", ex);
            return ResultWrapper<MessageResult>.Fail($"Failed to initialize genesis: {ex.Message}", ErrorCodes.InternalError);
        }
    }

    private ResultWrapper<MessageResult> InitializeGenesisFromMessageInternal(ParsedInitMessage initMessage)
    {
        BlockHeader genesisHeader = initializer.Initialize(initMessage);

        if (_logger.IsInfo)
            _logger.Info($"Genesis initialized from digestMessage: Hash={genesisHeader.Hash}, ChainId={initMessage.ChainId}");

        return ResultWrapper<MessageResult>.Success(new MessageResult
        {
            BlockHash = genesisHeader.Hash ?? throw new InvalidOperationException("Genesis hash is null"),
            SendRoot = Hash256.Zero
        });
    }
}
