// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Execution;

/// <summary>
/// Core execution engine for Arbitrum block production and state management.
/// </summary>
public interface IArbitrumExecutionEngine
{
    Task<ResultWrapper<MessageResult>> DigestMessageAsync(DigestMessageParameters parameters);
    Task<ResultWrapper<MessageResult[]>> ReorgAsync(ReorgParameters parameters);
    Task<ResultWrapper<MessageResult>> ResultAtMessageIndexAsync(ulong messageIndex);
    Task<ResultWrapper<ulong>> HeadMessageIndexAsync();
    ResultWrapper<long> MessageIndexToBlockNumber(ulong messageIndex);
    ResultWrapper<ulong> BlockNumberToMessageIndex(ulong blockNumber);
    ResultWrapper<EmptyResponse> SetFinalityData(SetFinalityDataParams parameters);
    ResultWrapper<EmptyResponse> MarkFeedStart(ulong to);
    Task<ResultWrapper<string>> TriggerMaintenanceAsync();
    Task<ResultWrapper<bool>> ShouldTriggerMaintenanceAsync();
    Task<ResultWrapper<MaintenanceStatus>> MaintenanceStatusAsync();
    ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message);
    ResultWrapper<EmptyResponse> SetConsensusSyncData(SetConsensusSyncDataParams? parameters);
    ResultWrapper<bool> Synced();
    ResultWrapper<Dictionary<string, object>> FullSyncProgressMap();
    Task<ResultWrapper<ulong>> ArbOSVersionForMessageIndexAsync(ulong messageIndex);
}
