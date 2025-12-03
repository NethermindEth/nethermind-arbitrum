using Autofac;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Blockchain.Tracing;
using Nethermind.Core;
using Nethermind.Core.Attributes;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.BlockProcessing
{

    [TestFixture]
    internal class BlockProcessorTests
    {
        public const int DefaultTimeoutMs = 1000;

        [Todo("This test was written early when full block processing / production pipeline was not available - check if it is still valid")]
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

            ArbitrumSubmitRetryableTransaction submitRetryableTx = new()
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

            BlockReceiptsTracer blockTracer = new();
            ulong nextBlockNumber = (ulong)((chain.BlockTree.Head?.Number ?? 0) + 1);

            L1IncomingMessageHeader header = new(ArbitrumL1MessageKind.SubmitRetryable, submitRetryableTx.SenderAddress, nextBlockNumber, (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                submitRetryableTx.RequestId, l1BaseFee);
            byte[] l2Msg = NitroL2MessageSerializer.SerializeTransactions([submitRetryableTx], header);

            ArbitrumPayloadAttributes payloadAttributes = new()
            {
                MessageWithMetadata = new MessageWithMetadata(new L1IncomingMessage(header, l2Msg, null), 10),
                Number = (long)nextBlockNumber
            };

            Task<Block?> buildBlockTask = chain.BlockProducer.BuildBlock(chain.BlockTree.Head?.Header, blockTracer, payloadAttributes);

            buildBlockTask.Wait(DefaultTimeoutMs);
            blockTracer.EndBlockTrace();

            Block newBlock = buildBlockTask.Result!;

            blockTracer.TxReceipts.Length.Should().Be(3);

            TxReceipt submitTxReceipt = blockTracer.TxReceipts[1];
            submitTxReceipt.Logs?.Length.Should()
                .Be(2); //logs checked in a different unit test, so just checking the count
            submitTxReceipt.GasUsed.Should().Be(GasCostOf.Transaction);

            UInt256 maxRefund = (submitRetryableTx.Gas * newBlock.Header.BaseFeePerGas) + maxSubmissionFee;
            ArbitrumRetryTransaction expectedRetryTx = new()
            {
                ChainId = chain.ChainSpec.ChainId,
                Nonce = 0,
                SenderAddress = TestItem.AddressA,
                GasFeeCap = newBlock.Header.BaseFeePerGas,
                Gas = gasLimit,
                To = TestItem.AddressB,
                Value = value,
                Data = data,
                TicketId = submitRetryableTx.CalculateHash(),
                RefundTo = TestItem.AddressD,
                MaxRefund = maxRefund,
                SubmissionFeeRefund = maxSubmissionFee,
                Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                DecodedMaxFeePerGas = newBlock.Header.BaseFeePerGas,
                GasLimit = (long)gasLimit,
                GasPrice = UInt256.Zero
            };
            expectedRetryTx.Hash = expectedRetryTx.CalculateHash();

            ArbitrumRetryTransaction? actualArbTransaction = newBlock.Transactions[2] as ArbitrumRetryTransaction;
            actualArbTransaction.Should().NotBeNull();
            actualArbTransaction.GasLimit = GasCostOf.Transaction;
            actualArbTransaction.Value.Should().Be(Unit.Ether / 100);
            actualArbTransaction.Should().BeEquivalentTo(expectedRetryTx, options => options
                .Using<ReadOnlyMemory<byte>>(ctx =>
                    ctx.Subject.Span.SequenceEqual(ctx.Expectation.Span).Should().BeTrue())
                .WhenTypeIs<ReadOnlyMemory<byte>>());
        }

        [Test]
        public void ProcessSubmitRetryable_WithNotEnoughGas_CreatesReceiptWithLogs()
        {
            UInt256 l1BaseFee = 39;

            Action<ContainerBuilder> preConfigurer = cb =>
            {
                cb.AddScoped(new ArbitrumTestBlockchainBase.Configuration()
                {
                    SuggestGenesisOnStart = true,
                    L1BaseFee = l1BaseFee,
                    FillWithTestDataOnStart = true
                });
            };

            ArbitrumRpcTestBlockchain chain = ArbitrumRpcTestBlockchain.CreateDefault(preConfigurer);

            Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(1);
            UInt256 gasFeeCap = 1000000000;
            UInt256 value = 10000000000000000;
            ulong gasLimit = GasCostOf.Transaction - 1; // Set gas limit just below the transaction cost to test early exit
            ReadOnlyMemory<byte> data = ReadOnlyMemory<byte>.Empty;
            ulong maxSubmissionFee = 54600;
            UInt256 deposit = 10021000000054600;

            ArbitrumSubmitRetryableTransaction tx = new()
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
                Data = data.ToArray(),
                Nonce = 0,
                Mint = deposit,
                Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
                To = ArbitrumConstants.ArbRetryableTxAddress,
                SourceHash = ticketIdHash
            };

            tx.Hash = tx.CalculateHash();

            Span<byte> l2Msg = stackalloc byte[512];
            tx.ToBinaryStream(l2Msg);

            L1IncomingMessageHeader incomingHeader = new(ArbitrumL1MessageKind.SubmitRetryable, TestItem.AddressC, 1,
                1500, Keccak.Compute("1"), l1BaseFee);

            ArbitrumPayloadAttributes payloadAttributes = new()
            {
                MessageWithMetadata = new MessageWithMetadata(new L1IncomingMessage(incomingHeader, l2Msg.ToArray(), null), 10),
                Number = 2
            };

            //need to make sure we get both block production and block processing right as we manipulate transaction data for early return to control gas spent
            BlockReceiptsTracer blockTracer = new();
            Task<Block?> buildBlockTask =
                chain.BlockProducer.BuildBlock(chain.BlockTree.BestSuggestedHeader, blockTracer, payloadAttributes);

            buildBlockTask.Wait(DefaultTimeoutMs);

            Block? processedBlock = null;
            TxReceipt[]? receipts = null;
            ManualResetEventSlim resetEvent = new(false);
            chain.BranchProcessor.BlockProcessed += (sender, args) =>
            {
                processedBlock = args.Block;
                receipts = args.TxReceipts;
                resetEvent.Set();
            };

            buildBlockTask.Result.Should().NotBeNull();

            chain.BlockTree.SuggestBlock(buildBlockTask.Result!);
            resetEvent.Wait(DefaultTimeoutMs);

            processedBlock.Should().NotBeNull();
            processedBlock.Hash.Should().BeEquivalentTo(buildBlockTask.Result.Hash);
            receipts?.Length.Should().Be(2);
            receipts?[1].Logs?.Length.Should().Be(1); //early return, so we only get 1 entry instead of 2
            receipts?[1].GasUsed.Should().Be(0);
        }
    }

    internal static class TxTestExtensions
    {
        internal static int ToBinaryStream(this ArbitrumSubmitRetryableTransaction tx, Span<byte> target)
        {
            int length = 0;
            (tx.RetryTo ?? Address.Zero).ToHash().Bytes.CopyTo(target[..(length += 32)]);
            tx.RetryValue.ToBigEndian(target[length..(length += 32)]);
            tx.DepositValue.ToBigEndian(target[length..(length += 32)]);
            tx.MaxSubmissionFee.ToBigEndian(target[length..(length += 32)]);
            tx.FeeRefundAddr.ToHash().Bytes.CopyTo(target[length..(length += 32)]);
            tx.Beneficiary.ToHash().Bytes.CopyTo(target[length..(length += 32)]);
            new UInt256((ulong)tx.GasLimit, 0, 0, 0).ToBigEndian(target[length..(length += 32)]);
            tx.MaxFeePerGas.ToBigEndian(target[length..(length += 32)]);

            new UInt256((ulong)tx.RetryData.Length, 0, 0, 0).ToBigEndian(target[length..(length += 32)]);
            tx.RetryData.Span.CopyTo(target[length..(length += tx.RetryData.Length)]);

            return length;
        }
    }
}
