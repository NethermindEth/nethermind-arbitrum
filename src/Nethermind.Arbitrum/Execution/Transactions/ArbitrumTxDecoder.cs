// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;
using Nethermind.Serialization.Rlp.TxDecoders;

namespace Nethermind.Arbitrum.Execution.Transactions
{
    public sealed class ArbitrumInternalTxDecoder<T>(Func<T>? transactionFactory = null)
        : BaseEIP1559TxDecoder<T>((TxType)ArbitrumTxType.ArbitrumInternal, transactionFactory)
        where T : Transaction, new()
    {
        public override void Encode(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None, bool forSigning = false, bool isEip155Enabled = false, ulong chainId = 0)
        {
            forSigning = true;
            base.Encode(transaction, stream, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetContentLength(Transaction transaction, RlpBehaviors rlpBehaviors, bool forSigning, bool isEip155Enabled = false, ulong chainId = 0)
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

        protected override void DecodePayload(Transaction transaction, RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            transaction.ChainId = rlpStream.DecodeULong();
            transaction.Data = rlpStream.DecodeByteArray();
        }

        protected override void DecodePayload(Transaction transaction, ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            transaction.ChainId = decoderContext.DecodeULong();
            transaction.Data = decoderContext.DecodeByteArray();
        }
    }

    public sealed class ArbitrumSubmitRetryableTxDecoder<T>(Func<T>? transactionFactory = null)
        : BaseEIP1559TxDecoder<T>((TxType)ArbitrumTxType.ArbitrumSubmitRetryable, transactionFactory)
        where T : Transaction, new()
    {
        public override void Encode(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None, bool forSigning = false, bool isEip155Enabled = false, ulong chainId = 0)
        {
            forSigning = true;
            base.Encode(transaction, stream, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetContentLength(Transaction transaction, RlpBehaviors rlpBehaviors, bool forSigning, bool isEip155Enabled = false, ulong chainId = 0)
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

        protected override void DecodePayload(Transaction transaction, RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ulong chainId = rlpStream.DecodeULong();
            Hash256 requestId = rlpStream.DecodeKeccak()!;
            Address from = rlpStream.DecodeAddress()!;
            UInt256 l1BaseFee = rlpStream.DecodeUInt256();
            UInt256 depositValue = rlpStream.DecodeUInt256();
            UInt256 gasFeeCap = rlpStream.DecodeUInt256();
            ulong gas = rlpStream.DecodeULong();
            Address? retryTo = rlpStream.DecodeAddress();
            UInt256 retryValue = rlpStream.DecodeUInt256();
            Address beneficiary = rlpStream.DecodeAddress()!;
            UInt256 maxSubmissionFee = rlpStream.DecodeUInt256();
            Address feeRefundAddr = rlpStream.DecodeAddress()!;
            byte[] retryData = rlpStream.DecodeByteArray() ?? [];

            transaction.ChainId = chainId;
            transaction.SenderAddress = from;
            transaction.To = ArbitrumConstants.ArbRetryableTxAddress;
            transaction.Value = depositValue;
            transaction.Mint = depositValue;
            transaction.GasLimit = (long)gas;
            transaction.Data = retryData;

<<<<<<< HEAD
<<<<<<< HEAD
=======
            // Set the Inner property if this is an ArbitrumTransaction
>>>>>>> e54760b (Arbitrum Transaction Design changes & decoders fix)
=======
>>>>>>> d63ca9b (Format)
            if (transaction is ArbitrumTransaction<ArbitrumSubmitRetryableTx> arbTx)
            {
                arbTx.Inner = new ArbitrumSubmitRetryableTx(
                    chainId, requestId, from, l1BaseFee, depositValue,
                    gasFeeCap, gas, retryTo, retryValue, beneficiary,
                    maxSubmissionFee, feeRefundAddr, retryData
                );
            }
        }

        protected override void DecodePayload(Transaction transaction, ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ulong chainId = decoderContext.DecodeULong();
            Hash256 requestId = decoderContext.DecodeKeccak()!;
            Address from = decoderContext.DecodeAddress()!;
            UInt256 l1BaseFee = decoderContext.DecodeUInt256();
            UInt256 depositValue = decoderContext.DecodeUInt256();
            UInt256 gasFeeCap = decoderContext.DecodeUInt256();
            ulong gas = decoderContext.DecodeULong();
            Address? retryTo = decoderContext.DecodeAddress();
            UInt256 retryValue = decoderContext.DecodeUInt256();
            Address beneficiary = decoderContext.DecodeAddress()!;
            UInt256 maxSubmissionFee = decoderContext.DecodeUInt256();
            Address feeRefundAddr = decoderContext.DecodeAddress()!;
            byte[] retryData = decoderContext.DecodeByteArray() ?? [];

            transaction.ChainId = chainId;
            transaction.SenderAddress = from;
            transaction.To = ArbitrumConstants.ArbRetryableTxAddress;
            transaction.Value = depositValue;
            transaction.Mint = depositValue;
            transaction.GasLimit = (long)gas;
            transaction.Data = retryData;

<<<<<<< HEAD
<<<<<<< HEAD
=======
            // Set the Inner property if this is an ArbitrumTransaction
>>>>>>> e54760b (Arbitrum Transaction Design changes & decoders fix)
=======
>>>>>>> d63ca9b (Format)
            if (transaction is ArbitrumTransaction<ArbitrumSubmitRetryableTx> arbTx)
            {
                arbTx.Inner = new ArbitrumSubmitRetryableTx(
                    chainId, requestId, from, l1BaseFee, depositValue,
                    gasFeeCap, gas, retryTo, retryValue, beneficiary,
                    maxSubmissionFee, feeRefundAddr, retryData
                );
            }
        }
    }

    public sealed class ArbitrumRetryTxDecoder<T>(Func<T>? transactionFactory = null)
        : BaseEIP1559TxDecoder<T>((TxType)ArbitrumTxType.ArbitrumRetry, transactionFactory)
        where T : Transaction, new()
    {
        public override void Encode(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None, bool forSigning = false, bool isEip155Enabled = false, ulong chainId = 0)
        {
            forSigning = true;
            base.Encode(transaction, stream, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetContentLength(Transaction transaction, RlpBehaviors rlpBehaviors, bool forSigning, bool isEip155Enabled = false, ulong chainId = 0)
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

        protected override void DecodePayload(Transaction transaction, RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ulong chainId = rlpStream.DecodeULong();
            ulong nonce = rlpStream.DecodeULong();
            Address from = rlpStream.DecodeAddress()!;
            UInt256 gasFeeCap = rlpStream.DecodeUInt256();
            ulong gas = rlpStream.DecodeULong();
            Address? to = rlpStream.DecodeAddress();
            UInt256 value = rlpStream.DecodeUInt256();
            byte[] data = rlpStream.DecodeByteArray() ?? [];
            Hash256 ticketId = rlpStream.DecodeKeccak()!;
            Address refundTo = rlpStream.DecodeAddress()!;
            UInt256 maxRefund = rlpStream.DecodeUInt256();
            UInt256 submissionFeeRefund = rlpStream.DecodeUInt256();

            transaction.ChainId = chainId;
            transaction.Nonce = nonce;
            transaction.SenderAddress = from;
            transaction.To = to;
            transaction.Value = value;
            transaction.GasLimit = (long)gas;
            transaction.Data = data;

<<<<<<< HEAD
<<<<<<< HEAD
=======
            // Set the Inner property if this is an ArbitrumTransaction
>>>>>>> e54760b (Arbitrum Transaction Design changes & decoders fix)
=======
>>>>>>> d63ca9b (Format)
            if (transaction is ArbitrumTransaction<ArbitrumRetryTx> arbTx)
            {
                arbTx.Inner = new ArbitrumRetryTx(
                    chainId, nonce, from, gasFeeCap, gas, to, value,
                    data, ticketId, refundTo, maxRefund, submissionFeeRefund
                );
            }
        }

        protected override void DecodePayload(Transaction transaction, ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ulong chainId = decoderContext.DecodeULong();
            ulong nonce = decoderContext.DecodeULong();
            Address from = decoderContext.DecodeAddress()!;
            UInt256 gasFeeCap = decoderContext.DecodeUInt256();
            ulong gas = decoderContext.DecodeULong();
            Address? to = decoderContext.DecodeAddress();
            UInt256 value = decoderContext.DecodeUInt256();
            byte[] data = decoderContext.DecodeByteArray() ?? [];
            Hash256 ticketId = decoderContext.DecodeKeccak()!;
            Address refundTo = decoderContext.DecodeAddress()!;
            UInt256 maxRefund = decoderContext.DecodeUInt256();
            UInt256 submissionFeeRefund = decoderContext.DecodeUInt256();

            transaction.ChainId = chainId;
            transaction.Nonce = nonce;
            transaction.SenderAddress = from;
            transaction.To = to;
            transaction.Value = value;
            transaction.GasLimit = (long)gas;
            transaction.Data = data;

<<<<<<< HEAD
<<<<<<< HEAD
=======
            // Set the Inner property if this is an ArbitrumTransaction
>>>>>>> e54760b (Arbitrum Transaction Design changes & decoders fix)
=======
>>>>>>> d63ca9b (Format)
            if (transaction is ArbitrumTransaction<ArbitrumRetryTx> arbTx)
            {
                arbTx.Inner = new ArbitrumRetryTx(
                    chainId, nonce, from, gasFeeCap, gas, to, value,
                    data, ticketId, refundTo, maxRefund, submissionFeeRefund
                );
            }
        }
    }

    public sealed class ArbitrumDepositTxDecoder<T>(Func<T>? transactionFactory = null)
        : BaseEIP1559TxDecoder<T>((TxType)ArbitrumTxType.ArbitrumDeposit, transactionFactory)
        where T : Transaction, new()
    {
        public override void Encode(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None, bool forSigning = false, bool isEip155Enabled = false, ulong chainId = 0)
        {
            forSigning = true;
            base.Encode(transaction, stream, rlpBehaviors, forSigning, isEip155Enabled, chainId);
        }

        protected override int GetContentLength(Transaction transaction, RlpBehaviors rlpBehaviors, bool forSigning, bool isEip155Enabled = false, ulong chainId = 0)
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

        protected override void DecodePayload(Transaction transaction, RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ulong chainId = rlpStream.DecodeULong();
            Hash256 l1RequestId = rlpStream.DecodeKeccak()!;
            Address from = rlpStream.DecodeAddress()!;
            Address to = rlpStream.DecodeAddress()!;
            UInt256 value = rlpStream.DecodeUInt256();

            transaction.ChainId = chainId;
            transaction.SenderAddress = from;
            transaction.To = to;
            transaction.Value = value;

<<<<<<< HEAD
<<<<<<< HEAD
=======
            // Set the Inner property if this is an ArbitrumTransaction
>>>>>>> e54760b (Arbitrum Transaction Design changes & decoders fix)
=======
>>>>>>> d63ca9b (Format)
            if (transaction is ArbitrumTransaction<ArbitrumDepositTx> arbTx)
            {
                arbTx.Inner = new ArbitrumDepositTx(chainId, l1RequestId, from, to, value);
            }
        }

        protected override void DecodePayload(Transaction transaction, ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            ulong chainId = decoderContext.DecodeULong();
            Hash256 l1RequestId = decoderContext.DecodeKeccak()!;
            Address from = decoderContext.DecodeAddress()!;
            Address to = decoderContext.DecodeAddress()!;
            UInt256 value = decoderContext.DecodeUInt256();

            transaction.ChainId = chainId;
            transaction.SenderAddress = from;
            transaction.To = to;
            transaction.Value = value;

<<<<<<< HEAD
<<<<<<< HEAD
=======
            // Set the Inner property if this is an ArbitrumTransaction
>>>>>>> e54760b (Arbitrum Transaction Design changes & decoders fix)
=======
>>>>>>> d63ca9b (Format)
            if (transaction is ArbitrumTransaction<ArbitrumDepositTx> arbTx)
            {
                arbTx.Inner = new ArbitrumDepositTx(chainId, l1RequestId, from, to, value);
            }
        }
    }
}
