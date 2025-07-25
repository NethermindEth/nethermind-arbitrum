// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Transactions
{
    public abstract class ArbitrumTransaction : Transaction
    {
        protected ArbitrumTransaction()
        {
        }

        protected ArbitrumTransaction(ulong chainId, TxType type)
        {
            ChainId = chainId;
            Type = type;
        }

        protected ArbitrumTransaction(
            ulong chainId, TxType type, Address from, Address? to, UInt256 value,
            UInt256 gasFeeCap, long gasLimit, ulong nonce, ReadOnlyMemory<byte> data)
        {
            if (from is null) throw new ArgumentNullException(nameof(from));

            ChainId = chainId;
            Type = type;
            SenderAddress = from;
            To = to;
            Value = value;
            GasLimit = gasLimit;
            Nonce = nonce;
            Data = data;
            DecodedMaxFeePerGas = gasFeeCap;
        }

        protected ArbitrumTransaction(
            ulong chainId, TxType type, Address from, Address to, UInt256 value,
            UInt256 gasFeeCap, long gasLimit, ReadOnlyMemory<byte> data, UInt256 mint)
        {
            if (from is null) throw new ArgumentNullException(nameof(from));
            if (to is null) throw new ArgumentNullException(nameof(to));

            ChainId = chainId;
            Type = type;
            SenderAddress = from;
            To = to;
            Value = value;
            GasLimit = gasLimit;
            Data = data;
            Mint = mint;
            DecodedMaxFeePerGas = gasFeeCap;
        }
    }

    public sealed class ArbitrumInternalTransaction : ArbitrumTransaction
    {
        public ArbitrumInternalTransaction() : base()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumInternal;
        }

        public ArbitrumInternalTransaction(ulong chainId, ReadOnlyMemory<byte> data)
            : base(chainId, (TxType)ArbitrumTxType.ArbitrumInternal)
        {
            Data = data;
        }
    }

    public sealed class ArbitrumDepositTransaction : ArbitrumTransaction
    {
        public Hash256 L1RequestId { get; set; } = Keccak.Zero;

        public ArbitrumDepositTransaction() : base()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumDeposit;
        }

        public ArbitrumDepositTransaction(ulong chainId, Hash256 l1RequestId, Address from, Address to, UInt256 value)
            : base(chainId, (TxType)ArbitrumTxType.ArbitrumDeposit)
        {
            if (l1RequestId is null) throw new ArgumentNullException(nameof(l1RequestId));
            if (from is null) throw new ArgumentNullException(nameof(from));
            if (to is null) throw new ArgumentNullException(nameof(to));

            L1RequestId = l1RequestId;
            SenderAddress = from;
            To = to;
            Value = value;
        }
    }

    public sealed class ArbitrumSubmitRetryableTransaction : ArbitrumTransaction
    {
        public Hash256 RequestId { get; set; } = Keccak.Zero;
        public UInt256 L1BaseFee { get; set; }
        public UInt256 DepositValue { get; set; }
        public UInt256 GasFeeCap { get; set; }
        public ulong Gas { get; set; }
        public Address? RetryTo { get; set; }
        public UInt256 RetryValue { get; set; }
        public Address Beneficiary { get; set; } = Address.Zero;
        public UInt256 MaxSubmissionFee { get; set; }
        public Address FeeRefundAddr { get; set; } = Address.Zero;
        public ReadOnlyMemory<byte> RetryData { get; set; }

        public ArbitrumSubmitRetryableTransaction() : base()
        {
            To = ArbitrumConstants.ArbRetryableTxAddress;
            Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable;
        }

        public ArbitrumSubmitRetryableTransaction(
            ulong chainId, Hash256 requestId, Address from, UInt256 l1BaseFee,
            UInt256 depositValue, UInt256 gasFeeCap, ulong gas, Address? retryTo,
            UInt256 retryValue, Address beneficiary, UInt256 maxSubmissionFee,
            Address feeRefundAddr, ReadOnlyMemory<byte> retryData)
            : base(chainId, (TxType)ArbitrumTxType.ArbitrumSubmitRetryable, from,
                   ArbitrumConstants.ArbRetryableTxAddress, depositValue, gasFeeCap, (long)gas, 0, retryData)
        {
            if (requestId is null) throw new ArgumentNullException(nameof(requestId));
            if (beneficiary is null) throw new ArgumentNullException(nameof(beneficiary));
            if (feeRefundAddr is null) throw new ArgumentNullException(nameof(feeRefundAddr));

            RequestId = requestId;
            L1BaseFee = l1BaseFee;
            DepositValue = depositValue;
            GasFeeCap = gasFeeCap;
            Gas = gas;
            RetryTo = retryTo;
            RetryValue = retryValue;
            Beneficiary = beneficiary;
            MaxSubmissionFee = maxSubmissionFee;
            FeeRefundAddr = feeRefundAddr;
            RetryData = retryData;
            Mint = depositValue;
        }
    }

    public sealed class ArbitrumRetryTransaction : ArbitrumTransaction
    {
        public UInt256 GasFeeCap { get; set; }
        public ulong Gas { get; set; }
        public Hash256 TicketId { get; set; } = Keccak.Zero;
        public Address RefundTo { get; set; } = Address.Zero;
        public UInt256 MaxRefund { get; set; }
        public UInt256 SubmissionFeeRefund { get; set; }

        public ArbitrumRetryTransaction() : base()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumRetry;
        }

        public ArbitrumRetryTransaction(
            ulong chainId, ulong nonce, Address from, UInt256 gasFeeCap,
            ulong gas, Address? to, UInt256 value, ReadOnlyMemory<byte> data,
            Hash256 ticketId, Address refundTo, UInt256 maxRefund, UInt256 submissionFeeRefund)
            : base(chainId, (TxType)ArbitrumTxType.ArbitrumRetry, from, to, value, gasFeeCap, (long)gas, nonce, data)
        {
            if (ticketId is null) throw new ArgumentNullException(nameof(ticketId));
            if (refundTo is null) throw new ArgumentNullException(nameof(refundTo));

            GasFeeCap = gasFeeCap;
            Gas = gas;
            TicketId = ticketId;
            RefundTo = refundTo;
            MaxRefund = maxRefund;
            SubmissionFeeRefund = submissionFeeRefund;
        }
    }

    public sealed class ArbitrumUnsignedTransaction : ArbitrumTransaction
    {
        public UInt256 GasFeeCap { get; set; }
        public ulong Gas { get; set; }

        public ArbitrumUnsignedTransaction() : base()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumUnsigned;
        }

        public ArbitrumUnsignedTransaction(
            ulong chainId, Address from, UInt256 nonce, UInt256 gasFeeCap,
            ulong gas, Address? to, UInt256 value, ReadOnlyMemory<byte> data)
            : base(chainId, (TxType)ArbitrumTxType.ArbitrumUnsigned, from, to, value, gasFeeCap, (long)gas, (ulong)nonce, data)
        {
            GasFeeCap = gasFeeCap;
            Gas = gas;
        }
    }

    public sealed class ArbitrumContractTransaction : ArbitrumTransaction
    {
        public Hash256 RequestId { get; set; } = Keccak.Zero;
        public UInt256 GasFeeCap { get; set; }
        public ulong Gas { get; set; }

        public ArbitrumContractTransaction() : base()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumContract;
        }

        public ArbitrumContractTransaction(
            ulong chainId, Hash256 requestId, Address from, UInt256 gasFeeCap,
            ulong gas, Address? to, UInt256 value, ReadOnlyMemory<byte> data)
            : base(chainId, (TxType)ArbitrumTxType.ArbitrumContract, from, to, value, gasFeeCap, (long)gas, 0, data)
        {
            if (requestId is null) throw new ArgumentNullException(nameof(requestId));

            RequestId = requestId;
            GasFeeCap = gasFeeCap;
            Gas = gas;
        }
    }
}
