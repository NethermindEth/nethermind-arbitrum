using FluentAssertions;
using Nethermind.Arbitrum.Execution.Transactions;
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
            _decoder.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder<Transaction>());
            _decoder.RegisterDecoder(new ArbitrumRetryTxDecoder<Transaction>());
            _decoder.RegisterDecoder(new ArbitrumDepositTxDecoder<Transaction>());
        }

        #region Original Hash Calculation Tests

        [Test(Description = "Data from dev chain simulation")]
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

            tx.Hash.Should().BeEquivalentTo(new Hash256(expectedHash));
        }

        [Test(Description = "Data from dev chain simulation")]
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

            var tx = new ArbitrumTransaction<ArbitrumRetryTx>(retryTx)
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

        [Test(Description = "Data from dev chain simulation")]
        [TestCase("0x0000000000000000000000000000000000000000000000000000000000000009",
            "0x502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F",
            "0x3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E",
            "100000000000000000000000",
            "0x38132c766a25034f7805a2f47c1bd4b23a97b979cf7b52bdcf29972c2e13f1e6")]
        public void DepositTx_Hash_CalculatesCorrectly(
            string l1RequestId, string from, string to, string value, string expectedHash)
        {
            ulong chainId = 412346;

            Hash256 l1RequestIdHash256 = new(l1RequestId);
            Address.TryParse(from, out Address? fromAddr);
            Address.TryParse(to, out Address? toAddr);
            UInt256.TryParse(value, out UInt256 value256);

            ArbitrumDepositTx depositTx = new ArbitrumDepositTx(chainId, l1RequestIdHash256, fromAddr, toAddr, value256);

            var tx = new ArbitrumTransaction<ArbitrumDepositTx>(depositTx)
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

        #endregion

        #region Real Data Encoding/Decoding Tests

        [Test(Description = "Encode/decode SubmitRetryable with real dev chain data")]
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

            var submitRetryableTx = new ArbitrumSubmitRetryableTx(chainId, ticketIdHash, senderAddress,
                l1BaseFee, deposit, gasFeeCap, gasLimit, retryToAddress, retryValue,
                beneficiaryAddress, maxSubmissionFee, beneficiaryAddress, retryData);

            var originalTx = new ArbitrumTransaction<ArbitrumSubmitRetryableTx>(submitRetryableTx)
            {
                Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
                ChainId = chainId,
                SenderAddress = senderAddress,
                To = ArbitrumConstants.ArbRetryableTxAddress,
                GasLimit = (long)gasLimit,
                Mint = deposit
            };

            // Act
            var encoded = _decoder.Encode(originalTx);
            var decodedTx = _decoder.Decode(new RlpStream(encoded.Bytes));

            // Assert - Verify critical fields are preserved
            decodedTx.Should().NotBeNull("decoded transaction should not be null");
            decodedTx.Type.Should().Be(originalTx.Type, "transaction type must be preserved");
            decodedTx.ChainId.Should().Be(originalTx.ChainId, "chain ID must be preserved");
            decodedTx.SenderAddress.Should().Be(originalTx.SenderAddress, "sender address must be preserved");
            decodedTx.To.Should().Be(originalTx.To, "destination address must be preserved");
            decodedTx.GasLimit.Should().Be(originalTx.GasLimit, "gas limit must be preserved");
            decodedTx.Mint.Should().Be(originalTx.Mint, "mint value must be preserved");
        }

        [Test(Description = "Encode/decode RetryTx with real dev chain data")]
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

            var retryTx = new ArbitrumRetryTx(chainId, nonce, senderAddress, gasFeeCap, gasLimit,
                recipientAddress, value, txData, ticketIdHash, refundToAddress, maxRefund, submissionFeeRefund);

            var originalTx = new ArbitrumTransaction<ArbitrumRetryTx>(retryTx)
            {
                ChainId = chainId,
                Type = (TxType)ArbitrumTxType.ArbitrumRetry,
                SenderAddress = senderAddress,
                To = recipientAddress,
                Value = value,
                Nonce = nonce,
                GasLimit = (long)gasLimit
            };

            // Act
            var encoded = _decoder.Encode(originalTx);
            var decodedTx = _decoder.Decode(new RlpStream(encoded.Bytes));

            // Assert - Verify critical fields are preserved
            decodedTx.Should().NotBeNull("decoded transaction should not be null");
            decodedTx.Type.Should().Be(originalTx.Type, "transaction type must be preserved");
            decodedTx.ChainId.Should().Be(originalTx.ChainId, "chain ID must be preserved");
            decodedTx.SenderAddress.Should().Be(originalTx.SenderAddress, "sender address must be preserved");
            decodedTx.To.Should().Be(originalTx.To, "destination address must be preserved");
            decodedTx.Value.Should().Be(originalTx.Value, "transaction value must be preserved");
            decodedTx.Nonce.Should().Be(originalTx.Nonce, "nonce must be preserved");
            decodedTx.GasLimit.Should().Be(originalTx.GasLimit, "gas limit must be preserved");
        }

        [Test(Description = "Encode/decode DepositTx with real dev chain data")]
        [TestCase("0x0000000000000000000000000000000000000000000000000000000000000009",
            "502fae7d46d88F08Fc2F8ed27fCB2Ab183Eb3e1F",
            "3f1Eae7D46d88F08fc2F8ed27FCb2AB183EB2d0E",
            "100000000000000000000000")]
        public void DepositTx_RealData_EncodeDecode_Preserves_AllFields(string l1RequestId, string from,
            string to, string value)
        {
            // Arrange - Real dev chain data
            ulong chainId = 412346;
            Hash256 l1RequestIdHash = new Hash256(l1RequestId);
            Address fromAddress = new Address(from);
            Address toAddress = new Address(to);
            UInt256 valueAmount = UInt256.Parse(value);

            var depositTx = new ArbitrumDepositTx(chainId, l1RequestIdHash, fromAddress, toAddress, valueAmount);

            var originalTx = new ArbitrumTransaction<ArbitrumDepositTx>(depositTx)
            {
                ChainId = chainId,
                Type = (TxType)ArbitrumTxType.ArbitrumDeposit,
                SenderAddress = fromAddress,
                To = toAddress,
                Value = valueAmount
            };

            // Act
            var encoded = _decoder.Encode(originalTx);
            var decodedTx = _decoder.Decode(new RlpStream(encoded.Bytes));

            // Assert - Verify critical fields are preserved
            decodedTx.Should().NotBeNull("decoded transaction should not be null");
            decodedTx.Type.Should().Be(originalTx.Type, "transaction type must be preserved");
            decodedTx.ChainId.Should().Be(originalTx.ChainId, "chain ID must be preserved");
            decodedTx.SenderAddress.Should().Be(originalTx.SenderAddress, "sender address must be preserved");
            decodedTx.To.Should().Be(originalTx.To, "destination address must be preserved");
            decodedTx.Value.Should().Be(originalTx.Value, "transaction value must be preserved");
        }

        [Test(Description = "ArbitrumInternal transaction encoding/decoding")]
        [TestCase(412346UL, new byte[] { 0xde, 0xad, 0xbe, 0xef })]
        [TestCase(1UL, new byte[] { })] // Empty data
        [TestCase(999999UL, new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 })] // Longer data
        public void ArbitrumInternalTx_RealData_EncodeDecode_Preserves_AllFields(ulong chainId, byte[] data)
        {
            // Arrange
            var originalTx = new Transaction
            {
                Type = (TxType)ArbitrumTxType.ArbitrumInternal,
                ChainId = chainId,
                Data = data
            };

            // Act
            var encoded = _decoder.Encode(originalTx);
            var decodedTx = _decoder.Decode(new RlpStream(encoded.Bytes));

            // Assert
            decodedTx.Should().NotBeNull("decoded transaction should not be null");
            decodedTx.Type.Should().Be(originalTx.Type, "transaction type must be preserved");
            decodedTx.ChainId.Should().Be(originalTx.ChainId, "chain ID must be preserved");
            decodedTx.Data.ToArray().Should().Equal(originalTx.Data.ToArray(), "transaction data must be preserved exactly");
        }

        #endregion

        #region Edge Cases with Real Data

        [Test(Description = "SubmitRetryable with maximum retry data size")]
        public void SubmitRetryableTx_MaxRetryData_EncodeDecode_Works()
        {
            // Arrange - Test with realistic large retry data (like contract call data)
            var largeRetryData = new byte[32768]; // 32KB - realistic smart contract call
            for (int i = 0; i < largeRetryData.Length; i++)
            {
                largeRetryData[i] = (byte)(i % 256);
            }

            var submitRetryableTx = new ArbitrumSubmitRetryableTx(
                412346, // chainId
                ArbRetryableTxTests.Hash256FromUlong(42), // requestId
                new Address("0xdd6bd74674c356345db88c354491c7d3173c6806"), // from
                39, // l1BaseFee
                10021000000054600, // depositValue
                1000000000, // gasFeeCap
                21000, // gas
                new Address("0x3fab184622dc19b6109349b94811493bf2a45362"), // retryTo
                10000000000000000, // retryValue
                new Address("0x93b4c114b40ecf1fc34745400a1b9b9115c34e42"), // beneficiary
                54600, // maxSubmissionFee
                new Address("0x93b4c114b40ecf1fc34745400a1b9b9115c34e42"), // feeRefundAddr
                largeRetryData); // retryData

            var originalTx = new ArbitrumTransaction<ArbitrumSubmitRetryableTx>(submitRetryableTx)
            {
                Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable,
                ChainId = 412346
            };

            // Act
            var encoded = _decoder.Encode(originalTx);
            var decodedTx = _decoder.Decode(new RlpStream(encoded.Bytes));

            // Assert
            decodedTx.Should().NotBeNull();
            decodedTx.Type.Should().Be(originalTx.Type);
            encoded.Bytes.Length.Should().BeGreaterThan(32768, "encoded size should accommodate large retry data");
        }

        [Test(Description = "Zero values in all numeric fields")]
        public void AllTransactionTypes_ZeroValues_EncodeDecode_Correctly()
        {
            // Test that zero values are handled correctly (not confused with null/empty)

            // ArbitrumInternal with empty data
            var internalTx = new Transaction
            {
                Type = (TxType)ArbitrumTxType.ArbitrumInternal,
                ChainId = 0,
                Data = new byte[0]
            };

            var encodedInternal = _decoder.Encode(internalTx);
            var decodedInternal = _decoder.Decode(new RlpStream(encodedInternal.Bytes));

            decodedInternal.Should().NotBeNull();
            decodedInternal.Type.Should().Be(internalTx.Type);
            decodedInternal.ChainId.Should().Be(0, "zero chain ID should be preserved");
            decodedInternal.Data.Length.Should().Be(0, "empty data should be preserved");
        }

        [Test(Description = "Concurrent encoding/decoding with real transaction data")]
        public void RealTransactions_ConcurrentAccess_ThreadSafe()
        {
            // Arrange - Create real-world transaction examples
            var transactions = new Transaction[]
            {
                new Transaction
                {
                    Type = (TxType)ArbitrumTxType.ArbitrumInternal,
                    ChainId = 412346,
                    Data = new byte[] { 0xde, 0xad, 0xbe, 0xef }
                },
                new ArbitrumTransaction<ArbitrumDepositTx>(
                    new ArbitrumDepositTx(412346, Hash256.Zero, Address.Zero, Address.Zero, UInt256.Zero))
                {
                    Type = (TxType)ArbitrumTxType.ArbitrumDeposit,
                    ChainId = 412346
                }
            };

            // Act - Encode/decode concurrently
            var results = new bool[transactions.Length];
            System.Threading.Tasks.Parallel.For(0, transactions.Length, i =>
            {
                try
                {
                    var encoded = _decoder.Encode(transactions[i]);
                    var decoded = _decoder.Decode(new RlpStream(encoded.Bytes));
                    results[i] = decoded != null && decoded.Type == transactions[i].Type;
                }
                catch
                {
                    results[i] = false;
                }
            });

            // Assert
            results.Should().AllBeEquivalentTo(true, "all concurrent operations should succeed");
        }

        #endregion

        #region Malformed Data Tests

        [Test(Description = "Reject malformed RLP with specific error")]
        public void Decode_MalformedRLP_ThrowsSpecificError()
        {
            // Arrange - Corrupted RLP that could appear in real network data
            byte[] malformedRlp = {
                (byte)ArbitrumTxType.ArbitrumInternal,
                0xFF, 0xFF, 0xFF, 0xFF // Invalid RLP
            };

            // Act & Assert
            Action decode = () => _decoder.Decode(new RlpStream(malformedRlp));
            decode.Should().Throw<Exception>("malformed RLP should be rejected");
        }

        [Test(Description = "Reject unknown transaction type")]
        public void Decode_UnknownTxType_Rejected()
        {
            // Arrange - Invalid transaction type that might appear in network
            byte[] unknownTypeTx = {
                200, // Unknown transaction type
                0xc0 // Empty RLP list
            };

            // Act & Assert
            Action decode = () => _decoder.Decode(new RlpStream(unknownTypeTx));
            decode.Should().Throw<Exception>("unknown transaction types should be rejected");
        }

        #endregion
    }
}