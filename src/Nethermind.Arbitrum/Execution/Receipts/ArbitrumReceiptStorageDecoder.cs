// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Collections;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;
using static Nethermind.Serialization.Rlp.Rlp;

namespace Nethermind.Arbitrum.Execution.Receipts;

/// <summary>
/// RLP decoder for Arbitrum transaction receipts with MultiGas support.
/// </summary>
[Decoder(RlpDecoderKey.Storage)]
public class ArbitrumReceiptStorageDecoder :
    IRlpStreamDecoder<ArbitrumTxReceipt>, IRlpValueDecoder<ArbitrumTxReceipt>, IRlpObjectDecoder<ArbitrumTxReceipt>, IReceiptRefDecoder,
    IRlpStreamDecoder<TxReceipt>, IRlpValueDecoder<TxReceipt>, IRlpObjectDecoder<TxReceipt>
{
    // RefStruct decode does not generate bloom
    public bool CanDecodeBloom => false;

    // TxReceipt interface implementations
    TxReceipt IRlpStreamDecoder<TxReceipt>.Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors) =>
        Decode(rlpStream, rlpBehaviors);

    TxReceipt IRlpValueDecoder<TxReceipt>.Decode(ref ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors) =>
        Decode(ref decoderContext, rlpBehaviors);
    public ArbitrumTxReceipt Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        if (rlpStream.IsNextItemNull())
        {
            rlpStream.ReadByte();
            return null!;
        }

        ArbitrumTxReceipt txReceipt = new();
        int lastCheck = rlpStream.ReadSequenceLength() + rlpStream.Position;

        byte[] firstItem = rlpStream.DecodeByteArray();
        if (firstItem.Length == 1)
            txReceipt.StatusCode = firstItem[0];
        else
            txReceipt.PostTransactionState = firstItem.Length == 0 ? null : new Hash256(firstItem);

        txReceipt.Sender = rlpStream.DecodeAddress();
        txReceipt.GasUsedTotal = (long)rlpStream.DecodeUBigInt();

        int sequenceLength = rlpStream.ReadSequenceLength();
        int logEntriesCheck = sequenceLength + rlpStream.Position;
        using ArrayPoolListRef<LogEntry> logEntries = new(sequenceLength * 2 / LengthOfAddressRlp);

        while (rlpStream.Position < logEntriesCheck)
            logEntries.Add(CompactLogEntryDecoder.Decode(rlpStream, RlpBehaviors.AllowExtraBytes)!);

        txReceipt.Logs = [.. logEntries];

        // Optional fields at end - forward/backward compatible
        if (lastCheck > rlpStream.Position)
        {
            int remainingItems = rlpStream.PeekNumberOfItemsRemaining(lastCheck);

            // GasUsedForL1
            if (remainingItems > 0)
                txReceipt.GasUsedForL1 = rlpStream.DecodeULong();

            // MultiGasUsed (optional)
            if (remainingItems > 1 && !rlpStream.IsNextItemNull())
                txReceipt.MultiGasUsed = MultiGas.Decode(rlpStream);
            else if (remainingItems > 1)
                rlpStream.SkipItem();
        }

        bool allowExtraBytes = (rlpBehaviors & RlpBehaviors.AllowExtraBytes) != 0;
        if (!allowExtraBytes)
            rlpStream.Check(lastCheck);

        txReceipt.Bloom = new Bloom(txReceipt.Logs);

        return txReceipt;
    }

    public ArbitrumTxReceipt Decode(ref ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        if (decoderContext.IsNextItemNull())
        {
            decoderContext.ReadByte();
            return null!;
        }

        ArbitrumTxReceipt txReceipt = new();
        int lastCheck = decoderContext.ReadSequenceLength() + decoderContext.Position;

        byte[] firstItem = decoderContext.DecodeByteArray();
        if (firstItem.Length == 1)
            txReceipt.StatusCode = firstItem[0];
        else
            txReceipt.PostTransactionState = firstItem.Length == 0 ? null : new Hash256(firstItem);

        txReceipt.Sender = decoderContext.DecodeAddress();
        txReceipt.GasUsedTotal = (long)decoderContext.DecodeUBigInt();

        int sequenceLength = decoderContext.ReadSequenceLength();
        int logEntriesCheck = sequenceLength + decoderContext.Position;
        using ArrayPoolListRef<LogEntry> logEntries = new(sequenceLength * 2 / LengthOfAddressRlp);

        while (decoderContext.Position < logEntriesCheck)
            logEntries.Add(CompactLogEntryDecoder.Decode(ref decoderContext, RlpBehaviors.AllowExtraBytes)!);

        txReceipt.Logs = [.. logEntries];

        // Optional fields at end - forward/backward compatible
        if (lastCheck > decoderContext.Position)
        {
            int remainingItems = decoderContext.PeekNumberOfItemsRemaining(lastCheck);

            // GasUsedForL1
            if (remainingItems > 0)
                txReceipt.GasUsedForL1 = decoderContext.DecodeULong();

            // MultiGasUsed (optional)
            if (remainingItems > 1 && !decoderContext.IsNextItemNull())
                txReceipt.MultiGasUsed = MultiGas.Decode(ref decoderContext);
            else if (remainingItems > 1)
                decoderContext.SkipItem();
        }

        bool allowExtraBytes = (rlpBehaviors & RlpBehaviors.AllowExtraBytes) != 0;
        if (!allowExtraBytes)
            decoderContext.Check(lastCheck);

        txReceipt.Bloom = new Bloom(txReceipt.Logs);

        return txReceipt;
    }

    public void DecodeLogEntryStructRef(scoped ref ValueDecoderContext decoderContext, RlpBehaviors none,
        out LogEntryStructRef current)
    {
        CompactLogEntryDecoder.DecodeLogEntryStructRef(ref decoderContext, none, out current);
    }

    public void DecodeStructRef(scoped ref ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors,
        out TxReceiptStructRef item)
    {
        item = new TxReceiptStructRef();

        if (decoderContext.IsNextItemNull())
        {
            decoderContext.ReadByte();
            return;
        }

        int lastCheck = decoderContext.ReadSequenceLength() + decoderContext.Position;

        ReadOnlySpan<byte> firstItem = decoderContext.DecodeByteArraySpan();
        if (firstItem.Length == 1)
            item.StatusCode = firstItem[0];
        else
            item.PostTransactionState = firstItem.Length == 0 ? new Hash256StructRef() : new Hash256StructRef(firstItem);

        decoderContext.DecodeAddressStructRef(out item.Sender);
        item.GasUsedTotal = (long)decoderContext.DecodeUBigInt();

        (int prefixLength, int contentLength) = decoderContext.PeekPrefixAndContentLength();
        int logsBytes = contentLength + prefixLength;
        item.LogsRlp = decoderContext.Data.Slice(decoderContext.Position, logsBytes);
        decoderContext.SkipItem();

        // Skip optional fields (GasUsedForL1, MultiGasUsed)
        if (lastCheck > decoderContext.Position)
        {
            int remainingItems = decoderContext.PeekNumberOfItemsRemaining(lastCheck);
            for (int i = 0; i < remainingItems; i++)
                decoderContext.SkipItem();
        }
    }

    public Hash256[] DecodeTopics(ValueDecoderContext valueDecoderContext)
    {
        return CompactLogEntryDecoder.DecodeTopics(valueDecoderContext);
    }

    public Rlp Encode(ArbitrumTxReceipt? item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        RlpStream rlpStream = new(GetLength(item!, rlpBehaviors));
        Encode(rlpStream, item, rlpBehaviors);
        return new Rlp(rlpStream.Data.ToArray()!);
    }

    public void Encode(RlpStream rlpStream, ArbitrumTxReceipt? item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        if (item is null)
        {
            rlpStream.EncodeNullObject();
            return;
        }

        (int totalContentLength, int logsLength) = GetContentLength(item, rlpBehaviors);
        bool isEip658receipts = (rlpBehaviors & RlpBehaviors.Eip658Receipts) == RlpBehaviors.Eip658Receipts;

        rlpStream.StartSequence(totalContentLength);

        if (isEip658receipts)
            rlpStream.Encode(item.StatusCode);
        else
            rlpStream.Encode(item.PostTransactionState);

        rlpStream.Encode(item.Sender);
        rlpStream.Encode(item.GasUsedTotal);

        rlpStream.StartSequence(logsLength);
        LogEntry[] logs = item.Logs ?? [];
        for (int i = 0; i < logs.Length; i++)
            CompactLogEntryDecoder.Encode(rlpStream, logs[i]);

        // Arbitrum-specific fields
        rlpStream.Encode(item.GasUsedForL1);

        // MultiGasUsed (optional, encode null if not present)
        if (item.MultiGasUsed.HasValue)
            item.MultiGasUsed.Value.Encode(rlpStream);
        else
            rlpStream.EncodeNullObject();
    }

    public void Encode(RlpStream stream, TxReceipt item, RlpBehaviors rlpBehaviors = RlpBehaviors.None) =>
        Encode(stream, (ArbitrumTxReceipt)item, rlpBehaviors);

    public Rlp Encode(TxReceipt? item, RlpBehaviors rlpBehaviors = RlpBehaviors.None) =>
        Encode((ArbitrumTxReceipt?)item, rlpBehaviors);

    public int GetLength(ArbitrumTxReceipt item, RlpBehaviors rlpBehaviors)
    {
        (int Total, _) = GetContentLength(item, rlpBehaviors);
        return LengthOfSequence(Total);
    }

    public int GetLength(TxReceipt item, RlpBehaviors rlpBehaviors) =>
        GetLength((ArbitrumTxReceipt)item, rlpBehaviors);

    private static (int Total, int Logs) GetContentLength(ArbitrumTxReceipt? item, RlpBehaviors rlpBehaviors)
    {
        int contentLength = 0;
        if (item is null)
            return (contentLength, 0);

        bool isEip658Receipts = (rlpBehaviors & RlpBehaviors.Eip658Receipts) == RlpBehaviors.Eip658Receipts;
        if (isEip658Receipts)
            contentLength += LengthOf(item.StatusCode);
        else
            contentLength += LengthOf(item.PostTransactionState);

        contentLength += LengthOf(item.Sender);
        contentLength += LengthOf(item.GasUsedTotal);

        int logsLength = GetLogsLength(item);
        contentLength += LengthOfSequence(logsLength);

        // Arbitrum-specific fields
        contentLength += LengthOf(item.GasUsedForL1);

        // MultiGasUsed (optional)
        if (item.MultiGasUsed.HasValue)
            contentLength += item.MultiGasUsed.Value.GetRlpLength();
        else
            contentLength += 1; // null encoding

        return (contentLength, logsLength);
    }

    private static int GetLogsLength(ArbitrumTxReceipt item)
    {
        int logsLength = 0;
        LogEntry[] logs = item.Logs ?? [];
        for (int i = 0; i < logs.Length; i++)
            logsLength += CompactLogEntryDecoder.Instance.GetLength(logs[i]);
        return logsLength;
    }
}
