// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Blockchain;
using Nethermind.Consensus.Producers;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.JsonRpc;
using Nethermind.Logging;
using Nethermind.Specs.ChainSpecStyle;

namespace Nethermind.Arbitrum.Modules
{
    public class ArbitrumRpcModule(
        IBlockTree _blockTree,
        IManualBlockProductionTrigger _trigger,
        ArbitrumRpcTxSource _txSource,
        ChainSpec _chainSpec,
        IArbitrumConfig _arbitrumConfig,
        ILogger _logger)
        : IArbitrumRpcModule
    {
        public async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
        {
            var transactions = NitroL2MessageParser.ParseTransactions(parameters.Message.Message, _chainSpec.ChainId, _logger);

            _logger.Info($"DigestMessage successfully parsed {transactions.Count} transaction(s)");

            _txSource.InjectTransactions(transactions);

            var block = await _trigger.BuildBlock();
            if (_logger.IsTrace) _logger.Trace($"Built block: hash={block?.Hash}");
            return block is null
                ? ResultWrapper<MessageResult>.Fail("Failed to build block", ErrorCodes.InternalError)
                : ResultWrapper<MessageResult>.Success(new()
                {
                    BlockHash = block.Hash ?? Hash256.Zero,
                    SendRoot = Hash256.Zero
                });
        }

        public async Task<ResultWrapper<MessageResult>> ResultAtPos(ulong messageIndex)
        {
            try
            {
                var blockNumberResult = await MessageIndexToBlockNumber(messageIndex);
                if (blockNumberResult.Result != Result.Success)
                {
                    return ResultWrapper<MessageResult>.Fail(blockNumberResult.Result.Error ?? "Unknown error converting message index");
                }

                var blockHeader = _blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.None);
                if (blockHeader == null)
                {
                    return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound);
                }

                if (_logger.IsTrace) _logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

                var headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, _logger);
                return ResultWrapper<MessageResult>.Success(new MessageResult
                {
                    BlockHash = blockHeader.Hash ?? Hash256.Zero,
                    SendRoot = headerInfo.SendRoot,
                });
            }
            catch (Exception ex)
            {
                if (_logger.IsError) _logger.Error($"Error processing ResultAtPos for message index {messageIndex}: {ex.Message}", ex);
                return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError);
            }
        }
        public Task<ResultWrapper<ulong>> HeadMessageNumber()
        {
            BlockHeader? header = _blockTree.FindLatestHeader();

            return header is null
                ? ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError)
                : ResultWrapper<ulong>.Success(BlockNumberToMessageIndex((ulong)header.Number).Result.Data);
        }

        public Task<ResultWrapper<long>> MessageIndexToBlockNumber(ulong messageIndex)
        {
            try
            {
                checked
                {
                    ulong blockNumber = GetGenesisBlockNumber() + messageIndex;
                    if (blockNumber > long.MaxValue)
                    {
                        return ResultWrapper<long>.Fail(ArbitrumRpcErrors.FormatExceedsLongMax(blockNumber));
                    }
                    return ResultWrapper<long>.Success((long)blockNumber);
                }
            }
            catch (OverflowException)
            {
                return ResultWrapper<long>.Fail(ArbitrumRpcErrors.Overflow);
            }
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
            return _arbitrumConfig.GenesisBlockNum;
        }
    }
}
