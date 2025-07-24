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
        private TxDecoder _decoder;

        [OneTimeSetUp]
        public void Setup()
        {
            _decoder = TxDecoder.Instance;
            _decoder.RegisterDecoder(new ArbitrumInternalTxDecoder<Transaction>());
            _decoder.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder<ArbitrumTransaction<ArbitrumSubmitRetryableTx>>());
            _decoder.RegisterDecoder(new ArbitrumRetryTxDecoder<ArbitrumTransaction<ArbitrumRetryTx>>());
            _decoder.RegisterDecoder(new ArbitrumDepositTxDecoder<ArbitrumTransaction<ArbitrumDepositTx>>());
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

            ArbitrumSubmitRetryableTx submitRetryableTx = new ArbitrumSubmitRetryableTx(chainId,
                ticketIdHash, senderAddress, l1BaseFee, deposit, gasFeeCap, gasLimit, retryToAddress,
                retryValue, beneficiaryAddress, maxSubmissionFee, beneficiaryAddress, ReadOnlyMemory<byte>.Empty);

            ArbitrumTransaction<ArbitrumSubmitRetryableTx> tx = new ArbitrumTransaction<ArbitrumSubmitRetryableTx>(submitRetryableTx)
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

            ArbitrumRetryTx retryTx = new ArbitrumRetryTx(chainId, nonce, senderAddress, gasFeeCap, gasLimit,
                recipientAddress, value, ReadOnlyMemory<byte>.Empty, ticketIdHash, refundToAddress, maxRefund,
                submissionFeeRefund);

            ArbitrumTransaction<ArbitrumRetryTx> tx = new ArbitrumTransaction<ArbitrumRetryTx>(retryTx)
            {
                ChainId = retryTx.ChainId,
                Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                SenderAddress = retryTx.From,
                To = retryTx.To,
                Value = retryTx.Value
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

            ArbitrumDepositTx depositTx = new ArbitrumDepositTx(chainId, l1RequestIdHash256, fromAddr, toAddr, value256);

            ArbitrumTransaction<ArbitrumDepositTx> tx = new ArbitrumTransaction<ArbitrumDepositTx>(depositTx)
            {
                ChainId = depositTx.ChainId,
                Type = (TxType)ArbitrumTxType.ArbitrumDeposit,
                SenderAddress = depositTx.From,
                To = depositTx.To,
                Value = depositTx.Value
            };

            tx.Hash = tx.CalculateHash();

            tx.Hash.Should().BeEquivalentTo(new Hash256(expectedHash));
        }

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

            ArbitrumSubmitRetryableTx submitRetryableTx = new(chainId, ticketIdHash, senderAddress,
                l1BaseFee, deposit, gasFeeCap, gasLimit, retryToAddress, retryValue,
                beneficiaryAddress, maxSubmissionFee, beneficiaryAddress, retryData);
            ArbitrumTransaction<ArbitrumSubmitRetryableTx> originalTx = new(submitRetryableTx)
            {
                Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
                ChainId = chainId,
                SenderAddress = senderAddress,
                To = ArbitrumConstants.ArbRetryableTxAddress,
                GasLimit = (long)gasLimit,
                Mint = deposit,
                Value = deposit,
                Data = retryData
            };
            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumTransaction<ArbitrumSubmitRetryableTx> decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForTransaction());
        }

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

            ArbitrumRetryTx retryTx = new(chainId, nonce, senderAddress, gasFeeCap, gasLimit,
                recipientAddress, value, txData, ticketIdHash, refundToAddress, maxRefund, submissionFeeRefund);
            ArbitrumTransaction<ArbitrumRetryTx> originalTx = new(retryTx)
            {
                ChainId = chainId,
                Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                SenderAddress = senderAddress,
                To = recipientAddress,
                Value = value,
                Nonce = nonce,
                GasLimit = (long)gasLimit,
                Data = txData
            };
            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumTransaction<ArbitrumRetryTx> decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForTransaction());
        }

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

            ArbitrumDepositTx depositTx = new(chainId, l1RequestIdHash, fromAddress, toAddress, valueAmount);
            ArbitrumTransaction<ArbitrumDepositTx> originalTx = new(depositTx)
            {
                ChainId = chainId,
                Type = (TxType)ArbitrumTxType.ArbitrumDeposit,
                SenderAddress = fromAddress,
                To = toAddress,
                Value = valueAmount
            };
            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumTransaction<ArbitrumDepositTx> decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForTransaction());
        }

        [TestCase(412346UL, new byte[] { 0xde, 0xad, 0xbe, 0xef })]
        [TestCase(1UL, new byte[] { })]
        [TestCase(999999UL, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 })]
        public void EncodeDecodeArbitrumInternalTx_Always_PreservesAllFields(ulong chainId, byte[] data)
        {
            Transaction originalTx = new()
            {
                Type = (TxType)ArbitrumTxType.ArbitrumInternal,
                ChainId = chainId,
                Data = data
            };

            Transaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForTransaction());
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

            ArbitrumSubmitRetryableTx submitRetryableTx = new(
                chainId,
                ArbRetryableTxTests.Hash256FromUlong(42),
                senderAddress,
                39,
                deposit,
                1000000000,
                gasLimit,
                new Address("0x3fab184622dc19b6109349b94811493bf2a45362"),
                10000000000000000,
                new Address("0x93b4c114b40ecf1fc34745400a1b9b9115c34e42"),
                54600,
                new Address("0x93b4c114b40ecf1fc34745400a1b9b9115c34e42"),
                largeRetryData);

            ArbitrumTransaction<ArbitrumSubmitRetryableTx> originalTx = new(submitRetryableTx)
            {
                Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
                ChainId = chainId,
                SenderAddress = senderAddress,
                To = ArbitrumConstants.ArbRetryableTxAddress,
                GasLimit = (long)gasLimit,
                Mint = deposit,
                Value = deposit,
                Data = largeRetryData
            };
            originalTx.Hash = originalTx.CalculateHash();

            ArbitrumTransaction<ArbitrumSubmitRetryableTx> decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForTransaction());

            Rlp encoded = _decoder.Encode(originalTx);
            encoded.Bytes.Length.Should().BeGreaterThan(32768);
        }

        [Test]
        public void EncodeDecodeArbitrumInternalTx_WithZeroChainId_PreservesAllFields()
        {
            Transaction originalTx = new()
            {
                Type = (TxType)ArbitrumTxType.ArbitrumInternal,
                ChainId = 0,
                Data = new byte[0]
            };

            Transaction decodedTx = EncodeDecode(_decoder, originalTx);

            decodedTx.Should().BeEquivalentTo(originalTx, o => o.ForTransaction());
        }

        [Test]
        public void DecodeArbitrumInternalTx_WithMalformedRlp_ThrowsException()
        {
            byte[] malformedRlp = {
                (byte)ArbitrumTxType.ArbitrumInternal,
                0xFF, 0xFF, 0xFF, 0xFF
            };

            Action decode = () => _decoder.Decode(new RlpStream(malformedRlp));
            decode.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void DecodeTransaction_WithUnknownTxType_ThrowsException()
        {
            byte[] unknownTypeTx = {
                200,
                0xc0
            };

            Action decode = () => _decoder.Decode(new RlpStream(unknownTypeTx));
            decode.Should().Throw<ArgumentOutOfRangeException>();
        }

        private static T EncodeDecode<T>(TxDecoder decoder, T input) where T : Transaction
        {
            return (T)decoder.Decode(new RlpStream(decoder.Encode(input).Bytes))!;
        }
    }
}
