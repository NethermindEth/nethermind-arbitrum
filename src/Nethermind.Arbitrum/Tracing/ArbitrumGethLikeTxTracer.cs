using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumGethLikeTxTracer(GethTraceOptions options) : GethLikeTxTracer(options), IArbitrumTxTracer
{
    public void CaptureArbitrumTransferHook(Address? from, Address? to, UInt256 value, bool before, string reason)
    {
        var transfer = new ArbitrumTransfer
        {
            Purpose = reason,
            Value = value
        };

        if (from != null) transfer.From = from;
        if (to != null) transfer.To = to;

        if (before)
            Trace.BeforeEvmTransfers.Add(transfer);
        else
            Trace.AfterEvmTransfers.Add(transfer);
    }

    public void CaptureArbitrumStorageGetHook(UInt256 index, int depth, bool before)
    {
    }

    public void CaptureArbitrumStorageSetHook(UInt256 index, ValueHash256 value, int depth, bool before)
    {
    }

    public void CaptureStylusHostioHook(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk, ulong endInk)
    {
    }
}
