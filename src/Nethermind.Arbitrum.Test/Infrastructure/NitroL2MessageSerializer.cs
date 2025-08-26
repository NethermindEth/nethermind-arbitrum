// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Infrastructure;

public static class NitroL2MessageSerializer
{
    public static byte[] SerializeTransactions(IReadOnlyList<Transaction> transactions, L1IncomingMessageHeader header)
    {
        if (transactions.Count == 0)
            throw new ArgumentException("Transactions must be non-empty", nameof(transactions));

        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        switch (header.Kind)
        {
            case ArbitrumL1MessageKind.L2Message:
                SerializeL2Message(writer, transactions, header);
                break;

            case ArbitrumL1MessageKind.L2FundedByL1:
                SerializeL2FundedByL1(writer, transactions, header);
                break;

            case ArbitrumL1MessageKind.SubmitRetryable:
                if (transactions is not [ArbitrumSubmitRetryableTransaction retryable])
                    throw new InvalidOperationException(
                        $"{ArbitrumL1MessageKind.SubmitRetryable} must have exactly one transaction of type {nameof(ArbitrumSubmitRetryableTransaction)}");

                SerializeSubmitRetryable(writer, retryable);
                break;

            case ArbitrumL1MessageKind.EthDeposit:
                if (transactions is not [ArbitrumDepositTransaction deposit])
                    throw new InvalidOperationException(
                        $"{ArbitrumL1MessageKind.EthDeposit} must have exactly one transaction of type {nameof(ArbitrumDepositTransaction)}");

                SerializeEthDeposit(writer, deposit);
                break;

            case ArbitrumL1MessageKind.BatchPostingReport:
                // SerializeBatchPostingReport(writer, transactions[0] as ArbitrumInternalTransaction, header, batchGasCost);
                throw new InvalidOperationException($"{ArbitrumL1MessageKind.BatchPostingReport} is not supported as {nameof(ArbitrumInternalTransaction)} " +
                    $"can't be used to build proper {nameof(DigestMessageParameters)}. It lacks original BatchHash and ExtraGas properties.");

            default:
                throw new ArgumentException($"Unsupported L1 message kind: {header.Kind}");
        }

        return stream.ToArray();
    }

    private static void SerializeL2Message(BinaryWriter writer, IReadOnlyList<Transaction> transactions, L1IncomingMessageHeader header)
    {
        if (transactions.Count > 1)
        {
            SerializeBatch(writer, transactions, header);
            return;
        }

        Transaction transaction = transactions[0];
        if (transaction is ArbitrumUnsignedTransaction unsignedTx)
        {
            writer.Write((byte)ArbitrumL2MessageKind.UnsignedUserTx);
            SerializeUnsignedTx(writer, unsignedTx, ArbitrumL2MessageKind.UnsignedUserTx);
        }
        else if (transaction is ArbitrumContractTransaction contractTx)
        {
            writer.Write((byte)ArbitrumL2MessageKind.ContractTx);
            SerializeUnsignedTx(writer, contractTx, ArbitrumL2MessageKind.ContractTx);
        }
        else if (transaction.Type <= TxType.Blob) // Signed transaction
        {
            writer.Write((byte)ArbitrumL2MessageKind.SignedTx);
            writer.Write(Rlp.Encode(transaction).Bytes);
        }
        else
            throw new ArgumentException($"Unsupported transaction {transaction.GetType()} for {ArbitrumL1MessageKind.L2Message}");
    }

    private static void SerializeBatch(BinaryWriter writer, IReadOnlyList<Transaction> transactions, L1IncomingMessageHeader header)
    {
        writer.Write((byte)ArbitrumL2MessageKind.Batch);

        UInt256 index = UInt256.Zero;
        foreach (Transaction tx in transactions)
        {
            using MemoryStream innerStream = new();
            using BinaryWriter innerWriter = new(innerStream);

            Hash256? subRequestId = null;
            if (header.RequestId != null)
            {
                Span<byte> combined = new byte[64];
                header.RequestId.Bytes.CopyTo(combined[..32]);
                index.ToBigEndian(combined[32..]);
                subRequestId = Keccak.Compute(combined);
            }

            L1IncomingMessageHeader subHeader = header with { Kind = ArbitrumL1MessageKind.L2Message, RequestId = subRequestId };

            SerializeL2Message(innerWriter, [tx], subHeader);

            ArbitrumBinaryWriter.WriteByteString(writer, innerStream.ToArray());
            index.Add(UInt256.One, out index);
        }
    }

    private static void SerializeUnsignedTx(BinaryWriter writer, Transaction tx, ArbitrumL2MessageKind kind)
    {
        ArbitrumBinaryWriter.WriteUInt256(writer, (ulong)tx.GasLimit);
        ArbitrumBinaryWriter.WriteUInt256(writer, tx.MaxFeePerGas);

        if (kind == ArbitrumL2MessageKind.UnsignedUserTx)
            ArbitrumBinaryWriter.WriteBigInteger256(writer, tx.Nonce);

        ArbitrumBinaryWriter.WriteAddressFrom256(writer, tx.To ?? Address.Zero);
        ArbitrumBinaryWriter.WriteUInt256(writer, tx.Value);
        writer.Write(tx.Data.ToArray());
    }

