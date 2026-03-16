// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Abi;
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
using Nethermind.Int256;
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

        private BlockHeader PrepareBlockHeader(BlockHeader parent, ArbitrumPayloadAttributes payloadAttributes, ArbosState arbosState, IReleaseSpec spec)
        {
            long newBlockNumber = parent.Number + 1;
            if (payloadAttributes.Number != newBlockNumber)
                throw new ArgumentException($"Wrong message number in digest, got {payloadAttributes.Number}, expected {newBlockNumber}");

            if (payloadAttributes.MessageWithMetadata == null)
                throw new ArgumentException("MessageWithMetadata is null");

            // Block debug logging
            var blockLog = BlockDebugLogger.GetOrCreate(_worldState, spec);
            blockLog.LogStepWithValue("block_start", "parentNumber", parent.Number);
            blockLog.LogValue("parentHash", parent.Hash);
            blockLog.LogValue("parentStateRoot", parent.StateRoot);

            ulong timestamp = payloadAttributes?.MessageWithMetadata.Message.Header.Timestamp ?? UInt64.MinValue;
            if (timestamp < parent.Timestamp)
                timestamp = parent.Timestamp;

            Address blockAuthor = payloadAttributes?.MessageWithMetadata.Message.Header.Sender ?? throw new InvalidOperationException();

            // Log before CommitMultiGasFees
            UInt256 baseFeeBeforeCommit = arbosState.L2PricingState.BaseFeeWeiStorage.Get();
            blockLog.LogStepWithValue("before_commit_multigas", "baseFee", baseFeeBeforeCommit);

            // Commit multi-gas fees before getting base fee (matches Nitro's block_processor.go)
            arbosState.L2PricingState.CommitMultiGasFees();

            UInt256 baseFeeAfterCommit = arbosState.L2PricingState.BaseFeeWeiStorage.Get();
            blockLog.LogStepWithValue("after_commit_multigas", "baseFee", baseFeeAfterCommit);

            BlockHeader header = new(
                parent.Hash!,
                Keccak.OfAnEmptySequenceRlp,
                blockAuthor,
                1,
                newBlockNumber,
                parent.GasLimit, // TODO: https://github.com/NethermindEth/nethermind-arbitrum/issues/369
                timestamp,
                parent.ExtraData)
            {
                MixHash = parent.MixHash,
                TotalDifficulty = parent.TotalDifficulty + 1,
                BaseFeePerGas = baseFeeAfterCommit,
                Nonce = payloadAttributes.MessageWithMetadata.DelayedMessagesRead
            };

            blockLog.LogStepWithValue("header_created", "number", header.Number);
            blockLog.LogValue("timestamp", header.Timestamp);
            blockLog.LogValue("baseFee", header.BaseFeePerGas);
            blockLog.LogValue("gasLimit", header.GasLimit);

            return header;
        }

        protected override BlockToProduce PrepareBlock(BlockHeader parent, PayloadAttributes? payloadAttributes = null, IBlockProducer.Flags flags = IBlockProducer.Flags.None)
        {
            if (payloadAttributes is not ArbitrumPayloadAttributes)
                throw new ArgumentException("Invalid payload attributes");

            ArbitrumPayloadAttributes arbitrumPayload = (ArbitrumPayloadAttributes)payloadAttributes;

            SystemBurner burner = new();

            using IDisposable worldStateDisposer = _worldState.BeginScope(parent);

            // Block debug logging - BEFORE OpenArbosState
            IReleaseSpec spec = _specProvider.GetSpec(parent.Number + 1, parent.Timestamp);
            var blockLog = BlockDebugLogger.GetOrCreate(_worldState, spec);
            blockLog.LogStepWithValue("before_open_arbos", "parentNumber", parent.Number);

            // Log ArbOS account state BEFORE OpenArbosState
            var arbosAccount = ArbosAddresses.ArbosSystemAccount;
            blockLog.LogValue("arbos_nonce_before", _worldState.GetNonce(arbosAccount));
            blockLog.LogValue("arbos_balance_before", _worldState.GetBalance(arbosAccount));
            blockLog.LogValue("arbos_codehash_before", _worldState.GetCodeHash(arbosAccount));

            ArbosState arbosState =
                ArbosState.OpenArbosState(_worldState, burner, Logger);

            // Log ArbOS account state AFTER OpenArbosState
            blockLog.LogValue("arbos_nonce_after", _worldState.GetNonce(arbosAccount));
            blockLog.LogStep("after_open_arbos");

            BlockHeader header = PrepareBlockHeader(parent, arbitrumPayload, arbosState, spec);

            IEnumerable<Transaction> transactions = TxSource.GetTransactions(parent, header.GasLimit, payloadAttributes, filterSource: true);

            ArbitrumInternalTransaction startTxn =
                CreateInternalTransaction(arbitrumPayload.MessageWithMetadata?.Message.Header!, header, parent, _specProvider);

            //use ToArray to also set Transactions on Block base class, this allows e.g. recovery step to successfully recover sender address
            Transaction[] allTransactions = transactions.Prepend(startTxn).ToArray();

            foreach (Transaction transaction in allTransactions)
            {
                transaction.Hash = transaction.CalculateHash();
            }

            return new BlockToProduce(header, allTransactions, [],
                payloadAttributes?.Withdrawals);
        }

        public static ArbitrumInternalTransaction CreateInternalTransaction(
            L1IncomingMessageHeader l1Header, BlockHeader newHeader, BlockHeader parent, ISpecProvider specProvider
        )
        {
            ulong timePassed = newHeader.Timestamp - parent.Timestamp;
            byte[] binaryData = AbiMetadata.PackInput(AbiMetadata.StartBlockMethod, l1Header.BaseFeeL1 ?? UInt256.Zero, l1Header.BlockNumber, newHeader.Number, timePassed);

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
