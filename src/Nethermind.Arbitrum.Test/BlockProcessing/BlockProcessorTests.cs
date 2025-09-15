using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Blockchain.Tracing;
using Nethermind.Consensus.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

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

            ArbitrumSubmitRetryableTransaction submitRetryableTx = new ArbitrumSubmitRetryableTransaction
            {
                ChainId = chain.ChainSpec.ChainId,
                RequestId = ticketIdHash,
                SenderAddress = TestItem.AddressA,
                L1BaseFee = l1BaseFee,
                DepositValue = deposit,
                GasFeeCap = gasFeeCap,
                Gas = gasLimit,
                RetryTo = TestItem.AddressB,
                RetryValue = value,
                Beneficiary = TestItem.AddressC,
                MaxSubmissionFee = maxSubmissionFee,
                FeeRefundAddr = TestItem.AddressD,
                RetryData = data,
                Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
                SourceHash = ticketIdHash,
                DecodedMaxFeePerGas = gasFeeCap,
                GasLimit = (long)gasLimit,
                To = ArbitrumConstants.ArbRetryableTxAddress,
                Data = data.ToArray(),
                Mint = deposit,
                Nonce = UInt256.Zero,
                GasPrice = UInt256.Zero,
                Value = UInt256.Zero,
                IsOPSystemTransaction = false
            };

            submitRetryableTx.Hash = submitRetryableTx.CalculateHash();

            BlockBody body = new BlockBody([submitRetryableTx], null);
            Block newBlock =
                new Block(
                    new BlockHeader(chain.BlockTree.HeadHash, null, TestItem.AddressF, UInt256.Zero, 0, 100_000, 100,
                        []), body);

            IWorldState worldState = chain.WorldStateManager.GlobalWorldState;
            using var dispose = worldState.BeginScope(chain.BlockTree.Head!.Header);

            var arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(),
                LimboLogs.Instance.GetLogger("arbosState"));
            newBlock.Header.BaseFeePerGas = arbosState.L2PricingState.BaseFeeWeiStorage.Get();

            Transaction actualTransaction = null!;
            chain.MainProcessingContext.TransactionProcessed += (o, args) =>
            {
                if (args.Index == 1)
                    actualTransaction = args.Transaction;
            };

            var blockTracer = new BlockReceiptsTracer();
            blockTracer.StartNewBlockTrace(newBlock);

            chain.BlockProcessor.ProcessOne(newBlock, ProcessingOptions.ProducingBlock, blockTracer, chain.SpecProvider.GenesisSpec);

            blockTracer.EndBlockTrace();

            //assert
            blockTracer.TxReceipts.Count.Should().Be(2);

            var submitTxReceipt = blockTracer.TxReceipts[0];
            submitTxReceipt.Logs?.Length.Should()
                .Be(2); //logs checked in a different unit test, so just checking the count
            submitTxReceipt.GasUsed.Should().Be(GasCostOf.Transaction);

            var maxRefund = (submitRetryableTx.Gas * newBlock.Header.BaseFeePerGas) + maxSubmissionFee;
            var expectedRetryTx = new ArbitrumRetryTransaction
            {
                ChainId = chain.ChainSpec.ChainId,
                Nonce = 0,
                SenderAddress = TestItem.AddressA,
                GasFeeCap = newBlock.Header.BaseFeePerGas,
                Gas = gasLimit,
                To = TestItem.AddressB,
                Value = value,
                Data = data,
                TicketId = submitRetryableTx.Hash,
                RefundTo = TestItem.AddressD,
                MaxRefund = maxRefund,
                SubmissionFeeRefund = maxSubmissionFee,
                Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                DecodedMaxFeePerGas = newBlock.Header.BaseFeePerGas,
                GasLimit = (long)gasLimit,
                GasPrice = UInt256.Zero
            };

            expectedRetryTx.Hash = expectedRetryTx.CalculateHash();

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
