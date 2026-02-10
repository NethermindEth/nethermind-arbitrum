// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Text.Json.Serialization;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Facade.Eth;
using Nethermind.Facade.Eth.RpcTransaction;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Modules;

public class ArbitrumInternalTransactionForRpc : TransactionForRpc, IFromTransaction<ArbitrumInternalTransactionForRpc>
{
    public static TxType TxType => (TxType)ArbitrumTxType.ArbitrumInternal;

    public override TxType? Type => TxType;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? ChainId { get; set; }

    [JsonIgnore]
    public Address From { get; set; } = null!;

    [JsonIgnore]
    public Address? To { get; set; }

    public byte[] Input { get; set; } = Array.Empty<byte>();

    [JsonConstructor]
    public ArbitrumInternalTransactionForRpc() { }

    public ArbitrumInternalTransactionForRpc(Transaction transaction, in TransactionForRpcContext extraData)
        : base(transaction, extraData)
    {
        ChainId = extraData.ChainId ?? transaction.ChainId;
        From = transaction.SenderAddress ?? Address.Zero;
        To = transaction.To;
        Input = transaction.Data.ToArray();
    }

    public override Result<Transaction> ToTransaction(bool validateUserInput = false)
    {
        return new ArbitrumInternalTransaction
        {
            Type = TxType,
            ChainId = ChainId,
            Data = Input
        };
    }

    public static ArbitrumInternalTransactionForRpc FromTransaction(Transaction tx, in TransactionForRpcContext extraData)
        => new(tx, extraData);

    public override void EnsureDefaults(long? gasCap) { }

    public override bool ShouldSetBaseFee() => false;
}

public class ArbitrumDepositTransactionForRpc : TransactionForRpc, IFromTransaction<ArbitrumDepositTransactionForRpc>
{
    public static TxType TxType => (TxType)ArbitrumTxType.ArbitrumDeposit;

    public override TxType? Type => TxType;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Hash256? RequestId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? ChainId { get; set; }

    public Address From { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? To { get; set; }

    public UInt256 Value { get; set; }

    [JsonConstructor]
    public ArbitrumDepositTransactionForRpc() { }

    public ArbitrumDepositTransactionForRpc(Transaction transaction, in TransactionForRpcContext extraData)
        : base(transaction, extraData)
    {
        ArbitrumDepositTransaction? depositTx = transaction as ArbitrumDepositTransaction;
        RequestId = depositTx?.L1RequestId;
        ChainId = extraData.ChainId ?? transaction.ChainId;
        From = transaction.SenderAddress ?? Address.Zero;
        To = transaction.To;
        Value = transaction.Value;
    }

    public override Result<Transaction> ToTransaction(bool validateUserInput = false)
    {
        return new ArbitrumDepositTransaction
        {
            Type = TxType,
            ChainId = ChainId,
            L1RequestId = RequestId ?? Hash256.Zero,
            SenderAddress = From,
            To = To,
            Value = Value
        };
    }

    public static ArbitrumDepositTransactionForRpc FromTransaction(Transaction tx, in TransactionForRpcContext extraData)
        => new(tx, extraData);

    public override void EnsureDefaults(long? gasCap) { }

    public override bool ShouldSetBaseFee() => false;
}

public class ArbitrumUnsignedTransactionForRpc : TransactionForRpc, IFromTransaction<ArbitrumUnsignedTransactionForRpc>, ITxTyped
{
    public static TxType TxType => (TxType)ArbitrumTxType.ArbitrumUnsigned;

    public override TxType? Type => TxType;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? ChainId { get; set; }

    public Address From { get; set; } = null!;

    public UInt256 Nonce { get; set; }

