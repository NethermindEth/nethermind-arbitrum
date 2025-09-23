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

    public TracingInfo? TracingInfo { get; } = tracingInfo;

    public void Burn(ulong amount)
    {
        if (ReadOnly)
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");

        if (Out.TraceShowBurn && Out.IsTargetBlock)
            Out.Log($"system burn={amount}");

        _gasBurnt += amount;
    }

    public ulong Burned => _gasBurnt;
    public bool ReadOnly { get; } = readOnly;
    public ref ulong GasLeft => throw new InvalidOperationException("SystemBurner does not track gas left."); // Strange, but consistent with Nitro.
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
