// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Blockchain.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Execution.Receipts;

public class ArbitrumBlockReceiptTracer(
    ArbitrumTxExecutionContext txExecContext,
    IArbitrumConfig arbitrumConfig) : BlockReceiptsTracer, IArbitrumTxTracer
{
    protected override TxReceipt BuildReceipt(Address recipient, long spentGas, byte statusCode, LogEntry[] logEntries, Hash256? stateRoot)
    {
        Transaction transaction = CurrentTx!;
        ArbitrumTxReceipt txReceipt = new()
        {
            Logs = logEntries,
            TxType = transaction.Type,
            // Bloom calculated in parallel with other receipts
            GasUsedTotal = Block.GasUsed,
            StatusCode = statusCode,
            Recipient = transaction.IsContractCreation ? null : recipient,
            BlockHash = Block.Hash,
            BlockNumber = Block.Number,
            Index = _currentIndex,
            GasUsed = spentGas,
            Sender = transaction.SenderAddress,
            ContractAddress = transaction.IsContractCreation ? recipient : null,
            TxHash = transaction.Hash,
            PostTransactionState = stateRoot,
            GasUsedForL1 = txExecContext.PosterGas, // Arbitrum specific receipt field
            // Multidimensional gas: only populate if config enables it
            MultiGasUsed = arbitrumConfig.ExposeMultiGas ? txExecContext.AccumulatedMultiGas : null
        };

        return txReceipt;
    }

    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, BalanceChangeReason reason)
    {
        IArbitrumTxTracer? arbitrumTxTracer = InnerTracer?.GetTracer<IArbitrumTxTracer>();
        arbitrumTxTracer?.CaptureArbitrumTransfer(from, to, value, before, reason);
    }

    public void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before)
    {
        if (InnerTracer is IArbitrumTxTracer arbitrumTxTracer) arbitrumTxTracer.CaptureArbitrumStorageGet(index, depth, before);
    }

    public void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before)
    {
        if (InnerTracer is IArbitrumTxTracer arbitrumTxTracer) arbitrumTxTracer.CaptureArbitrumStorageSet(index, value, depth, before);
    }

    public void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk, ulong endInk)
    {
        if (InnerTracer is IArbitrumTxTracer arbitrumTxTracer) arbitrumTxTracer.CaptureStylusHostio(name, args, outs, startInk, endInk);
    }

    /// <summary>
    /// Reports change of code for address
    /// </summary>
    /// <param name="address"></param>
    /// <param name="before"></param>
    /// <param name="after"></param>
    /// <remarks>Depends on <see cref="IsTracingState"/></remarks>
    public new void ReportCodeChange(Address address, byte[]? before, byte[]? after)
    {
        base.ReportCodeChange(address, before!, after!);
    }
}
