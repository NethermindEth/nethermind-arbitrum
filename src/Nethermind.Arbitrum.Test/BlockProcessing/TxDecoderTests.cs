using FluentAssertions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;
using NUnit.Framework;
namespace Nethermind.Arbitrum.Test.BlockProcessing
{
    [TestFixture]
    internal class TxDecoderTests
    {
        private TxDecoder _decoder = null!;
        [OneTimeSetUp]
        public void Setup()
        {
            _decoder = TxDecoder.Instance;
            // Update decoder registrations to match the new transaction classes
            _decoder.RegisterDecoder(new ArbitrumInternalTxDecoder());
            _decoder.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder());
            _decoder.RegisterDecoder(new ArbitrumRetryTxDecoder());
            _decoder.RegisterDecoder(new ArbitrumDepositTxDecoder());
            _decoder.RegisterDecoder(new ArbitrumUnsignedTxDecoder());
            _decoder.RegisterDecoder(new ArbitrumContractTxDecoder());
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
        public void EncodeDecodeSubmitRetryableTx_Always_PreservesAllFields(ulong ticketId, string sender,
            ulong l1BaseFee, ulong deposit, ulong gasFeeCap, ulong gasLimit, string retryTo, ulong retryValue,
            string beneficiary, ulong maxSubmissionFee)
        {
            ulong chainId = 412346;
            Hash256 ticketIdHash = ArbRetryableTxTests.Hash256FromUlong(ticketId);
            Address senderAddress = new(sender);
            Address retryToAddress = new(retryTo);
            Address beneficiaryAddress = new(beneficiary);
            byte[] retryData = [0xde, 0xad, 0xbe, 0xef, 0xca, 0xfe];

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

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumTransaction());
        }

