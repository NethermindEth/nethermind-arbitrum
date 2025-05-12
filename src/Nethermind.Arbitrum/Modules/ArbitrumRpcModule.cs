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
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Consensus.Producers;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;
using Nethermind.Blockchain;

namespace Nethermind.Arbitrum.Modules
{
    public class ArbitrumRpcModule(
        IBlockTree blockTree,
        IManualBlockProductionTrigger trigger,
        ArbitrumRpcTxSource txSource,
        ChainSpec chainSpec,
        IArbitrumConfig arbitrumConfig,
        ILogger logger) : IArbitrumRpcModule
    {
        public async Task<ResultWrapper<MessageResult>> DigestMessage()
        {
            // TODO: Parse inputs here and pass to TxSource

            var block = await trigger.BuildBlock();
            return block is null
                ? ResultWrapper<MessageResult>.Fail("Failed to build block", ErrorCodes.InternalError)
                : ResultWrapper<MessageResult>.Success(new()
                {
                    BlockHash = block.Hash ?? Hash256.Zero,
                    SendRoot = Hash256.Zero
                });
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
            Core.BlockHeader? header = blockTree.FindLatestHeader();

            return header is null
                ? ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError)
                : ResultWrapper<ulong>.Success(BlockNumberToMessageIndex((ulong)header.Number).Result.Data);
        }

        public Task<ResultWrapper<ulong>> MessageIndexToBlockNumber(ulong messageIndex)
        {
            return ResultWrapper<ulong>.Success(GetGenesisBlockNumber() + messageIndex);
        }

        public Task<ResultWrapper<ulong>> BlockNumberToMessageIndex(ulong blockNumber)
        {
            ulong genesis = GetGenesisBlockNumber();

            if (blockNumber < genesis)
            {
                return ResultWrapper<ulong>.Fail($"blockNumber {blockNumber} < genesis {genesis}");
            }

            return ResultWrapper<ulong>.Success(blockNumber - genesis);
        }

        private ulong GetGenesisBlockNumber()
        {
            return arbitrumConfig.GenesisBlockNum;
        }
    }
}
