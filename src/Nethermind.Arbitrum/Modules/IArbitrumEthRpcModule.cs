// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Data;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using Nethermind.JsonRpc.Modules.Eth;

namespace Nethermind.Arbitrum.Modules;

[RpcModule(ModuleType.Eth)]
public interface IArbitrumEthRpcModule : IEthRpcModule
{
    [JsonRpcMethod(IsSharable = false, IsImplemented = true)]
    Task<ResultWrapper<Hash256>> eth_sendRawTransactionConditional(byte[] transaction, ConditionalOptions options);
}
