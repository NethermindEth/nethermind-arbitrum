using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public interface IArbitrumTxTracer : ITxTracer
{
    public void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before);
    public void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before);
    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, BalanceChangeReason reason);

    public void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk,
        ulong endInk);
}
