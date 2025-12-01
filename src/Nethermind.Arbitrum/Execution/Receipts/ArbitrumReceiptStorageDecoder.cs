// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Evm.Serialization;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Serialization.Rlp;
using static Nethermind.Serialization.Rlp.Rlp;

namespace Nethermind.Arbitrum.Execution.Receipts;

/// <summary>
/// RLP encoder/decoder for Arbitrum receipt storage.
/// Format: [PostStateOrStatus, CumulativeGasUsed, L1GasUsed, Logs, ContractAddress?, MultiGasUsed?]
/// </summary>
[Decoder(RlpDecoderKey.Storage)]
public class ArbitrumReceiptStorageDecoder :
    IRlpStreamDecoder<ArbitrumTxReceipt>, IRlpValueDecoder<ArbitrumTxReceipt>, IRlpObjectDecoder<ArbitrumTxReceipt>,
    IRlpStreamDecoder<TxReceipt>, IRlpValueDecoder<TxReceipt>, IRlpObjectDecoder<TxReceipt>
{
    private readonly MultiGasRlpDecoder _multiGasDecoder = MultiGasRlpDecoder.Instance;

    public ArbitrumTxReceipt Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        if (rlpStream.IsNextItemNull())
        {
            rlpStream.ReadByte();
            return null!;
        }

        ArbitrumTxReceipt txReceipt = new();
        int lastCheck = rlpStream.ReadSequenceLength() + rlpStream.Position;

        // Read PostStateOrStatus (status code as byte[1])
        byte[] firstItem = rlpStream.DecodeByteArray();
        if (firstItem.Length == 1)
            txReceipt.StatusCode = firstItem[0];
        else
            txReceipt.PostTransactionState = firstItem.Length == 0 ? null : new Hash256(firstItem);

        // Read CumulativeGasUsed
        txReceipt.GasUsedTotal = (long)rlpStream.DecodeUBigInt();

        // Read L1GasUsed (Arbitrum-specific)
        txReceipt.GasUsedForL1 = rlpStream.DecodeULong();

        // Read Logs
        int sequenceLength = rlpStream.ReadSequenceLength();
        int logEntriesCheck = sequenceLength + rlpStream.Position;
        List<LogEntry> logEntries = new();

        while (rlpStream.Position < logEntriesCheck) logEntries.Add(Decode<LogEntry>(rlpStream, RlpBehaviors.AllowExtraBytes)!);

        txReceipt.Logs = logEntries.ToArray();

        // Read optional fields if present
        if (lastCheck > rlpStream.Position)
        {
            int remainingItems = rlpStream.PeekNumberOfItemsRemaining(lastCheck);

            // ContractAddress is first optional field
            if (remainingItems > 0) txReceipt.ContractAddress = rlpStream.DecodeAddress();

            // MultiGasUsed is second optional field
            if (remainingItems > 1) txReceipt.MultiGasUsed = _multiGasDecoder.Decode(rlpStream);
        }

        bool allowExtraBytes = (rlpBehaviors & RlpBehaviors.AllowExtraBytes) != 0;
        if (!allowExtraBytes) rlpStream.Check(lastCheck);

        // Recompute bloom from logs
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

        // Read PostStateOrStatus
        byte[] firstItem = decoderContext.DecodeByteArray();
        if (firstItem.Length == 1)
            txReceipt.StatusCode = firstItem[0];
        else
            txReceipt.PostTransactionState = firstItem.Length == 0 ? null : new Hash256(firstItem);

        // Read CumulativeGasUsed
        txReceipt.GasUsedTotal = (long)decoderContext.DecodeUBigInt();

        // Read L1GasUsed
        txReceipt.GasUsedForL1 = decoderContext.DecodeULong();

        // Read Logs
        int sequenceLength = decoderContext.ReadSequenceLength();
        int logEntriesCheck = sequenceLength + decoderContext.Position;
        List<LogEntry> logEntries = new();

        while (decoderContext.Position < logEntriesCheck) logEntries.Add(Decode<LogEntry>(ref decoderContext, RlpBehaviors.AllowExtraBytes)!);

        txReceipt.Logs = logEntries.ToArray();

        // Read optional fields if present
        if (lastCheck > decoderContext.Position)
        {
            int remainingItems = decoderContext.PeekNumberOfItemsRemaining(lastCheck);

            // ContractAddress is first optional field
            if (remainingItems > 0) txReceipt.ContractAddress = decoderContext.DecodeAddress();

            // MultiGasUsed is second optional field
            if (remainingItems > 1) txReceipt.MultiGasUsed = _multiGasDecoder.Decode(ref decoderContext);
        }

        bool allowExtraBytes = (rlpBehaviors & RlpBehaviors.AllowExtraBytes) != 0;
        if (!allowExtraBytes) decoderContext.Check(lastCheck);

        // Recompute bloom from logs
        txReceipt.Bloom = new Bloom(txReceipt.Logs);

        return txReceipt;
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

        // Encode PostStateOrStatus
        if (isEip658receipts)
            rlpStream.Encode(item.StatusCode);
        else
            rlpStream.Encode(item.PostTransactionState);

        // Encode CumulativeGasUsed
        rlpStream.Encode(item.GasUsedTotal);

        // Encode L1GasUsed (Arbitrum-specific)
        rlpStream.Encode(item.GasUsedForL1);

        // Encode Logs
        rlpStream.StartSequence(logsLength);
        LogEntry[] logs = item.Logs ?? [];
        for (int i = 0; i < logs.Length; i++) rlpStream.Encode(logs[i]);

        // Encode optional ContractAddress and MultiGasUsed
        // Following Nitro's logic: write ContractAddress if non-zero,
        // and write MultiGasUsed if non-zero (with zero address placeholder if needed)
        bool wroteAddress = false;
        if (item.ContractAddress is not null && item.ContractAddress != Address.Zero)
        {
            rlpStream.Encode(item.ContractAddress);
            wroteAddress = true;
        }

        if (item.MultiGasUsed != MultiGas.Zero)
        {
            if (!wroteAddress)
                // Write zero address placeholder to occupy ContractAddress slot
                rlpStream.Encode(Address.Zero);
            _multiGasDecoder.Encode(rlpStream, item.MultiGasUsed);
        }
    }

    private static (int Total, int Logs) GetContentLength(ArbitrumTxReceipt? item, RlpBehaviors rlpBehaviors)
    {
        int contentLength = 0;
        if (item is null) return (contentLength, 0);

        bool isEip658Receipts = (rlpBehaviors & RlpBehaviors.Eip658Receipts) == RlpBehaviors.Eip658Receipts;
        if (isEip658Receipts)
            contentLength += LengthOf(item.StatusCode);
        else
            contentLength += LengthOf(item.PostTransactionState);

        contentLength += LengthOf(item.GasUsedTotal);
        contentLength += LengthOf(item.GasUsedForL1);

        int logsLength = GetLogsLength(item);
        contentLength += LengthOfSequence(logsLength);

        // Add length for optional ContractAddress
        bool willWriteAddress = item.ContractAddress is not null && item.ContractAddress != Address.Zero;
        if (willWriteAddress) contentLength += LengthOf(item.ContractAddress);

        // Add length for optional MultiGasUsed (and zero address placeholder if needed)
        if (item.MultiGasUsed != MultiGas.Zero)
        {
            if (!willWriteAddress)
                // Add zero address placeholder length
                contentLength += LengthOf(Address.Zero);
            contentLength += MultiGasRlpDecoder.Instance.GetLength(item.MultiGasUsed);
        }

        return (contentLength, logsLength);
    }

    private static int GetLogsLength(ArbitrumTxReceipt item)
    {
        int logsLength = 0;
        LogEntry[] logs = item.Logs ?? [];
        for (int i = 0; i < logs.Length; i++) logsLength += LengthOf(logs[i]);

        return logsLength;
    }

    public int GetLength(ArbitrumTxReceipt item, RlpBehaviors rlpBehaviors)
    {
        (int Total, _) = GetContentLength(item, rlpBehaviors);
        return LengthOfSequence(Total);
    }

    // TxReceipt interface implementations (cast to ArbitrumTxReceipt)
    TxReceipt IRlpStreamDecoder<TxReceipt>.Decode(RlpStream rlpStream, RlpBehaviors rlpBehaviors)
    {
        return Decode(rlpStream, rlpBehaviors);
    }

    public void Encode(RlpStream stream, TxReceipt item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        Encode(stream, (ArbitrumTxReceipt)item, rlpBehaviors);
    }

    public int GetLength(TxReceipt item, RlpBehaviors rlpBehaviors)
    {
        return GetLength((ArbitrumTxReceipt)item, rlpBehaviors);
    }

    TxReceipt IRlpValueDecoder<TxReceipt>.Decode(ref ValueDecoderContext decoderContext, RlpBehaviors rlpBehaviors)
    {
        return Decode(ref decoderContext, rlpBehaviors);
    }

    public Rlp Encode(TxReceipt? item, RlpBehaviors rlpBehaviors = RlpBehaviors.None)
    {
        return Encode((ArbitrumTxReceipt?)item, rlpBehaviors);
    }
}
