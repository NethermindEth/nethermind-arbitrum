// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Tracing;

namespace Nethermind.Arbitrum.Arbos;

public interface IBurner
{
    public TracingInfo? TracingInfo { get; }
    void Burn(ulong amount);
    void Burn(ResourceKind kind, ulong amount);
    ulong Burned { get; }
    MultiGas BurnedMultiGas { get; }
    bool ReadOnly { get; }
    ref ulong GasLeft { get; }
}

public class SystemBurner(TracingInfo? tracingInfo = null, bool readOnly = false) : IBurner
{
    private ulong _gasBurnt;
    private MultiGas _burnedMultiGas;

    public TracingInfo? TracingInfo { get; } = tracingInfo;

    public void Burn(ulong amount)
    {
        if (ReadOnly)
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");

        _gasBurnt += amount;
    }

    public void Burn(ResourceKind kind, ulong amount)
    {
        if (ReadOnly)
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");

        _gasBurnt += amount;
        _burnedMultiGas.Increment(kind, amount);
    }

    public ulong Burned => _gasBurnt;
    public MultiGas BurnedMultiGas => _burnedMultiGas;
    public bool ReadOnly { get; } = readOnly;
    public ref ulong GasLeft => throw new InvalidOperationException("SystemBurner does not track gas left.");

    /// <summary>
    /// Merge in another MultiGas (e.g., from precompile context) into this burner's tracked gas.
    /// </summary>
    public void AddBurnedMultiGas(in MultiGas toAdd) => _burnedMultiGas.Add(in toAdd);

    /// <summary>
    /// Restore BurnedMultiGas to a previously saved value.
    /// Used by owner precompiles which don't charge multigas.
    /// </summary>
    public void RestoreBurnedMultiGas(in MultiGas saved) => _burnedMultiGas = saved;
}

public class ZeroGasBurner : IBurner
{
    private ulong _zeroGas = 0;

    public TracingInfo? TracingInfo => null;
    public void Burn(ulong amount)
    {
    }

    public void Burn(ResourceKind kind, ulong amount)
    {
    }

    public ulong Burned => 0;
    public MultiGas BurnedMultiGas => default;
    public bool ReadOnly => true;

    public ref ulong GasLeft => ref _zeroGas;
}
