// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class TestL1IncomingMessage
{
    public static L1IncomingMessage CreateEthDepositMessage(
        Hash256 requestId, UInt256 l1BaseFee, Address sender, Address receiver, UInt256 value)
    {
        ArbitrumDepositTransaction deposit = new()
        {
            SourceHash = requestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = UInt256.Zero,
            GasLimit = 0,
            IsOPSystemTransaction = false,
            Mint = value,
            ChainId = 412346,
            L1RequestId = requestId,
            Value = value,
            SenderAddress = sender,
            To = receiver
        };

        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.EthDeposit,
            sender,
            1,
            (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            requestId,
            l1BaseFee);

        byte[] l2Msg = NitroL2MessageSerializer.SerializeTransactions([deposit], header);

        return new L1IncomingMessage(header, l2Msg, null, null);
    }

    public static L1IncomingMessage CreateSubmitRetryableMessage(
        Hash256 requestId, UInt256 l1BaseFee, Address sender, Address receiver, Address beneficiary,
        UInt256 depositValue, UInt256 retryValue, UInt256 gasFee, ulong gasLimit, UInt256 maxSubmissionFee)
    {
        ArbitrumSubmitRetryableTransaction retryable = new()
        {
            SourceHash = requestId,
            Nonce = UInt256.Zero,
            GasPrice = UInt256.Zero,
            DecodedMaxFeePerGas = gasFee,
            GasLimit = (long)gasLimit,
            Value = 0,
            Data = Array.Empty<byte>(),
            IsOPSystemTransaction = false,
            Mint = depositValue,
            ChainId = 412346,
            RequestId = requestId,
            SenderAddress = sender,
            L1BaseFee = l1BaseFee,
            DepositValue = depositValue,
            GasFeeCap = gasFee,
            Gas = gasLimit,
            RetryTo = receiver,
            RetryValue = retryValue,
            Beneficiary = beneficiary,
            MaxSubmissionFee = maxSubmissionFee,
            FeeRefundAddr = beneficiary,
            RetryData = Array.Empty<byte>()
        };

        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.SubmitRetryable,
            sender,
            1,
            (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            requestId,
            l1BaseFee);

        byte[] l2Msg = NitroL2MessageSerializer.SerializeTransactions([retryable], header);

        return new L1IncomingMessage(header, l2Msg, null, null);
    }
}
