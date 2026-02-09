// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// RPC module implementing Nitro's ExecutionClient interface.
/// Namespace: nitroexecution
/// </summary>
[RpcModule("nitroexecution")]
public interface INitroExecutionRpcModule : IRpcModule
{
    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MessageResult>> nitroexecution_digestMessage(
        MessageIndex msgIdx,
        MessageWithMetadata message,
        MessageWithMetadata? messageForPrefetch);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MessageResult[]>> nitroexecution_reorg(
        MessageIndex msgIdxOfFirstMsgToAdd,
        MessageWithMetadataAndBlockInfo[] newMessages,
        MessageWithMetadata[] oldMessages);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MessageResult>> nitroexecution_resultAtMessageIndex(MessageIndex messageIndex);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MessageIndex>> nitroexecution_headMessageIndex();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<long>> nitroexecution_messageIndexToBlockNumber(MessageIndex messageIndex);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MessageIndex>> nitroexecution_blockNumberToMessageIndex(ulong blockNumber);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<EmptyResponse> nitroexecution_setFinalityData(
        RpcFinalityData? safeFinalityData,
        RpcFinalityData? finalizedFinalityData,
        RpcFinalityData? validatedFinalityData);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<EmptyResponse> nitroexecution_setConsensusSyncData(SetConsensusSyncDataParams syncData);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<EmptyResponse> nitroexecution_markFeedStart(MessageIndex to);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<string>> nitroexecution_triggerMaintenance();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<bool>> nitroexecution_shouldTriggerMaintenance();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MaintenanceStatus>> nitroexecution_maintenanceStatus();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<StartSequencingResult>> nitroexecution_startSequencing();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<EmptyResponse> nitroexecution_endSequencing(string? error);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<EmptyResponse> nitroexecution_enqueueDelayedMessages(L1IncomingMessage[] messages, ulong firstMsgIdx);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<EmptyResponse>> nitroexecution_appendLastSequencedBlock();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<ulong> nitroexecution_nextDelayedMessageNumber();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<SequencedMsg?>> nitroexecution_resequenceReorgedMessage(MessageWithMetadata? message);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<EmptyResponse> nitroexecution_pause();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<EmptyResponse> nitroexecution_activate();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<EmptyResponse> nitroexecution_forwardTo(string url);
}
