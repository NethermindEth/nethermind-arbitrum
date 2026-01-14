using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

public interface IArbitrumTxTracer : ITxTracer
{
    void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, BalanceChangeReason reason);
    void CaptureArbitrumStorageGet(UInt256 index, int depth, bool before);
    void CaptureArbitrumStorageSet(UInt256 index, ValueHash256 value, int depth, bool before);

    void CaptureStylusHostio(string name, ReadOnlySpan<byte> args, ReadOnlySpan<byte> outs, ulong startInk,
        ulong endInk);

    /// <summary>
    /// Whether this tracer captures per-opcode gas dimension breakdown.
    /// </summary>
    bool IsTracingGasDimension => false;

    /// <summary>
    /// Captures the gas dimension breakdown for a single opcode execution.
    /// Called after each opcode with before/after MultiGas snapshots.
    /// </summary>
    /// <param name="pc">Program counter</param>
    /// <param name="opcode">The executed opcode</param>
    /// <param name="depth">Current call depth</param>
    /// <param name="gasBefore">MultiGas snapshot before opcode execution</param>
    /// <param name="gasAfter">MultiGas snapshot after opcode execution</param>
    /// <param name="gasCost">Single-dimensional gas cost of the opcode</param>
    void CaptureGasDimension(
        int pc,
        Instruction opcode,
        int depth,
        in MultiGas gasBefore,
        in MultiGas gasAfter,
        long gasCost)
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
