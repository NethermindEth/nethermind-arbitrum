// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Tracing;

namespace Nethermind.Arbitrum.Arbos;

public interface IBurner
{
    public TracingInfo? TracingInfo { get; }
    void Burn(ulong amount);
    void BurnOut();
    ulong Burned { get; }
    bool ReadOnly { get; }
    ref ulong GasLeft { get; }

    // TODO: Update after ArbOS 60 Mutli-Gas Constrains are implemented
    void Burn(in MultiGas amount)
    {
        Burn(amount.SingleGas());
    }
}

public class SystemBurner(TracingInfo? tracingInfo = null, bool readOnly = false) : IBurner
{
    private ulong _gasBurnt;

    public TracingInfo? TracingInfo { get; } = tracingInfo;

    public void Burn(ulong amount)
    {
        if (ReadOnly)
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");

        _gasBurnt += amount;
    }

    public void BurnOut()
        => throw new InvalidOperationException("SystemBurner does not track gas left and cannot burn out.");

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

    public void BurnOut()
    {
    }

    public ulong Burned => 0;
    public bool ReadOnly => true;

    public ref ulong GasLeft => ref _zeroGas;
}
