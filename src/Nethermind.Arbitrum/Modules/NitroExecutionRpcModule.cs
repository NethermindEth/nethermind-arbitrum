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
        ulong msgIdx,
        MessageWithMetadata message,
        MessageWithMetadata? messageForPrefetch)
    {
        DigestMessageParameters parameters = new(msgIdx, message, messageForPrefetch);
        return engine.DigestMessageAsync(parameters);
    }

    public Task<ResultWrapper<MessageResult[]>> nitroexecution_reorg(
        ulong msgIdxOfFirstMsgToAdd,
        MessageWithMetadataAndBlockInfo[] newMessages,
        MessageWithMetadata[] oldMessages)
    {
        ReorgParameters parameters = new(msgIdxOfFirstMsgToAdd, newMessages, oldMessages);
        return engine.ReorgAsync(parameters);
    }

    public Task<ResultWrapper<MessageResult>> nitroexecution_resultAtMessageIndex(ulong messageIndex)
        => engine.ResultAtMessageIndexAsync(messageIndex);

    public Task<ResultWrapper<ulong>> nitroexecution_headMessageIndex()
        => engine.HeadMessageIndexAsync();

    public Task<ResultWrapper<long>> nitroexecution_messageIndexToBlockNumber(ulong messageIndex)
        => Task.FromResult(engine.MessageIndexToBlockNumber(messageIndex));

    public Task<ResultWrapper<ulong>> nitroexecution_blockNumberToMessageIndex(ulong blockNumber)
        => Task.FromResult(engine.BlockNumberToMessageIndex(blockNumber));

    public ResultWrapper<string> nitroexecution_setFinalityData(
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

    public ResultWrapper<string> nitroexecution_setConsensusSyncData(SetConsensusSyncDataParams syncData)
        => engine.SetConsensusSyncData(syncData);

    public ResultWrapper<string> nitroexecution_markFeedStart(ulong to)
        => engine.MarkFeedStart(to);

    public Task<ResultWrapper<string>> nitroexecution_triggerMaintenance()
        => engine.TriggerMaintenanceAsync();

    public Task<ResultWrapper<bool>> nitroexecution_shouldTriggerMaintenance()
        => engine.ShouldTriggerMaintenanceAsync();

    public Task<ResultWrapper<MaintenanceStatus>> nitroexecution_maintenanceStatus()
        => engine.MaintenanceStatusAsync();
}
