using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public interface IArbitrumTxTracer : ITxTracer
{
    /// <summary>
    /// Whether this tracer captures per-opcode gas dimension breakdown.
    /// </summary>
    bool IsTracingGasDimension => false;

    void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, BalanceChangeReason reason);
    void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before);
    void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before);

    void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk,
        ulong endInk);

    /// <summary>
    /// Called at the start of opcode execution to capture "before" gas state.
    /// Used by gas policy hooks for stateful dimension capture.
    /// </summary>
    /// <param name="pc">Program counter</param>
    /// <param name="opcode">The opcode being executed</param>
    /// <param name="depth">Current call depth (1-based)</param>
    /// <param name="gasBefore">MultiGas snapshot before opcode execution</param>
    void BeginGasDimensionCapture(int pc, Instruction opcode, int depth, in MultiGas gasBefore)
    { }

    /// <summary>
    /// Called at the end of opcode execution to capture "after" gas state and emit dimension log.
    /// Uses the "before" state captured by BeginGasDimensionCapture.
    /// Scalar gas cost is computed as gasAfter.Total - gasBefore.Total.
    /// </summary>
    /// <param name="gasAfter">MultiGas snapshot after opcode execution</param>
    void EndGasDimensionCapture(in MultiGas gasAfter)
    { }

    /// <summary>
    /// Sets the intrinsic gas for gas dimension logging.
    /// </summary>
    void SetIntrinsicGas(long intrinsicGas) { }

    /// <summary>
    /// Sets the L1 poster gas for gas dimension logging.
    /// </summary>
    void SetPosterGas(ulong posterGas) { }
}