    private static void SerializeL2FundedByL1(BinaryWriter writer, IReadOnlyList<Transaction> transactions, L1IncomingMessageHeader header)
    {
        if (transactions is not [ArbitrumDepositTransaction, { } unsigned])
            throw new ArgumentException($"{ArbitrumL1MessageKind.L2FundedByL1} must have exactly 2 transactions - deposit and unsigned");

        ArbitrumL2MessageKind kind = unsigned switch
        {
            ArbitrumUnsignedTransaction => ArbitrumL2MessageKind.UnsignedUserTx,
            ArbitrumContractTransaction => ArbitrumL2MessageKind.ContractTx,
            _ => throw new InvalidOperationException($"Invalid unsigned transaction {unsigned.GetType()} for {ArbitrumL1MessageKind.L2FundedByL1}")
        };

        writer.Write((byte)kind);
        SerializeUnsignedTx(writer, unsigned, kind);
    }

    private static void SerializeSubmitRetryable(BinaryWriter writer, ArbitrumSubmitRetryableTransaction tx)
    {
        ArbitrumBinaryWriter.WriteAddressFrom256(writer, tx.RetryTo ?? Address.Zero);
        ArbitrumBinaryWriter.WriteUInt256(writer, tx.RetryValue);
        ArbitrumBinaryWriter.WriteUInt256(writer, tx.DepositValue);
        ArbitrumBinaryWriter.WriteUInt256(writer, tx.MaxSubmissionFee);
        ArbitrumBinaryWriter.WriteAddressFrom256(writer, tx.FeeRefundAddr);
        ArbitrumBinaryWriter.WriteAddressFrom256(writer, tx.Beneficiary);
        ArbitrumBinaryWriter.WriteUInt256(writer, (ulong)tx.GasLimit);
        ArbitrumBinaryWriter.WriteUInt256(writer, tx.MaxFeePerGas);
        ArbitrumBinaryWriter.WriteUInt256(writer, (ulong)tx.RetryData.Length);
        writer.Write(tx.RetryData.ToArray());
    }

    private static void SerializeEthDeposit(BinaryWriter writer, ArbitrumDepositTransaction tx)
    {
        ArbitrumBinaryWriter.WriteAddress(writer, tx.To ?? Address.Zero);
        ArbitrumBinaryWriter.WriteUInt256(writer, tx.Value);
    }

    private static void SerializeBatchPostingReport(BinaryWriter writer, ArbitrumInternalTransaction tx, L1IncomingMessageHeader header, ulong? batchGasCost)
    {
        if (tx == null)
            throw new ArgumentException("Transaction must be ArbitrumInternalTransaction");

        if (batchGasCost == null)
            throw new ArgumentException("BatchGasCost is required for BatchPostingReport");

        Dictionary<string, object> decoded = AbiMetadata.UnpackInput(AbiMetadata.BatchPostingReport, tx.Data.ToArray());

        UInt256 batchTimestamp = (UInt256)decoded["batchTimestamp"];
        Address batchPosterAddr = (Address)decoded["batchPosterAddress"];
        ulong batchNum = (ulong)decoded["batchNumber"];
        ulong batchDataGas = (ulong)decoded["batchDataGas"];
        UInt256 l1BaseFee = (UInt256)decoded["l1BaseFeeWei"];

        // TODO: fix when ArbitrumInternalTransaction is updated to include this data
        Hash256 dataHash = Keccak.Zero;
        ulong extraGas = batchDataGas > batchGasCost.Value ? batchDataGas - batchGasCost.Value : 0;

        ArbitrumBinaryWriter.WriteUInt256(writer, batchTimestamp);
        ArbitrumBinaryWriter.WriteAddress(writer, batchPosterAddr);
        ArbitrumBinaryWriter.WriteHash256(writer, dataHash);
        ArbitrumBinaryWriter.WriteUInt256(writer, batchNum);
        ArbitrumBinaryWriter.WriteUInt256(writer, l1BaseFee);

        if (extraGas > 0)
            ArbitrumBinaryWriter.WriteULongBigEndian(writer, extraGas);
    }
}

public static class ArbitrumBinaryWriter
{
    public static void WriteUInt256(BinaryWriter writer, UInt256 value)
    {
        Span<byte> bytes = stackalloc byte[32];
        value.ToBigEndian(bytes);
        writer.Write(bytes);
    }

    public static void WriteBigInteger256(BinaryWriter writer, UInt256 value)
    {
        WriteUInt256(writer, value);
    }

    public static void WriteAddress(BinaryWriter writer, Address address)
    {
        writer.Write(address.Bytes);
    }

    public static void WriteAddressFrom256(BinaryWriter writer, Address address)
    {
        Span<byte> bytes = stackalloc byte[32];
        if (address != Address.Zero)
            address.Bytes.CopyTo(bytes[12..]);
        writer.Write(bytes);
    }

    public static void WriteHash256(BinaryWriter writer, Hash256 hash)
    {
        writer.Write(hash.Bytes);
    }

    public static void WriteByteString(BinaryWriter writer, byte[] data)
    {
        WriteUInt256(writer, (ulong)data.Length);
        writer.Write(data);
    }

    public static void WriteULongBigEndian(BinaryWriter writer, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BitConverter.TryWriteBytes(bytes, value);
        if (BitConverter.IsLittleEndian)
            bytes.Reverse();
        writer.Write(bytes);
    }
}
