using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;

public interface IBurner
{
    void Burn(ulong amount);
    ulong Burned { get; }
    bool ReadOnly { get; }
}

public class SystemBurner(ILogManager logManager, bool readOnly = false) : IBurner
{
    private readonly ILogger _logger = logManager.GetClassLogger();
    private ulong _gasBurnt;

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
