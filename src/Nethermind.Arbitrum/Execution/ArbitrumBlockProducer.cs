// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Blockchain;
using Nethermind.Config;
using Nethermind.Consensus;
using Nethermind.Consensus.Processing;
using Nethermind.Consensus.Producers;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Crypto;
using Nethermind.Evm.State;
using Nethermind.Evm.Tracing;
using Nethermind.Logging;
using Nethermind.Merge.Plugin.BlockProduction;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumBlockProducer : PostMergeBlockProducer
    {
        private IWorldState _worldState;

        public ArbitrumBlockProducer(
            ITxSource payloadAttrsTxSource,
            IBlockchainProcessor processor,
            IBlockTree blockTree,
            IWorldState worldState,
            IGasLimitCalculator gasLimitCalculator,
            ISealEngine sealEngine,
            ITimestamper timestamper,
            ISpecProvider specProvider,
            ILogManager logManager,
            IBlocksConfig? miningConfig) : base(
            payloadAttrsTxSource,
            processor,
            blockTree,
            worldState,
            gasLimitCalculator,
            sealEngine,
            timestamper,
            specProvider,
            logManager,
            miningConfig)
        {
            _worldState = worldState;
        }

        protected BlockHeader PrepareBlockHeader(BlockHeader parent, ArbitrumPayloadAttributes payloadAttributes, ArbosState arbosState)
        {
            long newBlockNumber = parent.Number + 1;
            if (payloadAttributes.Number != newBlockNumber)
                throw new ArgumentException($"Wrong message number in digest, got {payloadAttributes.Number}, expected {newBlockNumber}");

            ulong timestamp = payloadAttributes?.MessageWithMetadata.Message.Header.Timestamp ?? UInt64.MinValue;
            if (timestamp < parent.Timestamp)
                timestamp = parent.Timestamp;

            Address blockAuthor = payloadAttributes?.MessageWithMetadata.Message.Header.Sender;

            BlockHeader header = new(
                parent.Hash!,
                Keccak.OfAnEmptySequenceRlp,
                blockAuthor,
                1,
                newBlockNumber,
                parent.GasLimit,
                timestamp,
                parent.ExtraData)
            {
                MixHash = parent.MixHash
            };

            header.TotalDifficulty = parent.TotalDifficulty + 1;
            header.BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get();
            header.Nonce = payloadAttributes.MessageWithMetadata.DelayedMessagesRead;

            return header;
        }

        protected override BlockToProduce PrepareBlock(BlockHeader parent, PayloadAttributes? payloadAttributes = null, IBlockProducer.Flags flags = IBlockProducer.Flags.None)
        {
            Logger.Info($"[PREPARE_BLOCK] Called for parent block {parent.Number}, TxSource type: {TxSource.GetType().Name}, flags: {flags}");
            
            if (payloadAttributes is not ArbitrumPayloadAttributes)
                throw new ArgumentException("Invalid payload attributes");

            ArbitrumPayloadAttributes arbitrumPayload = (ArbitrumPayloadAttributes)payloadAttributes;

            var burner = new SystemBurner();

            using IDisposable worldStateDisposer = _worldState.BeginScope(parent);

            ArbosState arbosState =
                ArbosState.OpenArbosState(_worldState, burner, Logger);

            BlockHeader header = PrepareBlockHeader(parent, arbitrumPayload, arbosState);

            Logger.Info($"[PREPARE_BLOCK] Calling TxSource.GetTransactions for block {header.Number}");
            IEnumerable<Transaction> transactions = TxSource.GetTransactions(parent, header.GasLimit, payloadAttributes, filterSource: true);
            Logger.Info($"[PREPARE_BLOCK] GetTransactions returned {transactions.Count()} transactions");

            var startTxn =
                CreateInternalTransaction(arbitrumPayload.MessageWithMetadata.Message.Header, header, parent, _specProvider);

            //use ToArray to also set Transactions on Block base class, this allows e.g. recovery step to successfully recover sender address
            var allTransactions = transactions.Prepend(startTxn).ToArray();

            foreach (var transaction in allTransactions)
            {
                transaction.Hash = transaction.CalculateHash();
            }

            BlockToProduce blockToProduce = new(header, allTransactions, Array.Empty<BlockHeader>(),
                payloadAttributes?.Withdrawals);
            
            Logger.Info($"[PREPARE_BLOCK] Returning BlockToProduce with {blockToProduce.Transactions.Count()} transactions, header.GasUsed={header.GasUsed}");
            
            return blockToProduce;
        }
        
        protected override Block? ProcessPreparedBlock(Block block, IBlockTracer? blockTracer, CancellationToken token = default)
        {
            Logger.Info($"[PROCESS_BLOCK] ProcessPreparedBlock called for block {block.Number} with {block.Transactions.Length} transactions");
            
            Block? processedBlock = base.ProcessPreparedBlock(block, blockTracer, token);
            
            if (processedBlock is null)
            {
                Logger.Error($"[PROCESS_BLOCK] ProcessPreparedBlock returned null for block {block.Number}!");
            }
            else
            {
                Logger.Info($"[PROCESS_BLOCK] ProcessPreparedBlock completed for block {block.Number}, GasUsed={processedBlock.GasUsed}, Hash={processedBlock.Hash}");
            }
            
            return processedBlock;
        }

        public static ArbitrumInternalTransaction CreateInternalTransaction(
            L1IncomingMessageHeader l1Header, BlockHeader newHeader, BlockHeader parent, ISpecProvider specProvider
        )
        {
            var timePassed = newHeader.Timestamp - parent.Timestamp;
            var binaryData = AbiMetadata.PackInput(AbiMetadata.StartBlockMethod, l1Header.BaseFeeL1, l1Header.BlockNumber, newHeader.Number, timePassed);

            return new ArbitrumInternalTransaction
            {
                ChainId = specProvider.ChainId,
                Data = binaryData,
                SenderAddress = ArbosAddresses.ArbosAddress,
                To = ArbosAddresses.ArbosAddress
            };
        }
    }
}
