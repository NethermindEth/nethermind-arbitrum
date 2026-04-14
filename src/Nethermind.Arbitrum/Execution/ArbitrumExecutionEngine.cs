// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Execution.Stateless;
using Nethermind.Arbitrum.Genesis;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Modules;
using Nethermind.Arbitrum.Sequencer;
using Nethermind.Arbitrum.Sequencer.Queues;
using Nethermind.Arbitrum.Sequencer.Timeboost;
using Nethermind.Int256;
using Nethermind.Blockchain;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Stateless;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Arbitrum.Stylus;

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
    IArbitrumWitnessGeneratingBlockProcessingEnvFactory witnessGeneratingBlockProcessingEnvFactory,
    ArbitrumBlockFactory arbitrumBlockFactory,
    IArbitrumSequencerEngine sequencerEngine,
    IExpressLaneService expressLaneService,
    IExpressLaneTracker expressLaneTracker,
    IAuctionResolutionQueue auctionResolutionQueue,
    IEthereumEcdsa ethereumEcdsa,
    StateReconstructor stateReconstructor)
    : IArbitrumExecutionEngine
{
    private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumExecutionEngine>();

    private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, arbitrumConfig, logManager);

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

        ResultWrapper<MessageResult> resultAtMessageIndex = await ResultAtMessageIndexAsync(parameters.Index);
        if (resultAtMessageIndex.Result == Result.Success)
            return resultAtMessageIndex;

        ResultWrapper<Block> blockResult = await arbitrumBlockFactory.DigestMessageAsync(blockNumberResult.Data, parameters.Message);
        if (blockResult.Result != Result.Success)
            return ResultWrapper<MessageResult>.Fail(blockResult.Result.Error!, blockResult.ErrorCode);

        return ResultWrapper<MessageResult>.Success(new()
        {
            BlockHash = blockResult.Data.Hash!,
            SendRoot = GetSendRootFromBlock(blockResult.Data)
        });
    }

    public async Task<ResultWrapper<MessageResult[]>> ReorgAsync(ReorgParameters parameters)
    {
        if (parameters.MsgIdxOfFirstMsgToAdd == 0)
            return ResultWrapper<MessageResult[]>.Fail("Cannot reorg to genesis", ErrorCodes.InternalError);

        ResultWrapper<long> blockNumResult = MessageIndexToBlockNumber(parameters.MsgIdxOfFirstMsgToAdd - 1);
        if (blockNumResult.Result != Result.Success)
            return ResultWrapper<MessageResult[]>.Fail(blockNumResult.Result.Error ?? "Unknown error converting message index", blockNumResult.ErrorCode);

        ResultWrapper<Block[]> reorgedBlocks = await arbitrumBlockFactory.ReorgAsync(blockNumResult.Data, parameters.NewMessages);
        if (reorgedBlocks.Result != Result.Success)
            return ResultWrapper<MessageResult[]>.Fail(reorgedBlocks.Result.Error ?? "Unknown error during reorg", reorgedBlocks.ErrorCode);

        MessageResult[] results = reorgedBlocks.Data.Select(block => new MessageResult
        {
            BlockHash = block.Hash!,
            SendRoot = GetSendRootFromBlock(block)
        }).ToArray();

        return ResultWrapper<MessageResult[]>.Success(results);
    }

    public Task<ResultWrapper<MessageResult>> ResultAtMessageIndexAsync(ulong messageIndex)
    {
        try
        {
            ResultWrapper<long> blockNumberResult = MessageIndexToBlockNumber(messageIndex);
            if (blockNumberResult.Result != Result.Success)
                return Task.FromResult(ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Unknown error converting message index"));

            BlockHeader? blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.RequireCanonical);
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
        BlockHeader? header = blockTree.FindLatestHeader();

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

            if (arbitrumConfig.ValidationEnabled && validatedFinalityData.HasValue)
                MarkValid(new MarkValidParameters(validatedFinalityData.Value.MessageIndex, validatedFinalityData.Value.BlockHash));

            if (_logger.IsDebug)
                _logger.Debug("SetFinalityData completed successfully");

            return ResultWrapper.EmptySuccess;
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
            return ResultWrapper.EmptySuccess;
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

            return ResultWrapper.EmptySuccess;
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

            BlockHeader? blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.RequireCanonical);
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

    public Task<ResultWrapper<StartSequencingResult>> StartSequencingAsync(ulong l1BlockNumber, ulong l1Timestamp, ulong timestamp)
        => sequencerEngine.StartSequencingAsync(l1BlockNumber, l1Timestamp, timestamp);

    public Task<ResultWrapper<EmptyResponse>> EndSequencingAsync(string? error)
        => sequencerEngine.EndSequencingAsync(error);

    public Task<ResultWrapper<EmptyResponse>> AppendLastSequencedBlockAsync()
        => sequencerEngine.AppendLastSequencedBlockAsync();

    public ResultWrapper<EmptyResponse> EnqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx)
        => sequencerEngine.EnqueueDelayedMessages(messages, firstMsgIdx);

    public ResultWrapper<ulong> NextDelayedMessageNumber()
        => sequencerEngine.NextDelayedMessageNumber();

    public Task<ResultWrapper<SequencedMsg?>> ResequenceReorgedMessageAsync(MessageWithMetadata? msg)
        => sequencerEngine.ResequenceReorgedMessageAsync(msg);

    public ResultWrapper<EmptyResponse> Pause()
        => sequencerEngine.Pause();

    public ResultWrapper<EmptyResponse> Activate()
        => sequencerEngine.Activate();

    public ResultWrapper<EmptyResponse> ForwardTo(string url)
        => sequencerEngine.ForwardTo(url);

    public async Task<ResultWrapper<bool>> PublishAuctionResolutionTransactionAsync(byte[] rlpTransaction)
    {
        if (!arbitrumConfig.TimeboostEnabled)
            return ResultWrapper<bool>.Fail("Timeboost is not enabled");

        Transaction tx;
        try
        {
            tx = Rlp.Decode<Transaction>(rlpTransaction);
        }
        catch (Exception ex)
        {
            return ResultWrapper<bool>.Fail($"Failed to decode transaction: {ex.Message}");
        }

        if (tx.To != expressLaneTracker.AuctionContractAddress)
            return ResultWrapper<bool>.Fail($"Transaction must target the auction contract {expressLaneTracker.AuctionContractAddress}");

        if (string.IsNullOrEmpty(arbitrumConfig.TimeboostAuctioneerAddress))
            return ResultWrapper<bool>.Fail("TimeboostAuctioneerAddress is not configured");

        Address expectedAuctioneer = new(arbitrumConfig.TimeboostAuctioneerAddress);
        Address? sender = ethereumEcdsa.RecoverAddress(tx);
        if (sender != expectedAuctioneer)
            return ResultWrapper<bool>.Fail($"Transaction sender {sender} is not the authorized auctioneer {expectedAuctioneer}");

        if (!expressLaneTracker.IsWithinAuctionCloseWindow(DateTime.UtcNow))
            return ResultWrapper<bool>.Fail("Not within the auction close window");

        TxQueueItem item = TxQueueItem.CreateRegular(tx);
        await auctionResolutionQueue.WriteAsync(item);
        return ResultWrapper<bool>.Success(true);
    }

    public async Task<ResultWrapper<bool>> PublishExpressLaneTransactionAsync(ExpressLaneSubmissionForRpc rpcSubmission)
    {
        if (!arbitrumConfig.TimeboostEnabled)
            return ResultWrapper<bool>.Fail("Timeboost is not enabled");

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
            AuctionContractAddress = rpcSubmission.AuctionContractAddress,
            Options = rpcSubmission.Options
        };

        ulong currentBlock = (ulong)blockTree.Head!.Header.Number;

        ResultWrapper<EmptyResponse> result = await expressLaneService.SequenceAsync(submission, currentBlock);
        return result.Result == Result.Success
            ? ResultWrapper<bool>.Success(true)
            : ResultWrapper<bool>.Fail(result.Result.Error ?? "Express lane sequencing failed");
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

    public async Task<ResultWrapper<RecordResult>> RecordBlockCreation(RecordBlockCreationParameters parameters)
    {
        await stateReconstructor.WaitForPruningGateAsync();
        long blockNumber = MessageIndexToBlockNumber(parameters.Index).Data;
        if (blockNumber == 0)
        {
            // Cannot generate witness for genesis block as the block itself does not contain any transaction
            // responsible for the state setup. It is the weak subjectivity starting point to trust.
            return ResultWrapper<RecordResult>.Fail($"Cannot generate witness for genesis block");
        }

        BlockHeader? parent = blockTree.FindHeader(blockNumber - 1);
        if (parent is null)
        {
            return ResultWrapper<RecordResult>.Fail($"Unable to find parent for block {blockNumber}");
        }

        ArbitrumPayloadAttributes payload = new()
        {
            MessageWithMetadata = parameters.Message,
            Number = blockNumber
        };

        // References temporarily parent trie
        stateReconstructor.EnsureStateAvailable(parent);

        string[] wasmTargets = parameters.WasmTargets;
        string localTarget = StylusTargets.GetLocalTargetName();
        if (!wasmTargets.Contains(localTarget))
            wasmTargets = wasmTargets.Append(localTarget).ToArray();

        try
        {
            using IWitnessGeneratingBlockProcessingEnvScope scope = witnessGeneratingBlockProcessingEnvFactory.CreateScope(wasmTargets);
            IBlockBuildingWitnessCollector witnessCollector = ((IWitnessGeneratingPolyvalentEnv)scope.Env).CreateBlockBuildingWitnessCollector();
            (Block builtBlock, ArbitrumWitness witness) = await witnessCollector.BuildBlockAndGetWitness(parent, payload);

            using (witness)
            {
                if (builtBlock.Hash is null)
                    return ResultWrapper<RecordResult>.Fail($"Failed to build block {blockNumber} or block has no hash.");

                TaskCompletionSource<Hash256> blockAddedTcs = new();

                void OnBlockAddedToMain(object? sender, BlockReplacementEventArgs e)
                {
                    if (e.Block.Number == blockNumber)
                        blockAddedTcs.TrySetResult(e.Block.Hash!);
                }

                blockTree.BlockAddedToMain += OnBlockAddedToMain;

                try
                {
                    // Check immediately in case the block was committed before we subscribed
                    Hash256? canonicalHash = blockTree.FindCanonicalBlockInfo(blockNumber)?.BlockHash;
                    if (canonicalHash is null)
                    {
                        using CancellationTokenSource cts = arbitrumConfig.BuildProcessingTimeoutTokenSource();
                        canonicalHash = await blockAddedTcs.Task.WaitAsync(cts.Token);
                    }

                    if (canonicalHash != builtBlock.Hash)
                        return ResultWrapper<RecordResult>.Fail($"Built block hash: {builtBlock.Hash} does not match canonical block header hash: {canonicalHash}");

                    stateReconstructor.UpdateValidCandidateHeader(parent);

                    RecordResult result = new(parameters.Index, builtBlock.Hash!, witness);
                    return ResultWrapper<RecordResult>.Success(result);
                }
                catch (OperationCanceledException)
                {
                    return ResultWrapper<RecordResult>.Fail(ArbitrumRpcErrors.BlockNotFound(blockNumber));
                }
                finally
                {
                    blockTree.BlockAddedToMain -= OnBlockAddedToMain;
                }
            }
        }
        finally
        {
            // Removes temporary reference to parent trie
            // Gets removed by any execution path and after call to UpdateValidCandidateHeader
            stateReconstructor.DereferenceRoot(parent.StateRoot!);
        }
    }

    public ResultWrapper<EmptyResponse> PrepareForRecord(PrepareForRecordParameters parameters)
    {
        stateReconstructor.WaitForPruningGate();
        if (parameters.End < parameters.Start)
            return ResultWrapper<EmptyResponse>.Fail($"Invalid range: start {parameters.Start} > end {parameters.End}");

        ulong numOfBlocks = parameters.End + 1 - parameters.Start;
        long headerNum = MessageIndexToBlockNumber(parameters.Start).Data;
        if (parameters.Start > 0)
            headerNum--; // need to get previous as RecordBlockCreation executes from the parent block's state
        else
            numOfBlocks--; // genesis block doesn't need preparation, so recording one less block

        long lastHeaderNum = headerNum + (long)numOfBlocks;
        List<Hash256> referencedStateRoots = new List<Hash256>((int)numOfBlocks);

        for (long current = headerNum; current <= lastHeaderNum; current++)
        {
            BlockHeader? header = blockTree.FindHeader(current);
            if (header is null)
            {
                _logger.Warn($"PrepareForRecord: header not found for block {current}");
                break;
            }

            try
            {
                stateReconstructor.EnsureStateAvailable(header);
                stateReconstructor.UpdateValidCandidateHeader(header);
                referencedStateRoots.Add(header.StateRoot!);
            }
            catch (Exception ex)
            {
                _logger.Warn($"PrepareForRecord: failed to ensure state for block {current}: {ex.Message}");
                break;
            }
        }

        stateReconstructor.PreparedAddTrim(referencedStateRoots);

        return ResultWrapper<EmptyResponse>.Success(default);
    }

    private ResultWrapper<EmptyResponse> MarkValid(MarkValidParameters parameters)
    {
        stateReconstructor.WaitForPruningGate();
        ResultWrapper<long> blockNumberResult = MessageIndexToBlockNumber(parameters.Pos);
        if (blockNumberResult.Result != Result.Success)
            return ResultWrapper<EmptyResponse>.Fail(blockNumberResult.Result.Error!, blockNumberResult.ErrorCode);

        long validBlockNumber = blockNumberResult.Data;

        // Verify the canonical block at validBlockNumber is canonical
        Hash256? canonicalHash = blockTree.FindHeader(validBlockNumber, BlockTreeLookupOptions.RequireCanonical)?.Hash;
        if (canonicalHash != parameters.ResultHash)
        {
            if (_logger.IsError)
                _logger.Error($"MarkValid: canonical hash {canonicalHash} at block {validBlockNumber} does not match expected {parameters.ResultHash}");
            return ResultWrapper<EmptyResponse>.Success(default);
        }

        // Promote the candidate (its block number must be ≤ validBlockNumber)
        BlockHeader? validHeader = stateReconstructor.TryPromoteValidCandidate(validBlockNumber);

        if (validHeader is null)
        {
            if (_logger.IsWarn)
                _logger.Warn($"MarkValid: no candidate to promote for block {validBlockNumber}");
        }
        else if (_logger.IsDebug)
        {
            _logger.Debug($"MarkValid: promoted candidate block {validHeader.Number} hash {validHeader.Hash} as valid (validated at block {validBlockNumber}, hash={parameters.ResultHash})");
        }

        return ResultWrapper<EmptyResponse>.Success(default);
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
            $"Genesis already initialized, returning existing hash: {blockTree.Genesis?.Hash}");
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
        BlockHeader? existingGenesis = blockTree.Genesis;
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
