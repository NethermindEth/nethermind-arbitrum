// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Config;
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
using Nethermind.Evm.Tracing;
using Nethermind.Logging;
using Nethermind.Merge.Plugin.BlockProduction;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumBlockProducer : PostMergeBlockProducer
    {
        private IWorldState _worldState;

        private readonly ArbitrumPrefetchManager? _prefetchManager;
        private readonly EthereumEcdsa _ecdsa;
        private readonly RecoverSignatures _recoverSignatures;

        public bool CanPrefetch => _prefetchManager is not null;

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
            IBlocksConfig? miningConfig,
            IPrefetchManager? prefetchManager) : base(
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
            _prefetchManager = prefetchManager as ArbitrumPrefetchManager;
            _ecdsa = new EthereumEcdsa(_specProvider.ChainId);
            _recoverSignatures = new RecoverSignatures(_ecdsa, _specProvider, NullLogManager.Instance);
        }

        private BlockHeader PrepareBlockHeader(BlockHeader parent, ArbitrumPayloadAttributes payloadAttributes, ArbosState arbosState)
        {
            long newBlockNumber = parent.Number + 1;
            if (payloadAttributes.Number != newBlockNumber)
                throw new ArgumentException($"Wrong message number in digest, got {payloadAttributes.Number}, expected {newBlockNumber}");

            if (payloadAttributes.MessageWithMetadata == null)
                throw new ArgumentException("MessageWithMetadata is null");


            ulong timestamp = payloadAttributes?.MessageWithMetadata.Message.Header.Timestamp ?? UInt64.MinValue;
            if (timestamp < parent.Timestamp)
                timestamp = parent.Timestamp;

            Address blockAuthor = payloadAttributes?.MessageWithMetadata.Message.Header.Sender ?? throw new InvalidOperationException();

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
                BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get(),
                Nonce = payloadAttributes.MessageWithMetadata.DelayedMessagesRead
            };

            return header;
        }

        private BlockHeader PrepareBlockHeaderForPrefetch(BlockHeader parent, ArbitrumPayloadAttributes payloadAttributes, ArbosState arbosState)
        {
            //DigestMessage from block N and prefetch N+1, parent is N-1
            long newBlockNumber = parent.Number + 2;
            if (payloadAttributes.MessageWithMetadata == null)
                throw new ArgumentException("MessageWithMetadata is null");

            ulong timestamp = payloadAttributes?.MessageWithMetadata.Message.Header.Timestamp ?? UInt64.MinValue;
            if (timestamp < parent.Timestamp)
                timestamp = parent.Timestamp;

            Address blockAuthor = payloadAttributes?.MessageWithMetadata.Message.Header.Sender ?? throw new InvalidOperationException();

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
                TotalDifficulty = parent.TotalDifficulty + 2,
                BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get(),
                Nonce = payloadAttributes.MessageWithMetadata.DelayedMessagesRead
            };

            return header;
        }

        protected override BlockToProduce PrepareBlock(BlockHeader parent, PayloadAttributes? payloadAttributes = null, IBlockProducer.Flags flags = IBlockProducer.Flags.None)
        {
            if (payloadAttributes is not ArbitrumPayloadAttributes)
                throw new ArgumentException("Invalid payload attributes");

            ArbitrumPayloadAttributes arbitrumPayload = (ArbitrumPayloadAttributes)payloadAttributes;

            SystemBurner burner = new();

            using IDisposable worldStateDisposer = _worldState.BeginScope(parent);

            ArbosState arbosState =
                ArbosState.OpenArbosState(_worldState, burner, Logger);

            BlockHeader header = PrepareBlockHeader(parent, arbitrumPayload, arbosState);

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


        public bool PreWarmNextBlock(MessageWithMetadata currentMessage, MessageWithMetadata prefetchMessage, BlockHeader? parentHeader = null, IBlockProducer.Flags flags = IBlockProducer.Flags.None)
        {
            if (_prefetchManager is null)
                return false;

            try
            {
                parentHeader ??= BlockTree.Head?.Header;
                if (parentHeader is null)
                {
                    if (Logger.IsDebug)
                        Logger.Debug("Cannot pre-warm block caches, no parent header");
                    return false;
                }

                ArbitrumPayloadAttributes currentPayload = new()
                {
                    MessageWithMetadata = currentMessage,
                    Number = parentHeader.Number + 1
                };
                Transaction[] currentTransactions = TxSource.GetTransactions(parentHeader, parentHeader.GasLimit, currentPayload).ToArray();

                ArbitrumPayloadAttributes prefetchPayload = new()
                {
                    MessageWithMetadata = prefetchMessage,
                    Number = parentHeader.Number + 2
                };
                Transaction[] transactions = TxSource.GetTransactions(parentHeader, parentHeader.GasLimit, prefetchPayload).ToArray();

                //if (transactions.Length < 3)
                //    return false;

                if (transactions.Length > currentTransactions.Length * 1.75f)
                    return false;

                SystemBurner burner = new();
                using IDisposable worldStateDisposer = _worldState.BeginScope(parentHeader);
                ArbosState arbosState =
                    ArbosState.OpenArbosState(_worldState, burner, Logger);

                BlockHeader header = PrepareBlockHeaderForPrefetch(parentHeader, prefetchPayload, arbosState);
                ArbitrumInternalTransaction startTxn =
                    CreateInternalTransaction(prefetchPayload.MessageWithMetadata?.Message.Header!, header, parentHeader, _specProvider);

                //use ToArray to also set Transactions on Block base class, this allows e.g. recovery step to successfully recover sender address
                Transaction[] allTransactions = transactions.Prepend(startTxn).ToArray();

                foreach (Transaction transaction in allTransactions)
                {
                    transaction.Hash = transaction.CalculateHash();
                }

                Block preWarmBlock = new BlockToProduce(header, allTransactions, [], prefetchPayload?.Withdrawals);

                _recoverSignatures.RecoverData(preWarmBlock);
                _prefetchManager.PrefetchBlock(preWarmBlock, parentHeader, _specProvider.GetSpec(preWarmBlock.Header));
            }
            catch (Exception e) when (e is not TaskCanceledException)
            {
                if (Logger.IsError)
                    Logger.Error("Failed to pre-warm block", e);
                throw;
            }

            return true;
        }

        public void SwapCaches()
        {
            _prefetchManager?.SwapCaches();
        }

        public static ArbitrumInternalTransaction CreateInternalTransaction(
            L1IncomingMessageHeader l1Header, BlockHeader newHeader, BlockHeader parent, ISpecProvider specProvider
        )
        {
            ulong timePassed = newHeader.Timestamp - parent.Timestamp;
            byte[] binaryData = AbiMetadata.PackInput(AbiMetadata.StartBlockMethod, l1Header.BaseFeeL1, l1Header.BlockNumber, newHeader.Number, timePassed);

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
