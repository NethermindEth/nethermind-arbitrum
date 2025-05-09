// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Google.Protobuf.WellKnownTypes;
using Nethermind.Arbitrum.Data;
using Nethermind.JsonRpc;
using Nethermind.JsonRpc.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethermind.Arbitrum.Modules
{
    [RpcModule(ModuleType.Arbitrum)]
    public interface IArbitrumRpcModule : IRpcModule
    {
        Task<ResultWrapper<MessageResult>> ResultAtPos(UInt64 messageIndex);
        Task<ResultWrapper<UInt64>> HeadMessageNumber();
        Task<ResultWrapper<UInt64>> MessageIndexToBlockNumber(UInt64 messageIndex);
    }
}
