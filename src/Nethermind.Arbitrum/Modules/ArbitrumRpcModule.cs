// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nethermind.Arbitrum.Modules
{
    public class ArbitrumRpcModule : IArbitrumRpcModule
    {
        IArbitrumConfig _arbitrumConfig;

        public ArbitrumRpcModule(IArbitrumConfig arbitrumConfig)
        {
            _arbitrumConfig = arbitrumConfig;
        }

        public Task<ResultWrapper<MessageResult>> ResultAtPos(ulong messageIndex)
        {
            return ResultWrapper<MessageResult>.Success(new MessageResult
            {
                BlockHash = Keccak.EmptyTreeHash,
                SendRoot = Keccak.Zero
            });
        }

        public Task<ResultWrapper<ulong>> HeadMessageNumber()
        {
            return ResultWrapper<ulong>.Success(200);
        }

        public Task<ResultWrapper<ulong>> MessageIndexToBlockNumber(ulong messageIndex)
        {
            return ResultWrapper<ulong>.Success(_arbitrumConfig.GenesisBlockNum + messageIndex);
        }
    }
}
