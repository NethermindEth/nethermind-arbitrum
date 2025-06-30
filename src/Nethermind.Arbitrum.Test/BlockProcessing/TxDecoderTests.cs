using FluentAssertions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Test.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Crypto;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.BlockProcessing
{
    [TestFixture]
    internal class TxDecoderTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            TxDecoder.Instance.RegisterDecoder(new ArbitrumInternalTxDecoder<Transaction>());
            TxDecoder.Instance.RegisterDecoder(new ArbitrumSubmitRetryableTxDecoder<Transaction>());
            TxDecoder.Instance.RegisterDecoder(new ArbitrumRetryTxDecoder<Transaction>());
        }

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
    }
}
