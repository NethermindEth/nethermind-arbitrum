// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
public class ArbitrumReceiptStorageDecoder : RlpDecoder<TxReceipt>, IReceiptRefDecoder
{
    protected override TxReceipt DecodeInternal(ref ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        if (decoderContext.IsNextItemEmptyList())
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
            logEntries.Add(CompactLogEntryDecoder.Instance.Decode(ref decoderContext, RlpBehaviors.AllowExtraBytes)!);

        txReceipt.Logs = [.. logEntries];

        // Optional fields at end - forward/backward compatible
        if (lastCheck > decoderContext.Position)
        {
            int remainingItems = decoderContext.PeekNumberOfItemsRemaining(lastCheck);

            // GasUsedForL1
            if (remainingItems > 0)
                txReceipt.GasUsedForL1 = decoderContext.DecodeULong();

            // MultiGasUsed (optional)
            if (remainingItems > 1 && !decoderContext.IsNextItemEmptyList())
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

    public void DecodeStructRef(scoped ref ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors,
        out TxReceiptStructRef item)
    {
        item = new TxReceiptStructRef();

        if (decoderContext.IsNextItemEmptyList())
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

    public void DecodeLogEntryStructRef(scoped ref ValueDecoderContext decoderContext, RlpBehaviors none,
        out LogEntryStructRef current)
    {
        CompactLogEntryDecoder.DecodeLogEntryStructRef(ref decoderContext, none, out current);
    }

    public Hash256[] DecodeTopics(ValueDecoderContext valueDecoderContext)
    {
        return CompactLogEntryDecoder.DecodeTopics(valueDecoderContext);
    }

    // RefStruct decode does not generate bloom
    public bool CanDecodeBloom => false;

    public override void Encode(RlpStream rlpStream, TxReceipt? item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        if (item is null)
        {
            rlpStream.EncodeNullObject();
            return;
        }

        ArbitrumTxReceipt arbitrumItem = (ArbitrumTxReceipt)item;
        (int totalContentLength, int logsLength) = GetContentLength(arbitrumItem, rlpBehaviors);
        bool isEip658receipts = (rlpBehaviors & RlpBehaviors.Eip658Receipts) == RlpBehaviors.Eip658Receipts;

        rlpStream.StartSequence(totalContentLength);

        if (isEip658receipts)
            rlpStream.Encode(arbitrumItem.StatusCode);
        else
            rlpStream.Encode(arbitrumItem.PostTransactionState);

        rlpStream.Encode(arbitrumItem.Sender);
        rlpStream.Encode(arbitrumItem.GasUsedTotal);

        rlpStream.StartSequence(logsLength);
        LogEntry[] logs = arbitrumItem.Logs ?? [];
        for (int i = 0; i < logs.Length; i++)
            CompactLogEntryDecoder.Instance.Encode(rlpStream, logs[i]);

        // Arbitrum-specific fields
        rlpStream.Encode(arbitrumItem.GasUsedForL1);

        // MultiGasUsed (optional, encode null if not present)
        if (arbitrumItem.MultiGasUsed.HasValue)
            arbitrumItem.MultiGasUsed.Value.Encode(rlpStream);
        else
            rlpStream.EncodeNullObject();
    }

    public override int GetLength(TxReceipt item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        (int Total, _) = GetContentLength((ArbitrumTxReceipt)item, rlpBehaviors);
        return LengthOfSequence(Total);
    }

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
