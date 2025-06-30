using Nethermind.Arbitrum.Tracing;

namespace Nethermind.Arbitrum.Arbos;

public interface IBurner
{
    public TracingInfo? TracingInfo { get; }
    void Burn(ulong amount);
    ulong Burned { get; }
    bool ReadOnly { get; }
}

public class SystemBurner(TracingInfo? tracingInfo, bool readOnly = false) : IBurner
{
    private ulong _gasBurnt;

    public TracingInfo? TracingInfo { get; private set; } = tracingInfo;

    public void Burn(ulong amount)
    {
        if (ReadOnly)
        {
            throw new InvalidOperationException("Cannot burn gas with a read-only system burner.");
        }

        _gasBurnt += amount;
    }

    public ulong Burned => _gasBurnt;
    public bool ReadOnly { get; } = readOnly;
}
