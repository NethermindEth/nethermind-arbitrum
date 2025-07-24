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

            ArbitrumSubmitRetryableTx submitRetryableTx = new ArbitrumSubmitRetryableTx(chain.ChainSpec.ChainId,
                ticketIdHash, TestItem.AddressA, l1BaseFee, deposit, gasFeeCap, gasLimit, TestItem.AddressB,
                value, TestItem.AddressC, maxSubmissionFee, TestItem.AddressD, data);

            var tx = new ArbitrumTransaction<ArbitrumSubmitRetryableTx>(submitRetryableTx)
            {
                Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
                ChainId = submitRetryableTx.ChainId,
                SenderAddress = submitRetryableTx.From,
                SourceHash = submitRetryableTx.RequestId,
                DecodedMaxFeePerGas = submitRetryableTx.GasFeeCap,
                GasLimit = (long)submitRetryableTx.Gas,
                To = ArbitrumConstants.ArbRetryableTxAddress,
                Data = submitRetryableTx.RetryData.ToArray(),
                Mint = submitRetryableTx.DepositValue,
            };

            tx.Hash = tx.CalculateHash();

            BlockBody body = new BlockBody([tx], null);
            Block newBlock =
                new Block(
                    new BlockHeader(chain.BlockTree.HeadHash, null, TestItem.AddressF, UInt256.Zero, 0, 100_000, 100,
                        []), body);

            IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
            var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(),
                LimboLogs.Instance.GetLogger("arbosState"));
            newBlock.Header.BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get();

            Transaction actualTransaction = null!;
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

            var maxRefund = (submitRetryableTx.Gas * newBlock.Header.BaseFeePerGas) + maxSubmissionFee;
            var expectedRetryTx = new ArbitrumRetryTx(chain.ChainSpec.ChainId, 0, TestItem.AddressA,
                newBlock.Header.BaseFeePerGas,
                gasLimit, TestItem.AddressB, value, data, tx.Hash, TestItem.AddressD, maxRefund,
                maxSubmissionFee);

            var actualArbTransaction = actualTransaction as ArbitrumTransaction<ArbitrumRetryTx>;
            actualArbTransaction.Should().NotBeNull();
            actualArbTransaction.GasLimit = GasCostOf.Transaction;
            actualArbTransaction.Value.Should().Be(Unit.Ether / 100);
            actualArbTransaction.Inner.Should().BeEquivalentTo(expectedRetryTx, options => options
                .Using<ReadOnlyMemory<byte>>(ctx =>
                    ctx.Subject.Span.SequenceEqual(ctx.Expectation.Span).Should().BeTrue())
                .WhenTypeIs<ReadOnlyMemory<byte>>());
        }
    }
}
