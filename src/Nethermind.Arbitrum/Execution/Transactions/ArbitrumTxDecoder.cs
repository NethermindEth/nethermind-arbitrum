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
    public sealed class ArbitrumInternalTxDecoder : BaseEIP1559TxDecoder<ArbitrumInternalTransaction>
    {
        public ArbitrumInternalTxDecoder() : base((TxType)ArbitrumTxType.ArbitrumInternal)
        {
        }

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

    public sealed class ArbitrumSubmitRetryableTxDecoder : BaseEIP1559TxDecoder<ArbitrumSubmitRetryableTransaction>
    {
        public ArbitrumSubmitRetryableTxDecoder() : base((TxType)ArbitrumTxType.ArbitrumSubmitRetryable)
        {
        }

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
            var retryableTx = (ArbitrumSubmitRetryableTransaction)transaction;

            return Rlp.LengthOf(transaction.ChainId)
                    + Rlp.LengthOf(retryableTx.RequestId)
                    + Rlp.LengthOf(transaction.SenderAddress)
                    + Rlp.LengthOf(retryableTx.L1BaseFee)
                    + Rlp.LengthOf(retryableTx.DepositValue)
                    + Rlp.LengthOf(retryableTx.GasFeeCap)
                    + Rlp.LengthOf(retryableTx.Gas)
                    + Rlp.LengthOf(retryableTx.RetryTo)
                    + Rlp.LengthOf(retryableTx.RetryValue)
                    + Rlp.LengthOf(retryableTx.Beneficiary)
                    + Rlp.LengthOf(retryableTx.MaxSubmissionFee)
                    + Rlp.LengthOf(retryableTx.FeeRefundAddr)
                    + Rlp.LengthOf(retryableTx.RetryData.Span);
        }

        protected override void EncodePayload(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var retryableTx = (ArbitrumSubmitRetryableTransaction)transaction;

            stream.Encode(transaction.ChainId ?? 0);
            stream.Encode(retryableTx.RequestId);
            stream.Encode(transaction.SenderAddress);
            stream.Encode(retryableTx.L1BaseFee);
            stream.Encode(retryableTx.DepositValue);
            stream.Encode(retryableTx.GasFeeCap);
            stream.Encode(retryableTx.Gas);
            stream.Encode(retryableTx.RetryTo);
            stream.Encode(retryableTx.RetryValue);
            stream.Encode(retryableTx.Beneficiary);
            stream.Encode(retryableTx.MaxSubmissionFee);
            stream.Encode(retryableTx.FeeRefundAddr);
            stream.Encode(retryableTx.RetryData.Span);
        }

        protected override void DecodePayload(Transaction transaction, RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var retryableTx = (ArbitrumSubmitRetryableTransaction)transaction;

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

            // Set base transaction properties
            transaction.ChainId = chainId;
            transaction.SenderAddress = from;
            transaction.To = ArbitrumConstants.ArbRetryableTxAddress;
            transaction.GasLimit = (long)gas;
            transaction.DecodedMaxFeePerGas = gasFeeCap;

            // Set Arbitrum-specific properties
            retryableTx.RequestId = requestId;
            retryableTx.L1BaseFee = l1BaseFee;
            retryableTx.DepositValue = depositValue;
            retryableTx.GasFeeCap = gasFeeCap;
            retryableTx.Gas = gas;
            retryableTx.RetryTo = retryTo;
            retryableTx.RetryValue = retryValue;
            retryableTx.Beneficiary = beneficiary;
            retryableTx.MaxSubmissionFee = maxSubmissionFee;
            retryableTx.FeeRefundAddr = feeRefundAddr;
            retryableTx.RetryData = retryData;
        }

        protected override void DecodePayload(Transaction transaction, ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var retryableTx = (ArbitrumSubmitRetryableTransaction)transaction;

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

            // Set base transaction properties
            transaction.ChainId = chainId;
            transaction.SenderAddress = from;
            transaction.To = ArbitrumConstants.ArbRetryableTxAddress;
            transaction.GasLimit = (long)gas;
            transaction.DecodedMaxFeePerGas = gasFeeCap;

            // Set Arbitrum-specific properties
            retryableTx.RequestId = requestId;
            retryableTx.L1BaseFee = l1BaseFee;
            retryableTx.DepositValue = depositValue;
            retryableTx.GasFeeCap = gasFeeCap;
            retryableTx.Gas = gas;
            retryableTx.RetryTo = retryTo;
            retryableTx.RetryValue = retryValue;
            retryableTx.Beneficiary = beneficiary;
            retryableTx.MaxSubmissionFee = maxSubmissionFee;
            retryableTx.FeeRefundAddr = feeRefundAddr;
            retryableTx.RetryData = retryData;
        }
    }

    public sealed class ArbitrumRetryTxDecoder : BaseEIP1559TxDecoder<ArbitrumRetryTransaction>
    {
        public ArbitrumRetryTxDecoder() : base((TxType)ArbitrumTxType.ArbitrumRetry)
        {
        }

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
            var retryTx = (ArbitrumRetryTransaction)transaction;

            return Rlp.LengthOf(transaction.ChainId)
                   + Rlp.LengthOf(transaction.Nonce)
                   + Rlp.LengthOf(transaction.SenderAddress)
                   + Rlp.LengthOf(retryTx.GasFeeCap)
                   + Rlp.LengthOf(retryTx.Gas)
                   + Rlp.LengthOf(transaction.To)
                   + Rlp.LengthOf(transaction.Value)
                   + Rlp.LengthOf(retryTx.Data.Span)
                   + Rlp.LengthOf(retryTx.TicketId)
                   + Rlp.LengthOf(retryTx.RefundTo)
                   + Rlp.LengthOf(retryTx.MaxRefund)
                   + Rlp.LengthOf(retryTx.SubmissionFeeRefund);
        }

        protected override void EncodePayload(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var retryTx = (ArbitrumRetryTransaction)transaction;

            stream.Encode(transaction.ChainId ?? 0);
            stream.Encode(transaction.Nonce);
            stream.Encode(transaction.SenderAddress);
            stream.Encode(retryTx.GasFeeCap);
            stream.Encode(retryTx.Gas);
            stream.Encode(transaction.To);
            stream.Encode(transaction.Value);
            stream.Encode(retryTx.Data.Span);
            stream.Encode(retryTx.TicketId);
            stream.Encode(retryTx.RefundTo);
            stream.Encode(retryTx.MaxRefund);
            stream.Encode(retryTx.SubmissionFeeRefund);
        }

        protected override void DecodePayload(Transaction transaction, RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var retryTx = (ArbitrumRetryTransaction)transaction;

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

            // Set base transaction properties
            transaction.ChainId = chainId;
            transaction.Nonce = nonce;
            transaction.SenderAddress = from;
            transaction.To = to;
            transaction.Value = value;
            transaction.GasLimit = (long)gas;
            transaction.DecodedMaxFeePerGas = gasFeeCap;

            // Set Arbitrum-specific properties
            retryTx.GasFeeCap = gasFeeCap;
            retryTx.Gas = gas;
            retryTx.Data = data;
            retryTx.TicketId = ticketId;
            retryTx.RefundTo = refundTo;
            retryTx.MaxRefund = maxRefund;
            retryTx.SubmissionFeeRefund = submissionFeeRefund;
        }

        protected override void DecodePayload(Transaction transaction, ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var retryTx = (ArbitrumRetryTransaction)transaction;

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

            // Set base transaction properties
            transaction.ChainId = chainId;
            transaction.Nonce = nonce;
            transaction.SenderAddress = from;
            transaction.To = to;
            transaction.Value = value;
            transaction.GasLimit = (long)gas;
            transaction.DecodedMaxFeePerGas = gasFeeCap;

            // Set Arbitrum-specific properties
            retryTx.GasFeeCap = gasFeeCap;
            retryTx.Gas = gas;
            retryTx.Data = data;
            retryTx.TicketId = ticketId;
            retryTx.RefundTo = refundTo;
            retryTx.MaxRefund = maxRefund;
            retryTx.SubmissionFeeRefund = submissionFeeRefund;
        }
    }

    public sealed class ArbitrumDepositTxDecoder : BaseEIP1559TxDecoder<ArbitrumDepositTransaction>
    {
        public ArbitrumDepositTxDecoder() : base((TxType)ArbitrumTxType.ArbitrumDeposit)
        {
        }

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
            var depositTx = (ArbitrumDepositTransaction)transaction;

            return Rlp.LengthOf(transaction.ChainId)
                   + Rlp.LengthOf(depositTx.L1RequestId)
                   + Rlp.LengthOf(transaction.SenderAddress)
                   + Rlp.LengthOf(transaction.To)
                   + Rlp.LengthOf(transaction.Value);
        }

        protected override void EncodePayload(Transaction transaction, RlpStream stream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var depositTx = (ArbitrumDepositTransaction)transaction;

            stream.Encode(transaction.ChainId ?? 0);
            stream.Encode(depositTx.L1RequestId);
            stream.Encode(transaction.SenderAddress);
            stream.Encode(transaction.To);
            stream.Encode(transaction.Value);
        }

        protected override void DecodePayload(Transaction transaction, RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var depositTx = (ArbitrumDepositTransaction)transaction;

            ulong chainId = rlpStream.DecodeULong();
            Hash256 l1RequestId = rlpStream.DecodeKeccak()!;
            Address from = rlpStream.DecodeAddress()!;
            Address to = rlpStream.DecodeAddress()!;
            UInt256 value = rlpStream.DecodeUInt256();

            // Set basic Transaction properties
            transaction.ChainId = chainId;
            transaction.SenderAddress = from;
            transaction.To = to;
            transaction.Value = value;

            depositTx.L1RequestId = l1RequestId;
        }

        protected override void DecodePayload(Transaction transaction, ref Rlp.ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
        {
            var depositTx = (ArbitrumDepositTransaction)transaction;

            ulong chainId = decoderContext.DecodeULong();
            Hash256 l1RequestId = decoderContext.DecodeKeccak()!;
            Address from = decoderContext.DecodeAddress()!;
            Address to = decoderContext.DecodeAddress()!;
            UInt256 value = decoderContext.DecodeUInt256();

            // Set basic Transaction properties
            transaction.ChainId = chainId;
            transaction.SenderAddress = from;
            transaction.To = to;
            transaction.Value = value;

            depositTx.L1RequestId = l1RequestId;
        }
    }
}
