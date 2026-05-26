// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Tracing;

namespace Nethermind.Arbitrum.Arbos;

public interface IBurner
{
    void Burn(ResourceKind kind, ulong amount);
    void Burn(in MultiGas amount);
    void BurnOut();

    public TracingInfo? TracingInfo { get; }
    ulong Burned { get; }
    MultiGas BurnedMultiGas { get; }
    bool ReadOnly { get; }
    ref ulong GasLeft { get; }
}

public class SystemBurner(TracingInfo? tracingInfo = null, bool readOnly = false) : IBurner
{
    private MultiGas _burnedMultiGas;

    public TracingInfo? TracingInfo { get; } = tracingInfo;
    public ulong Burned => _burnedMultiGas.Total;
    public MultiGas BurnedMultiGas => _burnedMultiGas;
    public bool ReadOnly { get; } = readOnly;
    public ref ulong GasLeft => throw new InvalidOperationException("SystemBurner does not track gas left."); // Strange, but consistent with Nitro.

    public void Burn(ResourceKind kind, ulong amount)
    {
        if (ReadOnly)
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");

        _burnedMultiGas.Increment(kind, amount);
    }

    public void Burn(in MultiGas amount)
    {
        if (ReadOnly)
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");

        _burnedMultiGas.Add(in amount);
    }

    public void BurnOut()
        => throw new InvalidOperationException("SystemBurner does not track gas left and cannot burn out.");

    /// <summary>
    /// Restore BurnedMultiGas to a previously saved value.
    /// Used by owner precompiles which don't charge multigas.
    /// </summary>
    public void RestoreBurnedMultiGas(in MultiGas saved)
        => _burnedMultiGas = saved;
}

public class ZeroGasBurner : IBurner
{
    private ulong _zeroGas = 0;

    public TracingInfo? TracingInfo => null;

    public void Burn(ResourceKind kind, ulong amount)
    {
    }

    public void Burn(in MultiGas amount)
    {
    }

    public void BurnOut()
    {
    }

    public ulong Burned => 0;
    public MultiGas BurnedMultiGas => default;
    public bool ReadOnly => true;
    public ref ulong GasLeft => ref _zeroGas;
}
