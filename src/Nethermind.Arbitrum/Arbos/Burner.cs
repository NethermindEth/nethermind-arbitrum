using Nethermind.Arbitrum.Tracing;

namespace Nethermind.Arbitrum.Arbos;

public interface IBurner
{
    public ulong Burned { get; }
    public ref ulong GasLeft { get; }
    public bool ReadOnly { get; }
    public TracingInfo? TracingInfo { get; }
    public void Burn(ulong amount);
}

public class SystemBurner(TracingInfo? tracingInfo = null, bool readOnly = false) : IBurner
{
    private ulong _gasBurnt;

    public ulong Burned => _gasBurnt;
    public ref ulong GasLeft => throw new InvalidOperationException("SystemBurner does not track gas left."); // Strange, but consistent with Nitro.
    public bool ReadOnly { get; } = readOnly;

    public TracingInfo? TracingInfo { get; } = tracingInfo;
    public void Burn(ulong amount)
    {
        if (ReadOnly)
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");

        _gasBurnt += amount;
    } // Strange, but consistent with Nitro.
}

public class ZeroGasBurner : IBurner
{
    private ulong _zeroGas = 0;

    public ulong Burned => 0;

    public ref ulong GasLeft => ref _zeroGas;
    public bool ReadOnly => true;

    public TracingInfo? TracingInfo => null;

    public void Burn(ulong amount)
    {
    }
}
