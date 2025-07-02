using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public interface IArbitrumTxTracer: ITxTracer
{
    void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, string reason);
    void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before);
    void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before);
    void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk, ulong endInk);
}