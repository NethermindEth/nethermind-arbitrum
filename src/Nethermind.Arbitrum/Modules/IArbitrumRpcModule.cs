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
        Task<ResultWrapper<MessageResult>> DigestMessage();

        Task<ResultWrapper<MessageResult>> ResultAtPos(UInt64 messageIndex);

        Task<ResultWrapper<ulong>> HeadMessageNumber();

        Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex);

        Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber);
    }
}
