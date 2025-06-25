using Autofac;
using Autofac.Extras.Moq;
using Moq;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Events;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm.Tracing;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using FluentAssertions;
using static Nethermind.Arbitrum.Execution.ArbitrumBlockProcessor;

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
                new Block(new BlockHeader(chain.BlockTree.HeadHash, null, null, UInt256.Zero, 0, 100_000, 100, []), body);


            IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
            Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(123);

            var expectedRetryTx = PrepareArbitrumRetryTx(worldState, newBlock, ticketIdHash);
            var expectedTx = new ArbitrumTransaction<ArbitrumRetryTx>(expectedRetryTx);
            var expectedTxHash = expectedTx.CalculateHash();
            IArbitrumTransaction? actualArbitrumTransaction = null;

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
                            expectedTxHash, 0, expectedRetryTx.Gas, expectedRetryTx.RefundTo, expectedRetryTx.MaxRefund, 0);
                        tracer.MarkAsSuccess(null, 10, [], [log]);
                        return TransactionResult.Ok;
                    }

                    //should be the injected retry tx - just save, so can be asserted later on not to make the mock too big
                    if (tx is IArbitrumTransaction arbitrumTransaction)
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

            executor.ProcessTransactions(newBlock, ProcessingOptions.None, blockTracer,
                FullChainSimulationReleaseSpec.Instance);

            blockTracer.EndBlockTrace();

            //assert
            mock.Mock<ITransactionProcessor>().Verify(tp => tp.BuildUp(It.IsAny<Transaction>(), It.IsAny<ITxTracer>()) == TransactionResult.Ok,
                Times.Exactly(2));

            actualArbitrumTransaction.Should().NotBeNull();
            actualArbitrumTransaction.Should().NotBeNull().And.BeEquivalentTo(expectedTx, options =>
                options.Using<ReadOnlyMemory<byte>>(ctx =>
                        ctx.Subject.Span.SequenceEqual(ctx.Expectation.Span).Should().BeTrue())
                    .WhenTypeIs<ReadOnlyMemory<byte>>()
                    .Excluding(t => t.Type) //exclude properties dependent on Type as we don't yet have RetryableTx decoder
                    .Excluding(t => t.Supports1559)
                    .Excluding(t => t.SupportsAccessList));
        }

        private ArbitrumRetryTx PrepareArbitrumRetryTx(IWorldState worldState, Block block, Hash256 ticketIdHash)
        {
            ulong gasSupplied = ulong.MaxValue;
            PrecompileTestContextBuilder setupContext = new(worldState, gasSupplied);
            setupContext.WithArbosState().WithBlockExecutionContext(block);

            ulong calldataSize = 65;
            byte[] calldata = new byte[calldataSize];
            ulong timeout = block.Header.Timestamp + 1; // retryable not expired

            Retryable retryable = setupContext.ArbosState.RetryableState.CreateRetryable(
                ticketIdHash, Address.Zero, Address.Zero, 0, Address.Zero, timeout, calldata
            );

            ulong nonce = retryable.NumTries.Get(); // 0
            UInt256 maxRefund = UInt256.MaxValue;

            ArbitrumRetryTx expectedRetryInnerTx = new(
                setupContext.ChainId,
                nonce,
                retryable.From.Get(),
                setupContext.BlockExecutionContext.Header.BaseFeePerGas,
                0, // fill in after
                retryable.To?.Get(),
                retryable.CallValue.Get(),
                retryable.Calldata.Get(),
                ticketIdHash,
                setupContext.Caller,
                maxRefund,
                0
            );

            ArbRetryableTxTests.ComputeRedeemCost(out ulong gasToDonate, gasSupplied, calldataSize);

            // fix up the gas in the retry
            expectedRetryInnerTx.Gas = gasToDonate;

            return expectedRetryInnerTx;
        }
    }
}
