// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Serializes user transactions into L1IncomingMessage format for block production.
/// Inverse of NitroL2MessageParser.ParseTransactions().
/// </summary>
public static class L2MessageAssembler
{
    /// <summary>
    /// Assembles signed user transactions into a MessageWithMetadata suitable for block production.
    /// </summary>
    public static MessageWithMetadata AssembleFromSignedTransactions(Transaction[] transactions, BlockHeader parentHeader, ulong l1BlockNumber)
    {
        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.L2Message,
            ArbosAddresses.BatchPosterAddress,
            l1BlockNumber,
            parentHeader.Timestamp,
            null,
            0);

        byte[] l2Msg = SerializeL2Message(transactions);
        ulong delayedMessagesRead = parentHeader.Nonce;

        L1IncomingMessage message = new(header, l2Msg, null, null);
        return new MessageWithMetadata(message, delayedMessagesRead);
    }

    private static byte[] SerializeL2Message(Transaction[] transactions)
    {
        if (transactions.Length == 1)
            return SerializeSingleSignedTx(transactions[0]);

        return SerializeBatch(transactions);
    }

    private static byte[] SerializeSingleSignedTx(Transaction tx)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write((byte)ArbitrumL2MessageKind.SignedTx);
        writer.Write(Rlp.Encode(tx).Bytes);

        return stream.ToArray();
    }

    private static byte[] SerializeBatch(Transaction[] transactions)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write((byte)ArbitrumL2MessageKind.Batch);

        foreach (Transaction tx in transactions)
        {
            byte[] innerMsg = SerializeSingleSignedTx(tx);
            ArbitrumBinaryWriter.WriteByteString(writer, innerMsg);
        }

        return stream.ToArray();
    }
}
