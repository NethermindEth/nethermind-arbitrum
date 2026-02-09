// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// RPC module for the "nitroexecution" namespace. Thin facade that delegates to IArbitrumExecutionEngine.
/// </summary>
public class NitroExecutionRpcModule(IArbitrumExecutionEngine engine) : INitroExecutionRpcModule
{
    public Task<ResultWrapper<MessageResult>> nitroexecution_digestMessage(
        MessageIndex msgIdx,
        MessageWithMetadata message,
        MessageWithMetadata? messageForPrefetch)
    {
        DigestMessageParameters parameters = new(msgIdx, message, messageForPrefetch);
        return engine.DigestMessageAsync(parameters);
    }

    public Task<ResultWrapper<MessageResult[]>> nitroexecution_reorg(
        MessageIndex msgIdxOfFirstMsgToAdd,
        MessageWithMetadataAndBlockInfo[] newMessages,
        MessageWithMetadata[] oldMessages)
    {
        ReorgParameters parameters = new(msgIdxOfFirstMsgToAdd, newMessages, oldMessages);
        return engine.ReorgAsync(parameters);
    }

    public Task<ResultWrapper<MessageResult>> nitroexecution_resultAtMessageIndex(MessageIndex messageIndex)
        => engine.ResultAtMessageIndexAsync(messageIndex);

    public async Task<ResultWrapper<MessageIndex>> nitroexecution_headMessageIndex()
    {
        ResultWrapper<ulong> result = await engine.HeadMessageIndexAsync();
        return ResultWrapper<MessageIndex>.From(result, (MessageIndex)result.Data);
    }

    public Task<ResultWrapper<long>> nitroexecution_messageIndexToBlockNumber(MessageIndex messageIndex)
        => Task.FromResult(engine.MessageIndexToBlockNumber(messageIndex));

    public Task<ResultWrapper<MessageIndex>> nitroexecution_blockNumberToMessageIndex(ulong blockNumber)
    {
        ResultWrapper<ulong> result = engine.BlockNumberToMessageIndex(blockNumber);
        return Task.FromResult(ResultWrapper<MessageIndex>.From(result, (MessageIndex)result.Data));
    }

    public ResultWrapper<EmptyResponse> nitroexecution_setFinalityData(
        RpcFinalityData? safeFinalityData,
        RpcFinalityData? finalizedFinalityData,
        RpcFinalityData? validatedFinalityData)
    {
        SetFinalityDataParams parameters = new()
        {
            SafeFinalityData = safeFinalityData,
            FinalizedFinalityData = finalizedFinalityData,
            ValidatedFinalityData = validatedFinalityData
        };
        return engine.SetFinalityData(parameters);
    }

    public ResultWrapper<EmptyResponse> nitroexecution_setConsensusSyncData(SetConsensusSyncDataParams syncData)
        => engine.SetConsensusSyncData(syncData);

    public ResultWrapper<EmptyResponse> nitroexecution_markFeedStart(MessageIndex to)
        => engine.MarkFeedStart(to);

    public Task<ResultWrapper<string>> nitroexecution_triggerMaintenance()
        => engine.TriggerMaintenanceAsync();

    public Task<ResultWrapper<bool>> nitroexecution_shouldTriggerMaintenance()
        => engine.ShouldTriggerMaintenanceAsync();

    public Task<ResultWrapper<MaintenanceStatus>> nitroexecution_maintenanceStatus()
        => engine.MaintenanceStatusAsync();

    public Task<ResultWrapper<StartSequencingResult>> nitroexecution_startSequencing()
        => engine.StartSequencingAsync();

    public ResultWrapper<EmptyResponse> nitroexecution_endSequencing(string? error)
        => engine.EndSequencing(error);

    public ResultWrapper<EmptyResponse> nitroexecution_enqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx)
        => engine.EnqueueDelayedMessages(messages, firstMsgIdx);

    public Task<ResultWrapper<EmptyResponse>> nitroexecution_appendLastSequencedBlock()
        => engine.AppendLastSequencedBlockAsync();

    public ResultWrapper<ulong> nitroexecution_nextDelayedMessageNumber()
        => engine.NextDelayedMessageNumber();

    public Task<ResultWrapper<SequencedMsg?>> nitroexecution_resequenceReorgedMessage(MessageWithMetadata? message)
        => engine.ResequenceReorgedMessageAsync(message);

    public ResultWrapper<EmptyResponse> nitroexecution_pause()
        => engine.Pause();

    public ResultWrapper<EmptyResponse> nitroexecution_activate()
        => engine.Activate();

    public ResultWrapper<EmptyResponse> nitroexecution_forwardTo(string url)
        => engine.ForwardTo(url);
}
