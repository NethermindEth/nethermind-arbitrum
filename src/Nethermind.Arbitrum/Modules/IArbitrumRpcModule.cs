// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum.Modules
{
    [RpcModule("Arbitrum")]
    public interface IArbitrumRpcModule : IRpcModule
    {
        public Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber);
        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        public Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        public ResultWrapper<Dictionary<string, object>> FullSyncProgressMap();

        public Task<ResultWrapper<ulong>> HeadMessageIndex();

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        public ResultWrapper<string> MarkFeedStart(ulong to);

        public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex);

        public Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(UInt64 messageIndex);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        public ResultWrapper<string> SetConsensusSyncData(SetConsensusSyncDataParams? parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        public ResultWrapper<string> SetFinalityData(SetFinalityDataParams parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        public ResultWrapper<bool> Synced();
    }
}
