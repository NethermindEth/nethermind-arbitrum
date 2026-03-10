// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// RPC module for the "nitroexecution" namespace. Thin facade that delegates to IArbitrumExecutionEngine.
/// </summary>
public class NitroExecutionRpcModule : INitroExecutionRpcModule
{
    private readonly IArbitrumExecutionEngine _engine;
    private readonly ILogger _logger;

    public NitroExecutionRpcModule(IArbitrumExecutionEngine engine, ILogManager logManager)
    {
        _engine = engine;
        _logger = logManager.GetClassLogger();
    }

    public Task<ResultWrapper<MessageResult>> nitroexecution_digestMessage(
        MessageIndex msgIdx,
        MessageWithMetadata message,
        MessageWithMetadata? messageForPrefetch)
    {
        if (_logger.IsDebug)
            _logger.Debug($"nitroexecution_digestMessage called: msgIdx={msgIdx}");
        DigestMessageParameters parameters = new(msgIdx, message, messageForPrefetch);
        return _engine.DigestMessageAsync(parameters);
    }

    public Task<ResultWrapper<MessageResult[]>> nitroexecution_reorg(
        MessageIndex msgIdxOfFirstMsgToAdd,
        MessageWithMetadataAndBlockInfo[] newMessages,
        MessageWithMetadata[] oldMessages)
    {
        if (_logger.IsDebug)
            _logger.Debug($"nitroexecution_reorg called: msgIdxOfFirstMsgToAdd={msgIdxOfFirstMsgToAdd}, newMessages={newMessages.Length}, oldMessages={oldMessages.Length}");
        ReorgParameters parameters = new(msgIdxOfFirstMsgToAdd, newMessages, oldMessages);
        return _engine.ReorgAsync(parameters);
    }

    public Task<ResultWrapper<MessageResult>> nitroexecution_resultAtMessageIndex(MessageIndex messageIndex)
    {
        if (_logger.IsDebug)
            _logger.Debug($"nitroexecution_resultAtMessageIndex called: messageIndex={messageIndex}");
        return _engine.ResultAtMessageIndexAsync(messageIndex);
    }

    public async Task<ResultWrapper<MessageIndex>> nitroexecution_headMessageIndex()
    {
        if (_logger.IsDebug)
            _logger.Debug("nitroexecution_headMessageIndex called");
        ResultWrapper<ulong> result = await _engine.HeadMessageIndexAsync();
        return ResultWrapper<MessageIndex>.From(result, (MessageIndex)result.Data);
    }

    public Task<ResultWrapper<long>> nitroexecution_messageIndexToBlockNumber(MessageIndex messageIndex)
    {
        if (_logger.IsDebug)
            _logger.Debug($"nitroexecution_messageIndexToBlockNumber called: messageIndex={messageIndex}");
        return Task.FromResult(_engine.MessageIndexToBlockNumber(messageIndex));
    }

    public Task<ResultWrapper<MessageIndex>> nitroexecution_blockNumberToMessageIndex(ulong blockNumber)
    {
        if (_logger.IsDebug)
            _logger.Debug($"nitroexecution_blockNumberToMessageIndex called: blockNumber={blockNumber}");
        ResultWrapper<ulong> result = _engine.BlockNumberToMessageIndex(blockNumber);
        return Task.FromResult(ResultWrapper<MessageIndex>.From(result, (MessageIndex)result.Data));
    }

    public ResultWrapper<EmptyResponse> nitroexecution_setFinalityData(
        RpcFinalityData? safeFinalityData,
        RpcFinalityData? finalizedFinalityData,
        RpcFinalityData? validatedFinalityData)
    {
        if (_logger.IsDebug)
            _logger.Debug($"nitroexecution_setFinalityData called: safe={safeFinalityData is not null}, finalized={finalizedFinalityData is not null}, validated={validatedFinalityData is not null}");
        SetFinalityDataParams parameters = new()
        {
            SafeFinalityData = safeFinalityData,
            FinalizedFinalityData = finalizedFinalityData,
            ValidatedFinalityData = validatedFinalityData
        };
        return _engine.SetFinalityData(parameters);
    }

    public ResultWrapper<EmptyResponse> nitroexecution_setConsensusSyncData(SetConsensusSyncDataParams syncData)
    {
        if (_logger.IsDebug)
            _logger.Debug($"nitroexecution_setConsensusSyncData called");
        return _engine.SetConsensusSyncData(syncData);
    }

    public ResultWrapper<EmptyResponse> nitroexecution_markFeedStart(MessageIndex to)
    {
        if (_logger.IsDebug)
            _logger.Debug($"nitroexecution_markFeedStart called: to={to}");
        return _engine.MarkFeedStart(to);
    }

    public Task<ResultWrapper<string>> nitroexecution_triggerMaintenance()
    {
        if (_logger.IsDebug)
            _logger.Debug("nitroexecution_triggerMaintenance called");
        return _engine.TriggerMaintenanceAsync();
    }

    public Task<ResultWrapper<bool>> nitroexecution_shouldTriggerMaintenance()
    {
        if (_logger.IsDebug)
            _logger.Debug("nitroexecution_shouldTriggerMaintenance called");
        return _engine.ShouldTriggerMaintenanceAsync();
    }

    public Task<ResultWrapper<MaintenanceStatus>> nitroexecution_maintenanceStatus()
    {
        if (_logger.IsDebug)
            _logger.Debug("nitroexecution_maintenanceStatus called");
        return _engine.MaintenanceStatusAsync();
    }
}
