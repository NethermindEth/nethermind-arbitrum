using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Blockchain.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.JsonRpc;
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
            using var dispose = chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header);
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
            chain.BranchProcessor.BlockProcessed += (_, _) =>
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

        [Test]
        public void BlockTransactionPicker_WhenLegacyTxValidationFails_SkipTx()
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
            UInt256 baseFeeWei;
            using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
            {
                ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState,
                    new SystemBurner(), LimboNoErrorLogger.Instance);

                baseFeeWei = arbosState.L2PricingState.BaseFeeWeiStorage.Get();
            }

            EthereumEcdsa ethereumEcdsa = new(chain.SpecProvider.ChainId);

            Transaction incorrectNonceTx = Build.A.Transaction
                .WithGasLimit(GasCostOf.Transaction)
                .WithGasPrice(baseFeeWei)
                .WithNonce(2) //incorrect Nonce
                .WithValue(1.Ether())
                .To(TestItem.AddressB)
                .SignedAndResolved(ethereumEcdsa, TestItem.PrivateKeyA)
                .TestObject;

            Transaction invalidSenderTx = Build.A.Transaction
                .WithGasLimit(GasCostOf.Transaction)
                .WithGasPrice(baseFeeWei)
                .WithNonce(0)
                .WithValue(1.Ether())
                .To(TestItem.AddressB)
                .SignedAndResolved(ethereumEcdsa, TestItem.PrivateKeyD) //Address is a contract - not EOA
                .TestObject;

            Span<byte> l2Msg = stackalloc byte[350];
            int size = CreateSignedTxBatch(l2Msg, incorrectNonceTx, invalidSenderTx);
            l2Msg = l2Msg[..size];

            ArbitrumPayloadAttributes payloadAttributes = new()
            {
                MessageWithMetadata = new MessageWithMetadata(
                    new L1IncomingMessage(new(ArbitrumL1MessageKind.L2Message, TestItem.AddressC, 1, 1500, null, l1BaseFee),
                    l2Msg.ToArray(), null), 10),
                Number = 2
            };

            Task<Block?> buildBlockTask =
                chain.BlockProducer.BuildBlock(chain.BlockTree.BestSuggestedHeader, NullBlockTracer.Instance, payloadAttributes);

            buildBlockTask.Wait(DefaultTimeoutMs);

            //assert
            buildBlockTask.IsCompletedSuccessfully.Should().BeTrue();
            Block? builtBlock = buildBlockTask.Result;
            builtBlock.Should().NotBeNull();
            builtBlock.Transactions.Length.Should().Be(1); //only ArbitrumInternal tx
            builtBlock.Transactions.Should().NotContain(incorrectNonceTx);
            builtBlock.Transactions.Should().NotContain(invalidSenderTx);
        }

        [Test]
        public async Task BlockTransactionPicker_WhenStartTxHookPreProcessingFails_StillIncludesTx()
        {
            ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
                .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
                .Build();

            Hash256 requestId = Hash256.Zero;
            Address sender = new("0x0000000000000000000000000000000000000123"); // balance is 0
            Address receiver = TestItem.AddressA;
            Address beneficiary = TestItem.AddressB;

            UInt256 depositValue = 0; // no deposit to sender so that their balance stays 0
            UInt256 retryValue = 2.Ether();

            ulong gasLimit = 21000;
            UInt256 gasFee = 1.GWei();

            UInt256 l1BaseFee = 92;
            UInt256 maxSubmissionFee = 1000; // bigger than sender's balance

            TestSubmitRetryable retryable = new(requestId, l1BaseFee, sender, receiver, beneficiary, depositValue, retryValue, gasFee, gasLimit, maxSubmissionFee);
            ResultWrapper<MessageResult> result = await chain.Digest(retryable);
            result.Result.Should().Be(Result.Success);

            Block block = chain.BlockTree.Head!;
            // ArbitrumInternal tx + SubmitRetryable tx, but no retry tx as SubmitRetryable failed
            block.Transactions.Length.Should().Be(2);

            TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
            receipts.Should().HaveCount(2);
            receipts[0].TxType.Should().Be((TxType)ArbitrumTxType.ArbitrumInternal);
            receipts[0].StatusCode.Should().Be(StatusCode.Success);

            receipts[1].TxType.Should().Be((TxType)ArbitrumTxType.ArbitrumSubmitRetryable);
            receipts[1].StatusCode.Should().Be(StatusCode.Failure);
            // Fails at balanceAfterMint < tx.MaxSubmissionFee check in arbitrum tx processor
            receipts[1].Error.Should().Be("Fail : insufficient MaxFeePerGas for sender balance");
        }

        [Test]
        public void BlockTransactionPicker_WhenUnsignedTxValidationFails_SkipTx()
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
            UInt256 baseFeeWei;
            using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
            {
                ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState,
                    new SystemBurner(), LimboNoErrorLogger.Instance);

                baseFeeWei = arbosState.L2PricingState.BaseFeeWeiStorage.Get();
            }

            Span<byte> l2Msg = stackalloc byte[350];
            byte[] incorrectNonceTxData = CreateUnsignedTxData(GasCostOf.Transaction, baseFeeWei, 20, TestItem.AddressB, 5);
            byte[] validTxData = CreateUnsignedTxData(GasCostOf.Transaction, baseFeeWei, 0, TestItem.AddressB, 5);

            int size = CreateUnsignedTxBatch(l2Msg, incorrectNonceTxData, validTxData);
            l2Msg = l2Msg[..size];

            ArbitrumPayloadAttributes payloadAttributes = new()
            {
                MessageWithMetadata = new MessageWithMetadata(
                    new L1IncomingMessage(new(ArbitrumL1MessageKind.L2Message, TestItem.AddressC, 1, 1500, null, l1BaseFee),
                        l2Msg.ToArray(), null), 10),
                Number = 2
            };

            Task<Block?> buildBlockTask =
                chain.BlockProducer.BuildBlock(chain.BlockTree.BestSuggestedHeader, NullBlockTracer.Instance, payloadAttributes);

            buildBlockTask.Wait(DefaultTimeoutMs);

            //assert
            buildBlockTask.IsCompletedSuccessfully.Should().BeTrue();
            Block? builtBlock = buildBlockTask.Result;
            builtBlock.Should().NotBeNull();
            builtBlock.Transactions.Length.Should().Be(2); //ArbitrumInternal tx and one valied unsigned tx
        }

        [Test]
        public void BlockTransactionPicker_WhenPosterIsInvalidAccount_SkipUnsignedTx()
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
            UInt256 baseFeeWei;
            using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
            {
                ArbosState arbosState = ArbosState.OpenArbosState(chain.WorldStateManager.GlobalWorldState,
                    new SystemBurner(), LimboNoErrorLogger.Instance);

                baseFeeWei = arbosState.L2PricingState.BaseFeeWeiStorage.Get();
            }

            Span<byte> l2Msg = stackalloc byte[200];
            byte[] validTxData = CreateUnsignedTxData(GasCostOf.Transaction, baseFeeWei, 0, TestItem.AddressB, 5);

            int size = CreateUnsignedTxBatch(l2Msg, validTxData);
            l2Msg = l2Msg[..size];

            ArbitrumPayloadAttributes payloadAttributes = new()
            {
                //Poster is a contract address - not EOA - poster is used as sender for unsigned tx
                MessageWithMetadata = new MessageWithMetadata(
                    new L1IncomingMessage(new(ArbitrumL1MessageKind.L2Message, TestItem.AddressD, 1, 1500, null, l1BaseFee),
                        l2Msg.ToArray(), null), 10),
                Number = 2
            };

            Task<Block?> buildBlockTask =
                chain.BlockProducer.BuildBlock(chain.BlockTree.BestSuggestedHeader, NullBlockTracer.Instance, payloadAttributes);

            buildBlockTask.Wait(DefaultTimeoutMs);

            //assert
            buildBlockTask.IsCompletedSuccessfully.Should().BeTrue();
            Block? builtBlock = buildBlockTask.Result;
            builtBlock.Should().NotBeNull();
            builtBlock.Transactions.Length.Should().Be(1); //ArbitrumInternal tx only
        }

        private int CreateSignedTxBatch(Span<byte> l2Msg, params Transaction[] transactions)
        {
            l2Msg[0] = (byte)ArbitrumL2MessageKind.Batch;
            int written = 1;
            foreach (var tx in transactions)
            {
                Rlp txStream = TxDecoder.Instance.Encode(tx);
                ((ulong)txStream.Bytes.Length + 1).ToBigEndianByteArray().CopyTo(l2Msg[written..]);
                written += sizeof(ulong);
                l2Msg[written++] = (byte)ArbitrumL2MessageKind.SignedTx;
                txStream.Bytes.CopyTo(l2Msg[written..]);
                written += txStream.Bytes.Length;
            }
            return written;
        }
        private int CreateUnsignedTxBatch(Span<byte> l2Msg, params byte[][] transactionData)
        {
            l2Msg[0] = (byte)ArbitrumL2MessageKind.Batch;
            int written = 1;
            foreach (var txData in transactionData)
            {
                ((ulong)txData.Length + 1).ToBigEndianByteArray().CopyTo(l2Msg[written..]);
                written += sizeof(ulong);
                l2Msg[written++] = (byte)ArbitrumL2MessageKind.UnsignedUserTx;
                txData.CopyTo(l2Msg[written..]);
                written += txData.Length;
            }
            return written;
        }

        private static byte[] CreateUnsignedTxData(UInt256 gasLimit, UInt256 maxFeePerGas, UInt256 nonce, Address to, UInt256 value)
        {
            byte[] ret = new byte[5 * 32];
            Span<byte> target = ret.AsSpan();
            target[0] = (byte)ArbitrumL2MessageKind.UnsignedUserTx;

            gasLimit.ToBigEndian().CopyTo(target);
            maxFeePerGas.ToBigEndian().CopyTo(target[32..]);
            nonce.ToBigEndian().CopyTo(target[64..]);
            to.Bytes.CopyTo(target[(96 + 12)..]);
            value.ToBigEndian().CopyTo(target[128..]);
            return ret;
        }
    }
}
