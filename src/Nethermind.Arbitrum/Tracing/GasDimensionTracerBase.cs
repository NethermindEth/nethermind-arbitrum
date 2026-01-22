// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Arbitrum.Evm;
using Nethermind.Blockchain.Tracing.GethStyle;
using Nethermind.Blockchain.Tracing.GethStyle.Custom.Native;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Tracing;

/// <summary>
/// Base class for gas dimension tracers. Provides shared state management and Begin/End capture pattern.
/// </summary>
public abstract class GasDimensionTracerBase : GethLikeNativeTxTracer, IArbitrumTxTracer
{
    protected readonly Transaction? _transaction;
    protected readonly Block? _block;

    protected ulong _gasUsed;
    protected ulong _intrinsicGas;
    protected ulong _posterGas;
    protected bool _failed;

    // Before-state for gas dimension capture (stored between Begin/End calls)
    private int _beforePc;
    private Instruction _beforeOpcode;
    private int _beforeDepth;
    private MultiGas _beforeGas;

    protected GasDimensionTracerBase(Transaction? transaction, Block? block, GethTraceOptions options)
        : base(options)
    {
        _transaction = transaction;
        _block = block;
        IsTracingActions = true;
    }

    public bool IsTracingGasDimension => true;

    public override void MarkAsSuccess(Address recipient, GasConsumed gasSpent, byte[] output, LogEntry[] logs, Hash256? stateRoot = null)
    {
        base.MarkAsSuccess(recipient, gasSpent, output, logs, stateRoot);
        _gasUsed = (ulong)gasSpent.SpentGas;
        _failed = false;
    }

    public override void MarkAsFailed(Address recipient, GasConsumed gasSpent, byte[] output, string? error, Hash256? stateRoot = null)
    {
        base.MarkAsFailed(recipient, gasSpent, output, error, stateRoot);
        _gasUsed = (ulong)gasSpent.SpentGas;
        _failed = true;
    }

    public void BeginGasDimensionCapture(int pc, Instruction opcode, int depth, in MultiGas gasBefore)
    {
        _beforePc = pc;
        _beforeOpcode = opcode;
        _beforeDepth = depth;
        _beforeGas = gasBefore;
    }

    public void EndGasDimensionCapture(in MultiGas gasAfter)
    {
        long gasCost = (long)(gasAfter.Total - _beforeGas.Total);
        OnGasDimensionCaptured(_beforePc, _beforeOpcode, _beforeDepth, in _beforeGas, in gasAfter, gasCost);
    }

    /// <summary>
    /// Called when a gas dimension capture completes. Derived classes implement this to handle the captured data.
    /// </summary>
    protected abstract void OnGasDimensionCaptured(
        int pc,
        Instruction opcode,
        int depth,
        in MultiGas gasBefore,
        in MultiGas gasAfter,
        long gasCost);

    public void SetIntrinsicGas(long intrinsicGas) => _intrinsicGas = (ulong)intrinsicGas;

    public void SetPosterGas(ulong posterGas) => _posterGas = posterGas;

    public void CaptureArbitrumTransfer(Address? from, Address? to, UInt256 value, bool before, BalanceChangeReason reason)
    {
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

    /// <summary>
    /// Computes L1 and L2 gas usage from total gas used and poster gas.
    /// L1 gas is the poster gas (L1 data posting cost), L2 gas is everything else.
    /// </summary>
    protected (ulong gasUsedForL1, ulong gasUsedForL2) ComputeL1L2Gas()
    {
        ulong gasUsedForL1 = _posterGas;
        ulong gasUsedForL2 = _gasUsed > gasUsedForL1 ? _gasUsed - gasUsedForL1 : 0;
        return (gasUsedForL1, gasUsedForL2);
    }
}
