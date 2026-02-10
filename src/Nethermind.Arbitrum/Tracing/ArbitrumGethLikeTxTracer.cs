// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumTransfer(string purpose, Address? from, Address? to, UInt256 amount)
{
    public string Purpose { get; } = purpose;
    public Address? From { get; } = from;
    public Address? To { get; } = to;
    public UInt256 Value { get; } = amount;
}

public sealed class ArbitrumGethLikeTxTracer : GethLikeTxMemoryTracer, IArbitrumTxTracer
{
    public ArbitrumGethLikeTxTracer(GethTraceOptions options) : base(null, options)
    {
        IsTracingStorage = true;
    }

    public ArbitrumGethLikeTxTracer(Transaction? tx, GethTraceOptions options) : base(tx, options)
    {
        IsTracingStorage = true;
    }
    public List<ArbitrumTransfer> BeforeEvmTransfers { get; } = new();

    public List<ArbitrumTransfer> AfterEvmTransfers { get; } = new();

    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before,
        BalanceChangeReason reason)
    {
        ArbitrumTransfer transfer = new ArbitrumTransfer(reason.ToString(), from, to, value);

        if (before)
            BeforeEvmTransfers.Add(transfer);
        else
            AfterEvmTransfers.Add(transfer);
    }

    public void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before)
    {
    }

    public void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before)
    {
    }

    public void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk,
        ulong endInk)
    {
    }
}
