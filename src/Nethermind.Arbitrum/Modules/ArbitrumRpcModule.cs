// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Core;
using Nethermind.JsonRpc;

namespace Nethermind.Arbitrum.Modules;

/// <summary>
/// RPC module for the "Arbitrum" namespace. Thin facade that delegates to IArbitrumExecutionEngine.
/// Legacy namespace - converts EmptyResponse to "OK" for backwards compatibility.
/// </summary>
public class ArbitrumRpcModule(IArbitrumExecutionEngine engine) : IArbitrumRpcModule
{
    public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
        => engine.DigestInitMessage(message);

    public Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
        => engine.DigestMessageAsync(parameters);

    public Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters)
        => engine.ReorgAsync(parameters);

    public Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(ulong messageIndex)
        => engine.ResultAtMessageIndexAsync(messageIndex);

    public Task<ResultWrapper<ulong>> HeadMessageIndex()
        => engine.HeadMessageIndexAsync();

    public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
        => Task.FromResult(engine.MessageIndexToBlockNumber(messageIndex));

    public Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber)
        => Task.FromResult(engine.BlockNumberToMessageIndex(blockNumber));

    public ResultWrapper<string> SetFinalityData(SetFinalityDataParams parameters)
    {
        ResultWrapper<EmptyResponse> result = engine.SetFinalityData(parameters);
        return result.Result == Result.Success
            ? ResultWrapper<string>.Success("OK")
            : ResultWrapper<string>.Fail(result.Result.Error!, result.ErrorCode);
    }

    public ResultWrapper<string> MarkFeedStart(ulong to)
    {
        ResultWrapper<EmptyResponse> result = engine.MarkFeedStart(to);
        return result.Result == Result.Success
            ? ResultWrapper<string>.Success("OK")
            : ResultWrapper<string>.Fail(result.Result.Error!, result.ErrorCode);
    }

    public ResultWrapper<string> SetConsensusSyncData(SetConsensusSyncDataParams? parameters)
    {
        ResultWrapper<EmptyResponse> result = engine.SetConsensusSyncData(parameters);
        return result.Result == Result.Success
            ? ResultWrapper<string>.Success("OK")
            : ResultWrapper<string>.Fail(result.Result.Error!, result.ErrorCode);
    }

    public ResultWrapper<bool> Synced()
        => engine.Synced();

    public ResultWrapper<Dictionary<string, object>> FullSyncProgressMap()
        => engine.FullSyncProgressMap();

    public Task<ResultWrapper<ulong>> ArbOSVersionForMessageIndex(ulong messageIndex)
        => engine.ArbOSVersionForMessageIndexAsync(messageIndex);

    public Task<ResultWrapper<string>> TriggerMaintenance()
        => engine.TriggerMaintenanceAsync();

    public Task<ResultWrapper<bool>> ShouldTriggerMaintenance()
        => engine.ShouldTriggerMaintenanceAsync();

    public Task<ResultWrapper<MaintenanceStatus>> MaintenanceStatus()
        => engine.MaintenanceStatusAsync();
}
