using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.BlockProcessing
{

    [TestFixture]
    internal class BlockProcessorTests
    {
        [Test]
        public void ProcessTransactions_CreatesRetryTx_FromEmittedLog()
        {
            using var mock = AutoMock.GetLoose();

            var preConfigurer = (ContainerBuilder cb) =>
            {
                cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
                { SuggestGenesisOnStart = true, FillWithTestDataOnStart = true });
                cb.RegisterMock(mock.Mock<ITransactionProcessor>());
            };

            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

            var ethereumEcdsa = new EthereumEcdsa(chain.SpecProvider.ChainId);

            //trigger transaction - not processed but should emit event log picked up as retry tx
            Transaction transaction = Build.A.Transaction
                .WithGasLimit(10)
                .WithGasPrice(1)
                .WithNonce(0)
                .WithValue(UInt256.One)
                .To(TestItem.AddressA)
                .SignedAndResolved(ethereumEcdsa, TestItem.PrivateKeyA)
                .TestObject;

            BlockBody body = new BlockBody([transaction], null);
            Block newBlock =
                new Block(
                    new BlockHeader(chain.BlockTree.HeadHash, null, TestItem.AddressF, UInt256.Zero, 0, 100_000, 100,
                        []), body);

            IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
            Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(123);

            var expectedTx = TestTransaction.PrepareArbitrumRetryTx(worldState, newBlock.Header,
                ticketIdHash, TestItem.AddressB, TestItem.AddressC, newBlock.Beneficiary!, 1.Ether());
            var expectedRetryTx = expectedTx as ArbitrumRetryTransaction;

            ArbitrumTransaction? actualArbitrumTransaction = null;

            //need to mock processing as not implemented yet
            mock.Mock<ITransactionProcessor>().Setup(x =>
                x.BuildUp(It.IsAny<Transaction>(), It.IsAny<ITxTracer>())).Returns(
                (Transaction tx, ITxTracer tracer) =>
                {
                    //for the fake trigger transaction (should be SubmitRetryable when that gets implemented), emit and event and mark as processed
                    if (tx.SenderAddress == TestItem.AddressA)
                    {
                        var log = EventsEncoder.BuildLogEntryFromEvent(
                            ArbRetryableTx.RedeemScheduledEvent, ArbRetryableTx.Address, ticketIdHash,
                            expectedTx.Hash, 0, expectedRetryTx.Gas, expectedRetryTx.RefundTo,
                            expectedRetryTx.MaxRefund, 0);
                        tracer.MarkAsSuccess(null, 10, [], [log]);
                        return TransactionResult.Ok;
                    }

                    //should be the injected retry tx - just save, so can be asserted later on not to make the mock too big
                    if (tx is ArbitrumTransaction arbitrumTransaction)
                    {
                        actualArbitrumTransaction = arbitrumTransaction;
                        tracer.MarkAsSuccess(null, 10, [], []);
                        return TransactionResult.Ok;
                    }

                    return TransactionResult.MalformedTransaction;
                });

            ISpecProvider chainSpec = FullChainSimulationSpecProvider.Instance;

            ArbitrumBlockProductionTransactionsExecutor executor =
                new ArbitrumBlockProductionTransactionsExecutor(chain.TxProcessor,
                    chain.WorldStateManager.GlobalWorldState, chainSpec, LimboLogs.Instance);

            var blockTracer = new BlockReceiptsTracer();
            blockTracer.StartNewBlockTrace(newBlock);

            executor.ProcessTransactions(newBlock, ProcessingOptions.ProducingBlock, blockTracer,
                FullChainSimulationReleaseSpec.Instance);

            blockTracer.EndBlockTrace();

            //assert
            mock.Mock<ITransactionProcessor>().Verify(tp => tp.BuildUp(It.IsAny<Transaction>(), It.IsAny<ITxTracer>()),
                Times.Exactly(2));

            actualArbitrumTransaction.Should().NotBeNull();
            actualArbitrumTransaction.Should().NotBeNull().And.BeEquivalentTo(expectedTx, options =>
                options.Using<ReadOnlyMemory<byte>>(ctx =>
                        ctx.Subject.Span.SequenceEqual(ctx.Expectation.Span).Should().BeTrue())
                    .WhenTypeIs<ReadOnlyMemory<byte>>());
        }

        [Test]
        public void ProcessTransactions_SubmitRetryable_CreatesRetryTx()
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

            Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(1);
            UInt256 gasFeeCap = 1000000000;
            UInt256 value = 10000000000000000;
            ulong gasLimit = 21000;
            var data = ReadOnlyMemory<byte>.Empty;
            ulong maxSubmissionFee = 54600;
            UInt256 deposit = 10021000000054600;

            ArbitrumSubmitRetryableTransaction submitRetryableTx = new ArbitrumSubmitRetryableTransaction
            {
                ChainId = chain.ChainSpec.ChainId,
                RequestId = ticketIdHash,
                SenderAddress = TestItem.AddressA,
                L1BaseFee = l1BaseFee,
                DepositValue = deposit,
                DecodedMaxFeePerGas = gasFeeCap,
                GasFeeCap = gasFeeCap,
                GasLimit = (long)gasLimit,
                Gas = gasLimit,
                RetryTo = TestItem.AddressB,
                RetryValue = value,
                Beneficiary = TestItem.AddressC,
                MaxSubmissionFee = maxSubmissionFee,
                FeeRefundAddr = TestItem.AddressD,
                RetryData = data,
                Data = data,
                Nonce = 0,
                Mint = deposit
            };

            // Set transaction properties
            var tx = submitRetryableTx;
            tx.Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable;
            tx.To = ArbitrumConstants.ArbRetryableTxAddress;

            tx.Hash = tx.CalculateHash();

            BlockBody body = new BlockBody([tx], null);
            Block newBlock =
                new Block(
                    new BlockHeader(chain.BlockTree.HeadHash, null, TestItem.AddressF, UInt256.Zero, 0, 100_000, 100,
                        []), body);

            IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
            var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(),
                LimboLogs.Instance.GetLogger("arbosState"));

            // Set the base fee BEFORE creating expected transaction
            UInt256 blockBaseFee = arbosState.L2PricingState.BaseFeeWeiStorage.Get();
            newBlock.Header.BaseFeePerGas = blockBaseFee;

            // Create expected transaction matching GetScheduledTransactions logic
            var maxRefund = (submitRetryableTx.Gas * blockBaseFee) + maxSubmissionFee;
            ArbitrumRetryTransaction expectedRetryTx = new ArbitrumRetryTransaction
            {
                ChainId = chain.ChainSpec.ChainId,
                Nonce = 0,
                SenderAddress = TestItem.AddressA,
                DecodedMaxFeePerGas = blockBaseFee,
                GasFeeCap = blockBaseFee,
                Gas = gasLimit,
                GasLimit = (long)gasLimit,
                To = TestItem.AddressB,
                Value = value,
                Data = data,
                TicketId = tx.Hash,
                RefundTo = TestItem.AddressD,
                MaxRefund = maxRefund,
                SubmissionFeeRefund = maxSubmissionFee
            };

            // Set hash as GetScheduledTransactions does
            expectedRetryTx.Hash = expectedRetryTx.CalculateHash();

            //RetryTx processing not implemented yet - it's just reporting as processed, but can verify generated transaction
            Transaction actualTransaction = null;
            chain.BlockProcessor.TransactionProcessed += (o, args) =>
            {
                if (args.Index == 1)
                    actualTransaction = args.Transaction;
            };

            var blockTracer = new BlockReceiptsTracer();
            blockTracer.StartNewBlockTrace(newBlock);

            chain.BlockProcessor.Process(chain.BlockTree.Head?.StateRoot ?? Keccak.EmptyTreeHash,
                [newBlock], ProcessingOptions.ProducingBlock, blockTracer);

            blockTracer.EndBlockTrace();

            //assert
            blockTracer.TxReceipts.Count.Should().Be(2);

            var submitTxReceipt = blockTracer.TxReceipts[0];
            submitTxReceipt.Logs?.Length.Should()
                .Be(2); //logs checked in a different unit test, so just checking the count
            submitTxReceipt.GasUsed.Should().Be(GasCostOf.Transaction);

            var actualArbTransaction = actualTransaction as ArbitrumRetryTransaction;
            actualArbTransaction.Should().NotBeNull();
            actualArbTransaction.GasLimit = GasCostOf.Transaction;
            actualArbTransaction.Value.Should().Be(Unit.Ether / 100);

            actualArbTransaction.Should().BeEquivalentTo(expectedRetryTx, options => options
                .Using<ReadOnlyMemory<byte>>(ctx =>
                    ctx.Subject.Span.SequenceEqual(ctx.Expectation.Span).Should().BeTrue())
                .WhenTypeIs<ReadOnlyMemory<byte>>());
        }
    }
}
