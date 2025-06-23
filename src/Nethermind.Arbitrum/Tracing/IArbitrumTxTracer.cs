using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public interface IArbitrumTxTracer: ITxTracer
{
    void CaptureArbitrumTransferHook(Address from, Address to, UInt256 value, bool before, string reason);
    void CaptureArbitrumStorageGetHook(UInt256 index, int depth, bool before);
    void CaptureArbitrumStorageSetHook(UInt256 index, Hash256 value, int depth, bool before);
    void CaptureStylusHostioHook(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk, ulong endInk);
}