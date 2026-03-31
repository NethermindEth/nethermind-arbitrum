// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Data.Transactions;
using Nethermind.Arbitrum.Execution.Transactions;

namespace Nethermind.Arbitrum.Sequencer;

/// <summary>
/// Serializes user transactions into L1IncomingMessage format for block production.
/// Inverse of NitroL2MessageParser.ParseTransactions().
/// </summary>
public static class L2MessageAssembler
{
    /// <summary>
    /// Assembles RLP-encoded signed transactions into a MessageWithMetadata suitable for block production.
    /// Analogous to FullSequencingHooks.MessageFromTxes().
    /// </summary>
    public static MessageWithMetadata AssembleFromSignedTransactions(
        byte[][] rlpEncodedTxs, ulong l1BlockNumber, ulong timestamp, ulong delayedMessagesRead)
    {
        L1IncomingMessageHeader header = new(
            ArbitrumL1MessageKind.L2Message,
            ArbosAddresses.BatchPosterAddress,
            l1BlockNumber,
            timestamp,
            null,
            null);

        byte[] l2Msg = SerializeL2Message(rlpEncodedTxs);

        L1IncomingMessage message = new(header, l2Msg, null, null);
        return new MessageWithMetadata(message, delayedMessagesRead);
    }

    private static byte[] SerializeL2Message(byte[][] rlpEncodedTxs)
    {
        if (rlpEncodedTxs.Length == 1)
            return SerializeSingleSignedTx(rlpEncodedTxs[0]);

        return SerializeBatch(rlpEncodedTxs);
    }

    private static byte[] SerializeSingleSignedTx(byte[] rlpBytes)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write((byte)ArbitrumL2MessageKind.SignedTx);
        writer.Write(rlpBytes);

        return stream.ToArray();
    }

    private static byte[] SerializeBatch(byte[][] rlpEncodedTxs)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write((byte)ArbitrumL2MessageKind.Batch);

        for (int i = 0; i < rlpEncodedTxs.Length; i++)
        {
            byte[] innerMsg = SerializeSingleSignedTx(rlpEncodedTxs[i]);
            ArbitrumBinaryWriter.WriteByteString(writer, innerMsg);
        }

        return stream.ToArray();
    }
}
