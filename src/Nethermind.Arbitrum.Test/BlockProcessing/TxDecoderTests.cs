using FluentAssertions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.BlockProcessing
{
    [TestFixture]
    internal class TxDecoderTests
    {
        private TxDecoder _decoder;

        [OneTimeSetUp]
        public void Setup()
        {
            _decoder = TxDecoder.Instance;
            // Update decoder registrations to match the new transaction classes
            _decoder.RegisterDecoder(new ArbitrumInternalTxDecoder());
            _decoder.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder());
            _decoder.RegisterDecoder(new ArbitrumRetryTxDecoder());
            _decoder.RegisterDecoder(new ArbitrumDepositTxDecoder());
        }

        [Test]
        [TestCase(1UL, "dd6bd74674c356345db88c354491c7d3173c6806", 39UL, 10021000000054600UL, 1000000000UL, 21000UL,
            "3fab184622dc19b6109349b94811493bf2a45362", 10000000000000000UL,
            "0x93b4c114b40ecf1fc34745400a1b9b9115c34e42", 54600UL,
            "0xcfb3f4f75e092c28579f5b536c8919d63b823bf487c2c946ae8ad539ed2a971d")]
        public void SubmitRetryableTx_Hash_CalculatesCorrectly(ulong ticketId, string sender, ulong l1BaseFee,
            ulong deposit, ulong gasFeeCap, ulong gasLimit, string retryTo, ulong retryValue, string beneficiary,
            ulong maxSubmissionFee, string expectedHash)
        {
            ulong chainId = 412346;

            Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(ticketId);
            Address.TryParse(sender, out Address? senderAddress);
            Address.TryParse(retryTo, out Address? retryToAddress);
            Address.TryParse(beneficiary, out Address? beneficiaryAddress);

            ArbitrumSubmitRetryableTransaction tx = new ArbitrumSubmitRetryableTransaction
            {
                ChainId = chainId,
                RequestId = ticketIdHash,
                SenderAddress = senderAddress!,
                L1BaseFee = l1BaseFee,
                DepositValue = deposit,
                DecodedMaxFeePerGas = gasFeeCap,
                GasFeeCap = gasFeeCap,
                GasLimit = (long)gasLimit,
                Gas = gasLimit,
                RetryTo = retryToAddress,
                RetryValue = retryValue,
                Beneficiary = beneficiaryAddress!,
                MaxSubmissionFee = maxSubmissionFee,
                FeeRefundAddr = beneficiaryAddress!,
                RetryData = ReadOnlyMemory<byte>.Empty,
            };

            tx.Hash = tx.CalculateHash();

            tx.Hash.Should().BeEquivalentTo(new Hash256(expectedHash));
        }

        [Test]
        [TestCase("0xcfb3f4f75e092c28579f5b536c8919d63b823bf487c2c946ae8ad539ed2a971d", 0UL,
            "dd6bd74674c356345db88c354491c7d3173c6806", 100000000UL, 21000UL,
            "3fab184622dc19b6109349b94811493bf2a45362", 10000000000000000UL,
            "0x93b4c114b40ecf1fc34745400a1b9b9115c34e42", 2100000054600UL, 54600UL,
            "0xf2df0912b3d8b8e41d4d88fae405def3a64ae0ef1a229d1b517ef2f5c07e2c15")]
        public void RetryTx_Hash_CalculatesCorrectly(string ticketId, ulong nonce, string sender, ulong gasFeeCap,
            ulong gasLimit, string recipient, ulong value, string refundTo, ulong maxRefund, ulong submissionFeeRefund,
            string expectedHash)
        {
            ulong chainId = 412346;

            Hash256 ticketIdHash = new Hash256(ticketId);
            Address.TryParse(sender, out Address? senderAddress);
            Address.TryParse(recipient, out Address? recipientAddress);
            Address.TryParse(refundTo, out Address? refundToAddress);

            ArbitrumRetryTransaction tx = new ArbitrumRetryTransaction
            {
                ChainId = chainId,
                Nonce = nonce,
                SenderAddress = senderAddress!,
                DecodedMaxFeePerGas = gasFeeCap,
                GasFeeCap = gasFeeCap,
                Gas = gasLimit,
                GasLimit = (long)gasLimit,
                To = recipientAddress,
                Value = value,
                Data = ReadOnlyMemory<byte>.Empty,
                TicketId = ticketIdHash,
                RefundTo = refundToAddress!,
                MaxRefund = maxRefund,
                SubmissionFeeRefund = submissionFeeRefund
            };

            tx.Hash = tx.CalculateHash();

            tx.Hash.Should().BeEquivalentTo(new Hash256(expectedHash));
        }

        [Test]
        [TestCase("0x0000000000000000000000000000000000000000000000000000000000000009",
            "0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F",
            "0x3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E",
            "100000000000000000000000",
            "0x38132c766a25034f7805a2f47c1bd4b23a97b979cf7b52bdcf29972c2e13f1e6")]
        public void DepositTx_Hash_CalculatesCorrectly(
            string l1RequestId, string from, string to, string value, string expectedHash)
        {
            ulong chainId = 412346;

            Hash256 l1RequestIdHash256 = new Hash256(l1RequestId);
            Address.TryParse(from, out Address? fromAddr);
            Address.TryParse(to, out Address? toAddr);
            UInt256.TryParse(value, out UInt256 value256);

            ArbitrumDepositTransaction tx = new ArbitrumDepositTransaction
            {
                ChainId = chainId,
                L1RequestId = l1RequestIdHash256,
                SenderAddress = fromAddr!,
                To = toAddr!,
                Value = value256
            };

            tx.Hash = tx.CalculateHash();

            tx.Hash.Should().BeEquivalentTo(new Hash256(expectedHash));
        }

        [Test]
        [TestCase(1UL, "dd6bd74674c356345db88c354491c7d3173c6806", 39UL, 10021000000054600UL, 1000000000UL, 21000UL,
            "3fab184622dc19b6109349b94811493bf2a45362", 10000000000000000UL,
            "93b4c114b40ecf1fc34745400a1b9b9115c34e42", 54600UL)]
        public void SubmitRetryableTx_RealData_EncodeDecode_Preserves_AllFields(ulong ticketId, string sender,
            ulong l1BaseFee, ulong deposit, ulong gasFeeCap, ulong gasLimit, string retryTo, ulong retryValue,
            string beneficiary, ulong maxSubmissionFee)
        {
            // Arrange - Real dev chain data
            ulong chainId = 412346;
            Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(ticketId);
            Address senderAddress = new Address(sender);
            Address retryToAddress = new Address(retryTo);
            Address beneficiaryAddress = new Address(beneficiary);
            var retryData = new byte[] { 0xde, 0xad, 0xbe, 0xef, 0xca, 0xfe };

            ArbitrumSubmitRetryableTransaction originalTx = new ArbitrumSubmitRetryableTransaction
            {
                ChainId = chainId,
                RequestId = ticketIdHash,
                SenderAddress = senderAddress,
                L1BaseFee = l1BaseFee,
                DepositValue = deposit,
                DecodedMaxFeePerGas = gasFeeCap,
                GasFeeCap = gasFeeCap,
                GasLimit = (long)gasLimit,
                Gas = gasLimit,
                RetryTo = retryToAddress,
                RetryValue = retryValue,
                Beneficiary = beneficiaryAddress,
                MaxSubmissionFee = maxSubmissionFee,
                FeeRefundAddr = beneficiaryAddress,
                RetryData = retryData,
            };

            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumSubmitRetryableTransaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumSubmitRetryableTransaction());
        }

        [Test]
        [TestCase("0xcfb3f4f75e092c28579f5b536c8919d63b823bf487c2c946ae8ad539ed2a971d", 0UL,
            "dd6bd74674c356345db88c354491c7d3173c6806", 100000000UL, 21000UL,
            "3fab184622dc19b6109349b94811493bf2a45362", 10000000000000000UL,
            "93b4c114b40ecf1fc34745400a1b9b9115c34e42", 2100000054600UL, 54600UL)]
        public void RetryTx_RealData_EncodeDecode_Preserves_AllFields(string ticketId, ulong nonce, string sender,
            ulong gasFeeCap, ulong gasLimit, string recipient, ulong value, string refundTo,
            ulong maxRefund, ulong submissionFeeRefund)
        {
            // Arrange - Real dev chain data
            ulong chainId = 412346;
            Hash256 ticketIdHash = new Hash256(ticketId);
            Address senderAddress = new Address(sender);
            Address recipientAddress = new Address(recipient);
            Address refundToAddress = new Address(refundTo);
            var txData = new byte[] { 0x12, 0x34, 0x56, 0x78 };

            ArbitrumRetryTransaction originalTx = new ArbitrumRetryTransaction
            {
                ChainId = chainId,
                Nonce = nonce,
                SenderAddress = senderAddress,
                DecodedMaxFeePerGas = gasFeeCap,
                GasFeeCap = gasFeeCap,
                Gas = gasLimit,
                GasLimit = (long)gasLimit,
                To = recipientAddress,
                Value = value,
                Data = txData,
                TicketId = ticketIdHash,
                RefundTo = refundToAddress,
                MaxRefund = maxRefund,
                SubmissionFeeRefund = submissionFeeRefund
            };

            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumRetryTransaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumRetryTransaction());
        }

        [Test]
        [TestCase("0x0000000000000000000000000000000000000000000000000000000000000009",
            "502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F",
            "3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E",
            "100000000000000000000000")]
        public void EncodeDecodeDepositTx_Always_PreservesAllFields(string l1RequestId, string from,
            string to, string value)
        {
            ulong chainId = 412346;
            Hash256 l1RequestIdHash = new(l1RequestId);
            Address fromAddress = new(from);
            Address toAddress = new(to);
            UInt256 valueAmount = UInt256.Parse(value);

            ArbitrumDepositTransaction originalTx = new ArbitrumDepositTransaction
            {
                ChainId = chainId,
                L1RequestId = l1RequestIdHash,
                SenderAddress = fromAddress,
                To = toAddress,
                Value = valueAmount
            };

            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumDepositTransaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumDepositTransaction());
        }

        [Test]
        [TestCase(412346UL, new byte[] { 0xde, 0xad, 0xbe, 0xef })]
        [TestCase(1UL, new byte[] { })]
        [TestCase(999999UL, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 })]
        public void EncodeDecodeArbitrumInternalTx_Always_PreservesAllFields(ulong chainId, byte[] data)
        {
            ArbitrumInternalTransaction originalTx = new ArbitrumInternalTransaction
            {
                ChainId = chainId,
                Data = data
            };

            ArbitrumInternalTransaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumInternalTransaction());
        }

        [Test]
        public void EncodeDecodeSubmitRetryableTx_WithLargeData_PreservesAllFields()
        {
            byte[] largeRetryData = new byte[32768];
            for (int i = 0; i < largeRetryData.Length; i++)
            {
                largeRetryData[i] = (byte)(i % 256);
            }

            ulong chainId = 412346;
            ulong deposit = 10021000000054600;
            ulong gasLimit = 21000;
            Address senderAddress = new("0xdd6bd74674c356345db88c354491c7d3173c6806");

            ArbitrumSubmitRetryableTransaction originalTx = new ArbitrumSubmitRetryableTransaction
            {
                ChainId = chainId,
                RequestId = ArbRetryableTxTests.Hash256FromUlong(42),
                SenderAddress = senderAddress,
                L1BaseFee = 39,
                DepositValue = deposit,
                DecodedMaxFeePerGas = 1000000000,
                GasFeeCap = 1000000000,
                GasLimit = (long)gasLimit,
                Gas = gasLimit,
                RetryTo = new Address("0x3fab184622dc19b6109349b94811493bf2a45362"),
                RetryValue = 10000000000000000,
                Beneficiary = new Address("0x93b4c114b40ecf1fc34745400a1b9b9115c34e42"),
                MaxSubmissionFee = 54600,
                FeeRefundAddr = new Address("0x93b4c114b40ecf1fc34745400a1b9b9115c34e42"),
                RetryData = largeRetryData,
            };

            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumSubmitRetryableTransaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumSubmitRetryableTransaction());

            Rlp encoded = _decoder.Encode(originalTx);
            encoded.Bytes.Length.Should().BeGreaterThan(32768);
        }

        [Test]
        public void EncodeDecodeArbitrumInternalTx_WithZeroChainId_PreservesAllFields()
        {
            ArbitrumInternalTransaction originalTx = new ArbitrumInternalTransaction
            {
                ChainId = 0,
                Data = new byte[0]
            };

            ArbitrumInternalTransaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumInternalTransaction());
        }

        [Test]
        public void DecodeArbitrumInternalTx_WithMalformedRlp_ThrowsException()
        {
            byte[] malformedRlp = {
                (byte)ArbitrumTxType.ArbitrumInternal,
                0xFF, 0xFF, 0xFF, 0xFF
            };

            Action decode = () => _decoder.Decode(new RlpStream(malformedRlp));
            decode.Should().Throw<Exception>(); // More general exception type
        }

        [Test]
        public void DecodeTransaction_WithUnknownTxType_ThrowsException()
        {
            byte[] unknownTypeTx = {
                200,
                0xc0
            };

            Action decode = () => _decoder.Decode(new RlpStream(unknownTypeTx));
            decode.Should().Throw<Exception>("unknown transaction types should be rejected");
        }

        private static T EncodeDecode<T>(TxDecoder decoder, T input) where T : Transaction
        {
            return (T)decoder.Decode(new RlpStream(decoder.Encode(input).Bytes))!;
        }
    }
}
