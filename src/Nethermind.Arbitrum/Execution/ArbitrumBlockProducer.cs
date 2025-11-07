// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

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
using Nethermind.Evm.Tracing;
using Nethermind.Logging;
using Nethermind.Merge.Plugin.BlockProduction;

namespace Nethermind.Arbitrum.Execution
{
    public class ArbitrumBlockProducer : PostMergeBlockProducer
    {
        private IWorldState _worldState;
        private readonly IBlockCachePreWarmer? _blockCachePreWarmer;
        private readonly EthereumEcdsa _ecdsa;
        private readonly RecoverSignatures _recoverSignatures;

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
            IBlockCachePreWarmer? blockCachePreWarmer) : base(
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
            _blockCachePreWarmer = blockCachePreWarmer;
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
                parent.GasLimit,
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

        public Task PreWarmBlock(BlockHeader? parentHeader = null, IBlockTracer? blockTracer = null,
            PayloadAttributes? payloadAttributes = null, IBlockProducer.Flags flags = IBlockProducer.Flags.None, CancellationToken token = default)
        {
            try
            {
                if (_blockCachePreWarmer is not null)
                {
                    parentHeader ??= BlockTree.Head?.Header;
                    if (parentHeader is null)
                    {
                        if (Logger.IsDebug)
                            Logger.Debug("Cannot pre-warm block caches, no parent header");
                        return Task.CompletedTask;
                    }
                    Block preWarmBlock = PrepareBlock(parentHeader, payloadAttributes, flags);

                    _recoverSignatures.RecoverData(preWarmBlock);

                    return _blockCachePreWarmer.PreWarmCaches(preWarmBlock, parentHeader, _specProvider.GetSpec(preWarmBlock.Header), token);
                }
                return Task.CompletedTask;
            }
            catch (Exception e) when (e is not TaskCanceledException)
            {
                if (Logger.IsError)
                    Logger.Error("Failed to pre-warm block", e);
                throw;
            }
        }

        public void ClearPreWarmCaches()
        {
            _blockCachePreWarmer?.ClearCaches();
        }
    }
}
