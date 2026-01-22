// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum.Modules
{
    // TODO: Remove this interface after migration to INitroExecutionRpcModule is complete.
    // The new "nitroexecution" namespace should be used by Nitro consensus layer.
    // Methods to deprecate: DigestInitMessage, SetConsensusSyncData, Synced, FullSyncProgressMap
    [RpcModule("Arbitrum")]
    public interface IArbitrumRpcModule : IRpcModule
    {
        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters);

        Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(UInt64 messageIndex);

        Task<ResultWrapper<ulong>> HeadMessageIndex();

        Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex);

        Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<string> SetFinalityData(SetFinalityDataParams parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<string> MarkFeedStart(ulong to);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<string> SetConsensusSyncData(SetConsensusSyncDataParams? parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<bool> Synced();

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<Dictionary<string, object>> FullSyncProgressMap();

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<ulong>> ArbOSVersionForMessageIndex(ulong messageIndex);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<string>> TriggerMaintenance();

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<bool>> ShouldTriggerMaintenance();

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<MaintenanceStatus>> MaintenanceStatus();
    }
}
