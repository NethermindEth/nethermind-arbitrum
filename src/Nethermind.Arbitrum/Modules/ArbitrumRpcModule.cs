// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Genesis;
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
        ArbitrumBlockTreeInitializer initializer,
        IBlockTree blockTree,
        IManualBlockProductionTrigger trigger,
        ArbitrumRpcTxSource txSource,
        ChainSpec chainSpec,
        IArbitrumSpecHelper specHelper,
        ILogger logger)
        : IArbitrumRpcModule
    {
        public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
        {
            if (message.InitialL1BaseFee.IsZero)
            {
                return ResultWrapper<MessageResult>.Fail("InitialL1BaseFee must be greater than zero", ErrorCodes.InvalidParams);
            }

            ResultWrapper<byte[]> serializedChainConfigResult = DecodeSerializedChainConfig(message.SerializedChainConfig);
            if (serializedChainConfigResult.Result != Result.Success)
            {
                return ResultWrapper<MessageResult>.Fail(serializedChainConfigResult.Result.Error ?? "Invalid SerializedChainConfig", ErrorCodes.InvalidParams);
            }

            ParsedInitMessage initMessage = new(
                chainSpec.ChainId,
                message.InitialL1BaseFee,
                null,
                serializedChainConfigResult.Data);

            Block genesisBlock = initializer.Initialize(initMessage);

            return ResultWrapper<MessageResult>.Success(new()
            {
                BlockHash = genesisBlock.Hash ?? throw new InvalidOperationException("Genesis block hash must not be null"),
                SendRoot = Hash256.Zero
            });
        }

        public async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
        {
            var transactions = NitroL2MessageParser.ParseTransactions(parameters.Message.Message, chainSpec.ChainId, logger);

            logger.Info($"DigestMessage successfully parsed {transactions.Count} transaction(s)");

            txSource.InjectTransactions(transactions);

            var block = await trigger.BuildBlock();
            if (logger.IsTrace) logger.Trace($"Built block: hash={block?.Hash}");
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

                var blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.None);
                if (blockHeader == null)
                {
                    return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.BlockNotFound);
                }

                if (logger.IsTrace) logger.Trace($"Found block header for block {blockNumberResult.Data}: hash={blockHeader.Hash}");

                var headerInfo = ArbitrumBlockHeaderInfo.Deserialize(blockHeader, logger);
                return ResultWrapper<MessageResult>.Success(new MessageResult
                {
                    BlockHash = blockHeader.Hash ?? Hash256.Zero,
                    SendRoot = headerInfo.SendRoot,
                });
            }
            catch (Exception ex)
            {
                if (logger.IsError) logger.Error($"Error processing ResultAtPos for message index {messageIndex}: {ex.Message}", ex);
                return ResultWrapper<MessageResult>.Fail(ArbitrumRpcErrors.InternalError);
            }
        }
        public Task<ResultWrapper<ulong>> HeadMessageNumber()
        {
            BlockHeader? header = blockTree.FindLatestHeader();

            return header is null
                ? ResultWrapper<ulong>.Fail("Failed to get latest header", ErrorCodes.InternalError)
                : BlockNumberToMessageIndex((ulong)header.Number);
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
            return specHelper.GenesisBlockNum;
        }

        private ResultWrapper<byte[]> DecodeSerializedChainConfig(string? serializedChainConfig)
        {
            if (serializedChainConfig is null || serializedChainConfig.Length == 0)
            {
                return ResultWrapper<byte[]>.Fail("SerializedChainConfig must not be empty.", ErrorCodes.InvalidParams);
            }

            // Calculates the maximum possible decoded byte length from a Base64 string based on encoding rules (check base64 wiki)
            int bufferLength = (serializedChainConfig.Length * 3 + 3) / 4;

            byte[]? rentedBuffer = null;
            Span<byte> span = rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferLength);
            try
            {
                if (!Convert.TryFromBase64String(serializedChainConfig, span, out var bytesWritten))
                {
                    return ResultWrapper<byte[]>.Fail("SerializedChainConfig is not a valid Base64 string.", ErrorCodes.InvalidParams);
                }

                // Trim to the actual written portion
                span = span[..bytesWritten];

                return ResultWrapper<byte[]>.Success(span.ToArray());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }
}
