// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Consensus.Producers;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Modules
{
    public class ArbitrumRpcModule(
        IManualBlockProductionTrigger trigger,
        ArbitrumRpcTxSource txSource,
        ChainSpec chainSpec,
        IArbitrumConfig arbitrumConfig,
        ILogger logger) : IArbitrumRpcModule
    {
        public async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
        {
            var transactions = NitroL2MessageParser.ParseTransactions(parameters.Message.Message, chainSpec.ChainId, logger);

            logger.Info($"DigestMessage successfully parsed {transactions.Count} transaction(s)");

            txSource.InjectTransactions(transactions);

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
            return ResultWrapper<ulong>.Success(200);
        }

        public Task<ResultWrapper<ulong>> MessageIndexToBlockNumber(ulong messageIndex)
        {
            return ResultWrapper<ulong>.Success(arbitrumConfig.GenesisBlockNum + messageIndex);
        }
    }
}