    [JsonPropertyName("maxFeePerGas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? GasFeeCap { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? To { get; set; }

    public UInt256 Value { get; set; }

    public byte[] Input { get; set; } = [];

    [JsonConstructor]
    public ArbitrumUnsignedTransactionForRpc() { }

    public ArbitrumUnsignedTransactionForRpc(Transaction transaction, in TransactionForRpcContext extraData)
        : base(transaction, extraData)
    {
        ArbitrumUnsignedTransaction? unsignedTx = transaction as ArbitrumUnsignedTransaction;
        ChainId = extraData.ChainId ?? transaction.ChainId;
        From = transaction.SenderAddress ?? Address.Zero;
        Nonce = transaction.Nonce;
        GasFeeCap = unsignedTx?.GasFeeCap ?? transaction.MaxFeePerGas;
        Gas = (long?)unsignedTx?.Gas ?? transaction.GasLimit;
        To = transaction.To;
        Value = transaction.Value;
        Input = transaction.Data.ToArray();
    }

    public override Result<Transaction> ToTransaction(bool validateUserInput = false)
    {
        return new ArbitrumUnsignedTransaction
        {
            Type = TxType,
            ChainId = ChainId,
            SenderAddress = From,
            Nonce = Nonce,
            GasFeeCap = GasFeeCap ?? UInt256.Zero,
            Gas = (ulong)(Gas ?? 0),
            GasLimit = Gas ?? 0,
            DecodedMaxFeePerGas = GasFeeCap ?? UInt256.Zero,
            To = To,
            Value = Value,
            Data = Input
        };
    }

    public static ArbitrumUnsignedTransactionForRpc FromTransaction(Transaction tx, in TransactionForRpcContext extraData)
        => new(tx, extraData);

    public override void EnsureDefaults(long? gasCap) { }

    public override bool ShouldSetBaseFee() => false;
}

public class ArbitrumRetryTransactionForRpc : TransactionForRpc, IFromTransaction<ArbitrumRetryTransactionForRpc>, ITxTyped
{
    public static TxType TxType => (TxType)ArbitrumTxType.ArbitrumRetry;

    public override TxType? Type => TxType;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? ChainId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Hash256? TicketId { get; set; }

    public Address From { get; set; } = null!;

    public UInt256 Nonce { get; set; }

    [JsonPropertyName("maxFeePerGas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? GasFeeCap { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? To { get; set; }

    public UInt256 Value { get; set; }

    public byte[] Input { get; set; } = Array.Empty<byte>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? RefundTo { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? MaxRefund { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? SubmissionFeeRefund { get; set; }

    [JsonConstructor]
    public ArbitrumRetryTransactionForRpc() { }

    public ArbitrumRetryTransactionForRpc(Transaction transaction, in TransactionForRpcContext extraData)
        : base(transaction, extraData)
    {
        ArbitrumRetryTransaction? retryTx = transaction as ArbitrumRetryTransaction;
        ChainId = extraData.ChainId ?? transaction.ChainId;
        TicketId = retryTx?.TicketId;
        From = transaction.SenderAddress ?? Address.Zero;
        Nonce = transaction.Nonce;
        GasFeeCap = retryTx?.GasFeeCap ?? transaction.MaxFeePerGas;
        Gas = (long?)retryTx?.Gas ?? transaction.GasLimit;
        To = transaction.To;
        Value = transaction.Value;
        Input = transaction.Data.ToArray();
        RefundTo = retryTx?.RefundTo;
        MaxRefund = retryTx?.MaxRefund;
        SubmissionFeeRefund = retryTx?.SubmissionFeeRefund;
    }

    public override Result<Transaction> ToTransaction(bool validateUserInput = false)
    {
        return new ArbitrumRetryTransaction
        {
            Type = TxType,
            ChainId = ChainId,
            TicketId = TicketId ?? Hash256.Zero,
            SenderAddress = From,
            Nonce = Nonce,
            GasFeeCap = GasFeeCap ?? UInt256.Zero,
            Gas = (ulong)(Gas ?? 0),
            GasLimit = Gas ?? 0,
            DecodedMaxFeePerGas = GasFeeCap ?? UInt256.Zero,
            To = To,
            Value = Value,
            Data = Input,
            RefundTo = RefundTo ?? Address.Zero,
            MaxRefund = MaxRefund ?? UInt256.Zero,
            SubmissionFeeRefund = SubmissionFeeRefund ?? UInt256.Zero
        };
    }

    public static ArbitrumRetryTransactionForRpc FromTransaction(Transaction tx, in TransactionForRpcContext extraData)
        => new(tx, extraData);

    public override void EnsureDefaults(long? gasCap) { }

    public override bool ShouldSetBaseFee() => false;
}

public class ArbitrumSubmitRetryableTransactionForRpc : TransactionForRpc, IFromTransaction<ArbitrumSubmitRetryableTransactionForRpc>, ITxTyped
{
    public static TxType TxType => (TxType)ArbitrumTxType.ArbitrumSubmitRetryable;

    public override TxType? Type => TxType;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? ChainId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Hash256? RequestId { get; set; }

    public Address From { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? L1BaseFee { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? DepositValue { get; set; }

    [JsonPropertyName("maxFeePerGas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? GasFeeCap { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? To { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? RetryTo { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? RetryValue { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? Beneficiary { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? MaxSubmissionFee { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? RefundTo { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? RetryData { get; set; }

    public byte[] Input { get; set; } = [];

    [JsonConstructor]
    public ArbitrumSubmitRetryableTransactionForRpc() { }

    public ArbitrumSubmitRetryableTransactionForRpc(Transaction transaction, in TransactionForRpcContext extraData)
        : base(transaction, extraData)
    {
        ArbitrumSubmitRetryableTransaction? retryableTx = transaction as ArbitrumSubmitRetryableTransaction;
        ChainId = extraData.ChainId ?? transaction.ChainId;
        RequestId = retryableTx?.RequestId;
        From = transaction.SenderAddress ?? Address.Zero;
        L1BaseFee = retryableTx?.L1BaseFee;
        DepositValue = retryableTx?.DepositValue;
        GasFeeCap = retryableTx?.GasFeeCap ?? transaction.MaxFeePerGas;
        Gas = (long?)retryableTx?.Gas ?? transaction.GasLimit;
        To = transaction.To;
        RetryTo = retryableTx?.RetryTo;
        RetryValue = retryableTx?.RetryValue;
        Beneficiary = retryableTx?.Beneficiary;
        MaxSubmissionFee = retryableTx?.MaxSubmissionFee;
        RefundTo = retryableTx?.FeeRefundAddr;
        RetryData = retryableTx?.RetryData.ToArray();
        Input = transaction.Data.ToArray();
    }

    public override Result<Transaction> ToTransaction(bool validateUserInput = false)
    {
        return new ArbitrumSubmitRetryableTransaction
        {
            Type = TxType,
            ChainId = ChainId,
            RequestId = RequestId ?? Hash256.Zero,
            SenderAddress = From,
            L1BaseFee = L1BaseFee ?? UInt256.Zero,
            DepositValue = DepositValue ?? UInt256.Zero,
            GasFeeCap = GasFeeCap ?? UInt256.Zero,
            Gas = (ulong)(Gas ?? 0),
            GasLimit = Gas ?? 0,
            DecodedMaxFeePerGas = GasFeeCap ?? UInt256.Zero,
            To = To,
            RetryTo = RetryTo,
            RetryValue = RetryValue ?? UInt256.Zero,
            Beneficiary = Beneficiary ?? Address.Zero,
            MaxSubmissionFee = MaxSubmissionFee ?? UInt256.Zero,
            FeeRefundAddr = RefundTo ?? Address.Zero,
            RetryData = RetryData ?? []
        };
    }

    public static ArbitrumSubmitRetryableTransactionForRpc FromTransaction(Transaction tx, in TransactionForRpcContext extraData)
        => new(tx, extraData);

    public override void EnsureDefaults(long? gasCap) { }

    public override bool ShouldSetBaseFee() => false;
}

public class ArbitrumContractTransactionForRpc : TransactionForRpc, IFromTransaction<ArbitrumContractTransactionForRpc>, ITxTyped
{
    public static TxType TxType => (TxType)ArbitrumTxType.ArbitrumContract;

    public override TxType? Type => TxType;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ulong? ChainId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Hash256? RequestId { get; set; }

    public Address From { get; set; } = null!;

    [JsonPropertyName("maxFeePerGas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UInt256? GasFeeCap { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Address? To { get; set; }

    public UInt256 Value { get; set; }

    public byte[] Input { get; set; } = [];

    [JsonConstructor]
    public ArbitrumContractTransactionForRpc() { }

    public ArbitrumContractTransactionForRpc(Transaction transaction, in TransactionForRpcContext extraData)
        : base(transaction, extraData)
    {
        ArbitrumContractTransaction? contractTx = transaction as ArbitrumContractTransaction;
        ChainId = extraData.ChainId ?? transaction.ChainId;
        RequestId = contractTx?.RequestId;
        From = transaction.SenderAddress ?? Address.Zero;
        GasFeeCap = contractTx?.GasFeeCap ?? transaction.MaxFeePerGas;
        Gas = (long?)contractTx?.Gas ?? transaction.GasLimit;
        To = transaction.To;
        Value = transaction.Value;
        Input = transaction.Data.ToArray();
    }

    public override Result<Transaction> ToTransaction(bool validateUserInput = false)
    {
        return new ArbitrumContractTransaction
        {
            Type = TxType,
            ChainId = ChainId,
            RequestId = RequestId ?? Hash256.Zero,
            SenderAddress = From,
            GasFeeCap = GasFeeCap ?? UInt256.Zero,
            Gas = (ulong)(Gas ?? 0),
            GasLimit = Gas ?? 0,
            DecodedMaxFeePerGas = GasFeeCap ?? UInt256.Zero,
            To = To,
            Value = Value,
            Data = Input
        };
    }

    public static ArbitrumContractTransactionForRpc FromTransaction(Transaction tx, in TransactionForRpcContext extraData)
        => new(tx, extraData);

    public override void EnsureDefaults(long? gasCap) { }

    public override bool ShouldSetBaseFee() => false;
}
