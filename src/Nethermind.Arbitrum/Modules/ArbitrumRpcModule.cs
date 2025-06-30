// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
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
        ILogManager logManager)
        : IArbitrumRpcModule
    {
        private readonly ILogger _logger = logManager.GetClassLogger<ArbitrumRpcModule>();
        // TODO: implement configuration for ArbitrumRpcModule
        private readonly ArbitrumSyncMonitor _syncMonitor = new(blockTree, specHelper, new ArbitrumSyncMonitorConfig(), logManager);

        public ResultWrapper<MessageResult> DigestInitMessage(DigestInitMessage message)
        {
            if (message.InitialL1BaseFee.IsZero)
            {
                return ResultWrapper<MessageResult>.Fail("InitialL1BaseFee must be greater than zero", ErrorCodes.InvalidParams);
            }

            if (message.SerializedChainConfig is null || message.SerializedChainConfig.Length == 0)
            {
                return ResultWrapper<MessageResult>.Fail("SerializedChainConfig must not be empty.", ErrorCodes.InvalidParams);
            }

            if (!TryDeserializeChainConfig(message.SerializedChainConfig, out ChainConfig? chainConfig))
            {
                return ResultWrapper<MessageResult>.Fail("Failed to deserialize ChainConfig.", ErrorCodes.InvalidParams);
            }

            ParsedInitMessage initMessage = new(chainSpec.ChainId, message.InitialL1BaseFee, chainConfig, message.SerializedChainConfig);
            Block genesisBlock = initializer.Initialize(initMessage);

            return ResultWrapper<MessageResult>.Success(new()
            {
                BlockHash = genesisBlock.Hash ?? throw new InvalidOperationException("Genesis block hash must not be null"),
                SendRoot = Hash256.Zero
            });
        }

        public async Task<ResultWrapper<MessageResult>> DigestMessage(DigestMessageParameters parameters)
        {
            _ = txSource; // TODO: replace with the actual use
            var payload = new ArbitrumPayloadAttributes()
            {
                MessageWithMetadata = parameters.Message
            };

            var block = await trigger.BuildBlock(payloadAttributes: payload);
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

                var blockHeader = blockTree.FindHeader(blockNumberResult.Data, BlockTreeLookupOptions.None);
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

        public ResultWrapper<string> SetFinalityData(SetFinalityDataParams? parameters)
        {
            if (parameters is null)
                return ResultWrapper<string>.Fail(ArbitrumRpcErrors.FormatNullParameters(), ErrorCodes.InvalidParams);

            try
            {
                if (_logger.IsDebug)
                {
                    _logger.Debug($"SetFinalityData called: safe={parameters.SafeFinalityData?.MsgIdx}, " +
                                 $"finalized={parameters.FinalizedFinalityData?.MsgIdx}, " +
                                 $"validated={parameters.ValidatedFinalityData?.MsgIdx}");
                }

                // Convert RPC parameters to internal types
                var safeFinalityData = parameters.SafeFinalityData?.ToArbitrumFinalityData();
                var finalizedFinalityData = parameters.FinalizedFinalityData?.ToArbitrumFinalityData();
                var validatedFinalityData = parameters.ValidatedFinalityData?.ToArbitrumFinalityData();

                // Set finality data
                _syncMonitor.SetFinalityData(safeFinalityData, finalizedFinalityData, validatedFinalityData);

                if (_logger.IsDebug)
                    _logger.Debug("SetFinalityData completed successfully");

                return ResultWrapper<string>.Success("OK");
            }
            catch (Exception ex)
            {
                if (_logger.IsError)
                    _logger.Error($"SetFinalityData failed: {ex.Message}", ex);

                return ResultWrapper<string>.Fail(ArbitrumRpcErrors.InternalError);
            }
        }


        private bool TryDeserializeChainConfig(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out ChainConfig? chainConfig)
        {
            try
            {
                chainConfig = JsonSerializer.Deserialize<ChainConfig>(bytes);
                return chainConfig != null;
            }
            catch (Exception exception)
            {
                _logger.Error("Failed to deserialize ChainConfig from bytes.", exception);
                chainConfig = null;
                return false;
            }
        }
    }
}
