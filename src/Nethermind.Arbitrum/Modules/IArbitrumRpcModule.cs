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
        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<BulkMessageResult>> DigestMessages(DigestMessageBulkParameters parameters);

        Task<ResultWrapper<MessageResult>> ResultAtMessageIndex(UInt64 messageIndex);

        Task<ResultWrapper<ulong>> HeadMessageIndex();

        Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex);

        Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<string> SetFinalityData(SetFinalityDataParams parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<string> MarkFeedStart(ulong to);
    }
}
