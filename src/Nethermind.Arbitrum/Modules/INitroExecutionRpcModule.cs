// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
    // Core execution methods
    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(ulong messageIndex);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<ulong>> HeadMessageIndex();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<string> SetFinalityData(SetFinalityDataParams parameters);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    ResultWrapper<string> MarkFeedStart(ulong to);

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<string>> TriggerMaintenance();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<bool>> ShouldTriggerMaintenance();

    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<MaintenanceStatus>> MaintenanceStatus();
}
