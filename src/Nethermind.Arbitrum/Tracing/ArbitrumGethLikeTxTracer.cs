using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.GethStyle;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public class ArbitrumTransfer(string purpose, Address? from, Address? to, UInt256 amount)
{
    public string Purpose { get; } = purpose;
    public Address? From { get; } = from;
    public Address? To { get; } = to;
    public UInt256 Value { get; } = amount;

}

public class ArbitrumGethLikeTxTracer(GethTraceOptions options) : GethLikeTxMemoryTracer(null, options), IArbitrumTxTracer
{
    public List<ArbitrumTransfer> BeforeEvmTransfers { get; set; } = new();

    public List<ArbitrumTransfer> AfterEvmTransfers { get; set; } = new();
    
    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before,
        BalanceChangeReason reason)
    {
        var transfer = new ArbitrumTransfer(reason.ToString(), from, to, value);

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
