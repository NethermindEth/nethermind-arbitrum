// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;

namespace Nethermind.Arbitrum.Arbos;

public interface IBurner
{
    public TracingInfo? TracingInfo { get; }
    void Burn(ulong amount);
    ulong Burned { get; }
    bool ReadOnly { get; }
    ref ulong GasLeft { get; }
}

public class SystemBurner(TracingInfo? tracingInfo = null, bool readOnly = false) : IBurner
{
    private ulong _gasBurnt;

    public TracingInfo? TracingInfo { get; set; } = tracingInfo;

    public void Burn(ulong amount)
    {
        if (ReadOnly)
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");

        if (Out.TraceShowBurn && Out.IsTargetBlock)
            Out.Log($"system burn={amount}");

        _gasBurnt += amount;
    }

    /// <summary>
    /// Returns the current burned gas and resets the counter to zero.
    /// Used when reusing a burner across transaction processing phases.
    /// </summary>
    public ulong ResetBurned()
    {
        ulong burned = _gasBurnt;
        _gasBurnt = 0;
        return burned;
    }

    public ulong Burned => _gasBurnt;
    public bool ReadOnly { get; } = readOnly;
    public ref ulong GasLeft => throw new InvalidOperationException("SystemBurner does not track gas left."); // Strange, but consistent with Nitro.
}

/// <summary>
/// Thin IBurner wrapper with a swappable inner reference.
/// All ArbosStorage sub-storages sharing this holder automatically
/// delegate to whatever IBurner is currently set, enabling
/// ArbosState reuse across calls with different burners.
/// </summary>
public class BurnerHolder(IBurner initial) : IBurner
{
    public IBurner Current { get; set; } = initial;
    public TracingInfo? TracingInfo => Current.TracingInfo;
    public void Burn(ulong amount) => Current.Burn(amount);
    public ulong Burned => Current.Burned;
    public bool ReadOnly => Current.ReadOnly;
    public ref ulong GasLeft => ref Current.GasLeft;
}

public class ZeroGasBurner : IBurner
{
    private ulong _zeroGas = 0;

    public TracingInfo? TracingInfo => null;
    public void Burn(ulong amount)
    {
    }

    public ulong Burned => 0;
    public bool ReadOnly => true;

    public ref ulong GasLeft => ref _zeroGas;
}