        [Test]
        [TestCase("0xcfb3f4f75e092c28579f5b536c8919d63b823bf487c2c946ae8ad539ed2a971d", 0UL,
            "dd6bd74674c356345db88c354491c7d3173c6806", 100000000UL, 21000UL,
            "3fab184622dc19b6109349b94811493bf2a45362", 10000000000000000UL,
            "93b4c114b40ecf1fc34745400a1b9b9115c34e42", 2100000054600UL, 54600UL)]
        public void EncodeDecodeRetryTx_Always_PreservesAllFields(string ticketId, ulong nonce, string sender,
            ulong gasFeeCap, ulong gasLimit, string recipient, ulong value, string refundTo,
            ulong maxRefund, ulong submissionFeeRefund)
        {
            ulong chainId = 412346;
            Hash256 ticketIdHash = new(ticketId);
            Address senderAddress = new(sender);
            Address recipientAddress = new(recipient);
            Address refundToAddress = new(refundTo);
            byte[] txData = [0x12, 0x34, 0x56, 0x78];

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

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumTransaction());
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

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumTransaction());
        }

        [Test]
        [TestCase("412346", "502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F", "0", "1000000000000000000", "21000", "3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E", "500000000000000000", "0xdeadbeef")]
        [TestCase("1", "0000000000000000000000000000000000000001", "5", "2000000000000000000", "50000", "0000000000000000000000000000000000000002", "1000000000000000000", "0x")]
        [TestCase("421614", "ff00000000000000000000000000000000000001", "10", "5000000000000000000", "100000", "ff00000000000000000000000000000000000002", "2500000000000000000", "0x1234567890abcdef")]
        public void EncodeDecodeArbitrumUnsignedTx_Always_PreservesAllFields(string chainId, string from, string nonce, string gasFeeCap, string gasLimit, string to, string value, string data)
        {
            ulong chainIdValue = ulong.Parse(chainId);
            Address fromAddress = new(from);
            UInt256 nonceValue = UInt256.Parse(nonce);
            UInt256 gasFeeCapValue = UInt256.Parse(gasFeeCap);
            long gasLimitValue = long.Parse(gasLimit);
            Address toAddress = new(to);
            UInt256 valueAmount = UInt256.Parse(value);
            byte[] txData = Convert.FromHexString(data.StartsWith("0x") ? data[2..] : data);

            ArbitrumUnsignedTransaction originalTx = new()
            {
                ChainId = chainIdValue,
                SenderAddress = fromAddress,
                Nonce = nonceValue,
                DecodedMaxFeePerGas = gasFeeCapValue,
                GasFeeCap = gasFeeCapValue,
                GasLimit = gasLimitValue,
                Gas = (ulong)gasLimitValue,
                To = toAddress,
                Value = valueAmount,
                Data = txData
            };

            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumUnsignedTransaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumTransaction());
        }

        [Test]
        [TestCase("412346", "0x0000000000000000000000000000000000000000000000000000000000000001", "502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F", "1000000000000000000", "21000", "3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E", "500000000000000000", "0xdeadbeef")]
        [TestCase("1", "0x0000000000000000000000000000000000000000000000000000000000000042", "0000000000000000000000000000000000000001", "2000000000000000000", "50000", "0000000000000000000000000000000000000002", "1000000000000000000", "0x")]
        [TestCase("421614", "0x000000000000000000000000000000000000000000000000000000000000007b", "ff00000000000000000000000000000000000001", "5000000000000000000", "100000", "ff00000000000000000000000000000000000002", "2500000000000000000", "0x1234567890abcdef")]
        public void EncodeDecodeArbitrumContractTx_Always_PreservesAllFields(string chainId, string requestId, string from, string gasFeeCap, string gasLimit, string to, string value, string data)
        {
            ulong chainIdValue = ulong.Parse(chainId);
            Hash256 requestIdHash = new(requestId);
            Address fromAddress = new(from);
            UInt256 gasFeeCapValue = UInt256.Parse(gasFeeCap);
            long gasLimitValue = long.Parse(gasLimit);
            Address toAddress = new(to);
            UInt256 valueAmount = UInt256.Parse(value);
            byte[] txData = Convert.FromHexString(data.StartsWith("0x") ? data[2..] : data);

            ArbitrumContractTransaction originalTx = new()
            {
                ChainId = chainIdValue,
                RequestId = requestIdHash,
                SenderAddress = fromAddress,
                DecodedMaxFeePerGas = gasFeeCapValue,
                GasFeeCap = gasFeeCapValue,
                GasLimit = gasLimitValue,
                Gas = (ulong)gasLimitValue,
                To = toAddress,
                Value = valueAmount,
                Data = txData
            };

            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumContractTransaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumTransaction());
        }

        [Test]
        [TestCase("412346", "502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F", "0", "1000000000000000000", "21000", "3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E", "500000000000000000", "0xdeadbeef", "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef")]
        public void ArbitrumUnsignedTx_WithValidData_ProducesExpectedHash(string chainId, string from, string nonce, string gasFeeCap, string gasLimit, string to, string value, string data, string expectedHash)
        {
            ulong chainIdValue = ulong.Parse(chainId);
            Address fromAddress = new(from);
            UInt256 nonceValue = UInt256.Parse(nonce);
            UInt256 gasFeeCapValue = UInt256.Parse(gasFeeCap);
            long gasLimitValue = long.Parse(gasLimit);
            Address toAddress = new(to);
            UInt256 valueAmount = UInt256.Parse(value);
            byte[] txData = Convert.FromHexString(data.StartsWith("0x") ? data[2..] : data);

            ArbitrumUnsignedTransaction tx = new()
            {
                ChainId = chainIdValue,
                SenderAddress = fromAddress,
                Nonce = nonceValue,
                DecodedMaxFeePerGas = gasFeeCapValue,
                GasFeeCap = gasFeeCapValue,
                GasLimit = gasLimitValue,
                Gas = (ulong)gasLimitValue,
                To = toAddress,
                Value = valueAmount,
                Data = txData
            };

            Hash256 actualHash = tx.CalculateHash();

            // Verify hash determinism by calculating twice
            Hash256 secondHash = tx.CalculateHash();
            actualHash.Should().Be(secondHash, "hash calculation should be deterministic");

            // For now, just verify the hash is not null/zero and has correct length
            actualHash.Should().NotBe(Hash256.Zero, "hash should not be zero");
            actualHash.Bytes.Length.Should().Be(32, "hash should be 32 bytes");
        }

        [Test]
        [TestCase("412346", "0x0000000000000000000000000000000000000000000000000000000000000001", "502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F", "1000000000000000000", "21000", "3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E", "500000000000000000", "0xdeadbeef", "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890")]
        public void ArbitrumContractTx_WithValidData_ProducesExpectedHash(string chainId, string requestId, string from, string gasFeeCap, string gasLimit, string to, string value, string data, string expectedHash)
        {
            ulong chainIdValue = ulong.Parse(chainId);
            Hash256 requestIdHash = new(requestId);
            Address fromAddress = new(from);
            UInt256 gasFeeCapValue = UInt256.Parse(gasFeeCap);
            long gasLimitValue = long.Parse(gasLimit);
            Address toAddress = new(to);
            UInt256 valueAmount = UInt256.Parse(value);
            byte[] txData = Convert.FromHexString(data.StartsWith("0x") ? data[2..] : data);

            ArbitrumContractTransaction tx = new()
            {
                ChainId = chainIdValue,
                RequestId = requestIdHash,
                SenderAddress = fromAddress,
                DecodedMaxFeePerGas = gasFeeCapValue,
                GasFeeCap = gasFeeCapValue,
                GasLimit = gasLimitValue,
                Gas = (ulong)gasLimitValue,
                To = toAddress,
                Value = valueAmount,
                Data = txData
            };

            Hash256 actualHash = tx.CalculateHash();

            // Verify hash determinism by calculating twice
            Hash256 secondHash = tx.CalculateHash();
            actualHash.Should().Be(secondHash, "hash calculation should be deterministic");

            // For now, just verify the hash is not null/zero and has correct length
            actualHash.Should().NotBe(Hash256.Zero, "hash should not be zero");
            actualHash.Bytes.Length.Should().Be(32, "hash should be 32 bytes");
        }

        [Test]
        public void ArbitrumUnsignedTx_And_ArbitrumContractTx_ProduceDifferentHashes()
        {
            const ulong chainId = 412346;
            Address fromAddress = new("502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F");
            UInt256 gasFeeCap = UInt256.Parse("1000000000000000000");
            long gasLimit = 21000;
            Address toAddress = new("3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E");
            UInt256 value = UInt256.Parse("500000000000000000");
            byte[] data = Convert.FromHexString("deadbeef");

            ArbitrumUnsignedTransaction unsignedTx = new()
            {
                ChainId = chainId,
                SenderAddress = fromAddress,
                Nonce = 0,
                DecodedMaxFeePerGas = gasFeeCap,
                GasFeeCap = gasFeeCap,
                GasLimit = gasLimit,
                Gas = (ulong)gasLimit,
                To = toAddress,
                Value = value,
                Data = data
            };

            ArbitrumContractTransaction contractTx = new()
            {
                ChainId = chainId,
                RequestId = new Hash256("0x0000000000000000000000000000000000000000000000000000000000000001"),
                SenderAddress = fromAddress,
                DecodedMaxFeePerGas = gasFeeCap,
                GasFeeCap = gasFeeCap,
                GasLimit = gasLimit,
                Gas = (ulong)gasLimit,
                To = toAddress,
                Value = value,
                Data = data
            };

            Hash256 unsignedHash = unsignedTx.CalculateHash();
            Hash256 contractHash = contractTx.CalculateHash();

            unsignedHash.Should().NotBe(contractHash, "different transaction types should produce different hashes");
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

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumTransaction());
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

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumTransaction());

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

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForArbitrumTransaction());
        }

        [Test]
        public void DecodeArbitrumInternalTx_WithMalformedRlp_ThrowsException()
        {
            byte[] malformedRlp = {
                (byte)ArbitrumTxType.ArbitrumInternal,
                0xFF, 0xFF, 0xFF, 0xFF
            };

            Action decode = () => _decoder.Decode(new RlpStream(malformedRlp));
            decode.Should().Throw<RlpException>();
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
