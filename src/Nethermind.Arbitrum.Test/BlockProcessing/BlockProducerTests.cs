using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.BlockProcessing
{
    [TestFixture]
    internal class BlockProducerTests
    {
        public const int DefaultTimeoutMs = 1000;

        [Test]
        public void BuildBlock_Always_StartsFromArbitrumInternalTransaction()
        {
            UInt256 l1BaseFee = 39;
            var preConfigurer = (ContainerBuilder cb) =>
            {
                cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
                {
                    SuggestGenesisOnStart = true,
                    L1BaseFee = l1BaseFee,
                    FillWithTestDataOnStart = false
                });
            };

            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

            var incomingHeader = new L1IncomingMessageHeader(ArbitrumL1MessageKind.L2Message, TestItem.AddressA, 1,
                1500, null,
                l1BaseFee);

            var payloadAttributes = new ArbitrumPayloadAttributes()
            {
                MessageWithMetadata = new MessageWithMetadata(new L1IncomingMessage(incomingHeader, null, null), 10),
                Number = 1
            };

            var blockTracer = new BlockReceiptsTracer();
            var buildBlockTask = chain.BlockProducer.BuildBlock(chain.BlockTree.Head?.Header, blockTracer, payloadAttributes);
            buildBlockTask.Wait(DefaultTimeoutMs);

            //assert
            buildBlockTask.IsCompletedSuccessfully.Should().BeTrue();

            var buildBlock = buildBlockTask.Result;
            buildBlock.Should().NotBeNull();
            buildBlock.Transactions.Length.Should().Be(1);
            var initTransaction = buildBlock.Transactions[0];
            initTransaction.Should().NotBeNull();
            initTransaction.Type.Should().Be((TxType)ArbitrumTxType.ArbitrumInternal);
            initTransaction.ChainId.Should().Be(chain.ChainSpec.ChainId);
            initTransaction.SenderAddress.Should().Be(ArbosAddresses.ArbosAddress);
            initTransaction.To.Should().Be(ArbosAddresses.ArbosAddress);

            var binaryData = AbiMetadata.PackInput(AbiMetadata.StartBlockMethod, incomingHeader.BaseFeeL1, incomingHeader.BlockNumber, chain.BlockTree.Head.Number + 1, 1500);
            initTransaction.Data.ToArray().Should().BeEquivalentTo(binaryData);
        }

        [Test]
        public void BuildBlock_SignedTransaction_RecoversSenderAddressFromSignature()
        {
            UInt256 l1BaseFee = 39;
            var preConfigurer = (ContainerBuilder cb) =>
            {
                cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
                {
                    SuggestGenesisOnStart = true,
                    L1BaseFee = l1BaseFee,
                    FillWithTestDataOnStart = true
                });
            };

            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

            ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState,
                new SystemBurner(), LimboNoErrorLogger.Instance);

            var ethereumEcdsa = new EthereumEcdsa(chain.SpecProvider.ChainId);
            Transaction transaction = Build.A.Transaction
                .WithGasLimit(GasCostOf.Transaction)
                .WithGasPrice(arbosState.L2PricingState.BaseFeeWeiStorage.Get())
                .WithNonce(0)
                .WithValue(1.Ether())
                .To(TestItem.AddressB)
                .SignedAndResolved(ethereumEcdsa, TestItem.PrivateKeyA)
                .TestObject;

            var txStream = TxDecoder.Instance.Encode(transaction);

            Span<byte> l2Msg = stackalloc byte[txStream.Length + 1];
            l2Msg[0] = (byte)ArbitrumL2MessageKind.SignedTx;
            txStream.Bytes.CopyTo(l2Msg[1..]);

            var incomingHeader = new L1IncomingMessageHeader(ArbitrumL1MessageKind.L2Message, TestItem.AddressC, 1,
                1500, null,
                l1BaseFee);

            var payloadAttributes = new ArbitrumPayloadAttributes()
            {
                MessageWithMetadata = new MessageWithMetadata(new L1IncomingMessage(incomingHeader, l2Msg.ToArray(), null), 10),
                Number = 2
            };

            var blockTracer = new BlockReceiptsTracer();
            var buildBlockTask =
                chain.BlockProducer.BuildBlock(chain.BlockTree.BestSuggestedHeader, blockTracer, payloadAttributes);

            buildBlockTask.Wait(DefaultTimeoutMs);

            //assert
            buildBlockTask.IsCompletedSuccessfully.Should().BeTrue();

            var buildBlock = buildBlockTask.Result;
            buildBlock.Should().NotBeNull();
            buildBlock.Transactions.Length.Should().Be(2);
            var receipt = blockTracer.TxReceipts[1];
            receipt.Sender.Should().BeEquivalentTo(TestItem.AddressA);
        }

        [Test]
        public void BuildBlock_WhenTimestampSmallerThanParents_SetsAsParents()
        {
            UInt256 l1BaseFee = 39;
            var preConfigurer = (ContainerBuilder cb) =>
            {
                cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
                {
                    SuggestGenesisOnStart = true,
                    L1BaseFee = l1BaseFee,
                    FillWithTestDataOnStart = false
                });
            };

            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

            var header1 = new L1IncomingMessageHeader(ArbitrumL1MessageKind.L2Message, TestItem.AddressA, 1,
                1500, null, l1BaseFee);

            var payloadAttributes1 = new ArbitrumPayloadAttributes()
            {
                MessageWithMetadata = new MessageWithMetadata(new L1IncomingMessage(header1, null, null), 10),
                Number = 1
            };

            var blockTracer = new BlockReceiptsTracer();
            var buildBlockTask = chain.BlockProducer.BuildBlock(chain.BlockTree.Head?.Header, blockTracer, payloadAttributes1);
            buildBlockTask.Wait(DefaultTimeoutMs);

            var block1 = buildBlockTask.Result;

            chain.BlockTree.SuggestBlock(block1!);

            ManualResetEventSlim blockProcessedEvent = new(false);
            chain.BlockProcessor.BlockProcessed += (_, _) =>
            {
                chain.BlockTree.UpdateMainChain([block1!], true);
                blockProcessedEvent.Set();
            };

            //2nd block
            var header2 = new L1IncomingMessageHeader(ArbitrumL1MessageKind.L2Message, TestItem.AddressA, 2,
                1200, null, l1BaseFee);
            var payloadAttributes2 = new ArbitrumPayloadAttributes()
            {
                MessageWithMetadata = new MessageWithMetadata(new L1IncomingMessage(header2, null, null), 10),
                Number = 2
            };

            blockProcessedEvent.Wait(DefaultTimeoutMs);

            buildBlockTask = chain.BlockProducer.BuildBlock(chain.BlockTree.Head?.Header, blockTracer, payloadAttributes2);
            buildBlockTask.Wait(DefaultTimeoutMs);

            buildBlockTask.IsCompletedSuccessfully.Should().BeTrue();

            var buildBlock = buildBlockTask.Result;
            buildBlock.Should().NotBeNull();
            buildBlock.Header.Timestamp.Should().Be(1500);
        }
    }
}
