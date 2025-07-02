using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumTransfer
{
    public string Purpose { get; set; }
    public Address From { get; set; }
    public Address To { get; set; }
    public UInt256 Value { get; set; }

}

public class ArbitrumGethLikeTxTracer(GethTraceOptions options) : GethLikeTxMemoryTracer(null, options), IArbitrumTxTracer
{
    public List<ArbitrumTransfer> BeforeEvmTransfers { get; set; } = new();

    public List<ArbitrumTransfer> AfterEvmTransfers { get; set; } = new();
    
    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, string reason)
    {
        var transfer = new ArbitrumTransfer
        {
            Purpose = reason,
            Value = value
        };

        if (from != null) transfer.From = from;
        if (to != null) transfer.To = to;

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

    public void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk, ulong endInk)
    {
    }
}
