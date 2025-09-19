// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Transactions
{
    public abstract class ArbitrumTransaction : Transaction
    {
        /// <summary>
        /// Override for the SpentGas property to allow setting to zero in edge cases
        /// </summary>
        public long? OverrideSpentGas { get; set; }
    }

    public sealed class ArbitrumInternalTransaction : ArbitrumTransaction
    {
        public ArbitrumInternalTransaction()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumInternal;
            //in nitro this is always the arbos address
            To = ArbosAddresses.ArbosAddress;
            //in nitro this is always the arbos address via NewArbitrumSigner
            SenderAddress = ArbosAddresses.ArbosAddress;
        }
    }

    public sealed class ArbitrumDepositTransaction : ArbitrumTransaction
    {
        public Hash256 L1RequestId { get; set; } = Keccak.Zero;

        public ArbitrumDepositTransaction()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumDeposit;
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

        public ArbitrumSubmitRetryableTransaction()
        {
            To = ArbitrumConstants.ArbRetryableTxAddress;
            Type = (TxType)ArbitrumTxType.ArbitrumSubmitRetryable;
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

        public ArbitrumRetryTransaction()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumRetry;
        }
    }

    public sealed class ArbitrumUnsignedTransaction : ArbitrumTransaction
    {
        public UInt256 GasFeeCap { get; set; }
        public ulong Gas { get; set; }

        public ArbitrumUnsignedTransaction()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumUnsigned;
        }
    }

    public sealed class ArbitrumContractTransaction : ArbitrumTransaction
    {
        public Hash256 RequestId { get; set; } = Keccak.Zero;
        public UInt256 GasFeeCap { get; set; }
        public ulong Gas { get; set; }

        public ArbitrumContractTransaction()
        {
            Type = (TxType)ArbitrumTxType.ArbitrumContract;
        }
    }
}
