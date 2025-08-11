// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;

namespace Nethermind.Arbitrum.Modules
{
    [RpcModule(ModuleType.Arbitrum)]
    public interface IArbitrumRpcModule : IRpcModule
    {
        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<MessageResult[]>> Reorg(ReorgParameters parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        Task<ResultWrapper<MessageResult>> SequenceDelayedMessage(SequenceDelayedMessageParameters parameters);

        Task<ResultWrapper<MessageResult>> ResultAtPos(UInt64 messageIndex);

        Task<ResultWrapper<ulong>> HeadMessageNumber();

        Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex);

        Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        ResultWrapper<string> SetFinalityData(SetFinalityDataParams parameters);

        [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
        void MarkFeedStart(ulong to);
    }
}
