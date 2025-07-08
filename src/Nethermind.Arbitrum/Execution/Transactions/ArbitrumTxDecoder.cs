// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only
using Nethermind.Core;
using Nethermind.Serialization.Rlp;
using Nethermind.Serialization.Rlp.TxDecoders;

namespace Nethermind.Arbitrum.Execution.Transactions
{
    public sealed class ArbitrumInternalTxDecoder<T>(Func<T>? transactionFactory = null)
    : BaseEIP1559TxDecoder<T>((TxType)ArbitrumTxType.ArbitrumInternal, transactionFactory) where T : Transaction, new()
    {
        public override void Encode(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None, bool forSigning = false, bool isEip155Enabled = false, ulong chainId = 0)
        {
            forSigning = true;
            base.Encode(transaction, stream, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetContentLength(Transaction transaction, RlpBehaviors rlpBehaviors, bool forSigning, bool isEip155Enabled = false,
            ulong chainId = 0)
        {
            forSigning = true;
            return base.GetContentLength(transaction, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetPayloadLength(Transaction transaction)
        {
            return Rlp.LengthOf(transaction.ChainId)
                   + Rlp.LengthOf(transaction.Data);
        }

        protected override void EncodePayload(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            stream.Encode(transaction.ChainId ?? 0);
            stream.Encode(transaction.Data);
        }
    }

    public sealed class ArbitrumSubmitRetryableTxDecoder<T>(Func<T>? transactionFactory = null)
        : BaseEIP1559TxDecoder<T>((TxType)ArbitrumTxType.ArbitrumSubmitRetryable, transactionFactory) where T : Transaction, new()
    {
        public override void Encode(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None, bool forSigning = false, bool isEip155Enabled = false, ulong chainId = 0)
        {
            forSigning = true;
            base.Encode(transaction, stream, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }
        protected override int GetContentLength(Transaction transaction, RlpBehaviors rlpBehaviors, bool forSigning, bool isEip155Enabled = false,
            ulong chainId = 0)
        {
            forSigning = true;
            return base.GetContentLength(transaction, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetPayloadLength(Transaction transaction)
        {
            ArbitrumSubmitRetryableTx arbTxn = ((ArbitrumTransaction<ArbitrumSubmitRetryableTx>)transaction).Inner;

            return Rlp.LengthOf(transaction.ChainId)
                    + Rlp.LengthOf(arbTxn.RequestId)
                    + Rlp.LengthOf(transaction.SenderAddress)
                    + Rlp.LengthOf(arbTxn.L1BaseFee)
                    + Rlp.LengthOf(arbTxn.DepositValue)
                    + Rlp.LengthOf(arbTxn.GasFeeCap)
                    + Rlp.LengthOf(arbTxn.Gas)
                    + Rlp.LengthOf(arbTxn.RetryTo)
                    + Rlp.LengthOf(arbTxn.RetryValue)
                    + Rlp.LengthOf(arbTxn.Beneficiary)
                    + Rlp.LengthOf(arbTxn.MaxSubmissionFee)
                    + Rlp.LengthOf(arbTxn.FeeRefundAddr)
                    + Rlp.LengthOf(arbTxn.RetryData.Span);
        }

        protected override void EncodePayload(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ArbitrumSubmitRetryableTx arbTxn = ((ArbitrumTransaction<ArbitrumSubmitRetryableTx>)transaction).Inner;

            stream.Encode(transaction.ChainId ?? 0);
            stream.Encode(arbTxn.RequestId);
            stream.Encode(transaction.SenderAddress);
            stream.Encode(arbTxn.L1BaseFee);
            stream.Encode(arbTxn.DepositValue);
            stream.Encode(arbTxn.GasFeeCap);
            stream.Encode(arbTxn.Gas);
            stream.Encode(arbTxn.RetryTo);
            stream.Encode(arbTxn.RetryValue);
            stream.Encode(arbTxn.Beneficiary);
            stream.Encode(arbTxn.MaxSubmissionFee);
            stream.Encode(arbTxn.FeeRefundAddr);
            stream.Encode(arbTxn.RetryData.Span);
        }
    }

    public sealed class ArbitrumRetryTxDecoder<T>(Func<T>? transactionFactory = null)
    : BaseEIP1559TxDecoder<T>((TxType)ArbitrumTxType.ArbitrumRetry, transactionFactory) where T : Transaction, new()
    {
        public override void Encode(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None, bool forSigning = false, bool isEip155Enabled = false, ulong chainId = 0)
        {
            forSigning = true;
            base.Encode(transaction, stream, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetContentLength(Transaction transaction, RlpBehaviors rlpBehaviors, bool forSigning, bool isEip155Enabled = false,
            ulong chainId = 0)
        {
            forSigning = true;
            return base.GetContentLength(transaction, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetPayloadLength(Transaction transaction)
        {
            ArbitrumRetryTx arbTxn = ((ArbitrumTransaction<ArbitrumRetryTx>)transaction).Inner;

            return Rlp.LengthOf(transaction.ChainId)
                   + Rlp.LengthOf(transaction.Nonce)
                   + Rlp.LengthOf(transaction.SenderAddress)
                   + Rlp.LengthOf(arbTxn.GasFeeCap)
                   + Rlp.LengthOf(arbTxn.Gas)
                   + Rlp.LengthOf(transaction.To)
                   + Rlp.LengthOf(transaction.Value)
                   + Rlp.LengthOf(arbTxn.Data.Span)
                   + Rlp.LengthOf(arbTxn.TicketId)
                   + Rlp.LengthOf(arbTxn.RefundTo)
                   + Rlp.LengthOf(arbTxn.MaxRefund)
                   + Rlp.LengthOf(arbTxn.SubmissionFeeRefund);
        }

        protected override void EncodePayload(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ArbitrumRetryTx arbTxn = ((ArbitrumTransaction<ArbitrumRetryTx>)transaction).Inner;

            stream.Encode(transaction.ChainId ?? 0);
            stream.Encode(transaction.Nonce);
            stream.Encode(transaction.SenderAddress);
            stream.Encode(arbTxn.GasFeeCap);
            stream.Encode(arbTxn.Gas);
            stream.Encode(transaction.To);
            stream.Encode(transaction.Value);
            stream.Encode(arbTxn.Data.Span);
            stream.Encode(arbTxn.TicketId);
            stream.Encode(arbTxn.RefundTo);
            stream.Encode(arbTxn.MaxRefund);
            stream.Encode(arbTxn.SubmissionFeeRefund);
        }
    }

    public sealed class ArbitrumDepositTxDecoder<T>(Func<T>? transactionFactory = null)
    : BaseEIP1559TxDecoder<T>((TxType)ArbitrumTxType.ArbitrumDeposit, transactionFactory) where T : Transaction, new()
    {
        public override void Encode(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None, bool forSigning = false, bool isEip155Enabled = false, ulong chainId = 0)
        {
            forSigning = true;
            base.Encode(transaction, stream, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetContentLength(Transaction transaction, RlpBehaviors rlpBehaviors, bool forSigning, bool isEip155Enabled = false,
            ulong chainId = 0)
        {
            forSigning = true;
            return base.GetContentLength(transaction, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetPayloadLength(Transaction transaction)
        {
            ArbitrumDepositTx arbTxn = ((ArbitrumTransaction<ArbitrumDepositTx>)transaction).Inner;

            return Rlp.LengthOf(transaction.ChainId)
                   + Rlp.LengthOf(arbTxn.L1RequestId)
                   + Rlp.LengthOf(transaction.SenderAddress)
                   + Rlp.LengthOf(transaction.To)
                   + Rlp.LengthOf(transaction.Value);
        }

        protected override void EncodePayload(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ArbitrumDepositTx arbTxn = ((ArbitrumTransaction<ArbitrumDepositTx>)transaction).Inner;

            stream.Encode(transaction.ChainId ?? 0);
            stream.Encode(arbTxn.L1RequestId);
            stream.Encode(transaction.SenderAddress);
            stream.Encode(transaction.To);
            stream.Encode(transaction.Value);
        }
    }

}
