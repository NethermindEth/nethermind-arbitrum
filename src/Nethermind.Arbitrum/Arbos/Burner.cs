using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;

public interface IBurner
{
    void Burn(ulong amount);
    ulong Burned { get; }
    bool ReadOnly { get; }
    void Restrict(Exception? ex); // Added from Go's Burner
    Task HandleErrorAsync(Exception ex); // Added from Go's Burner
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

    public void Restrict(Exception? ex)
    {
        if (ex != null)
        {
            _logger.Error("SystemBurner: Restrict called with an error.", ex);
            // Go's version logs and continues. If this should halt, throw here.
        }
    }

    public Task HandleErrorAsync(Exception ex)
    {
        _logger.Error("SystemBurner: Fatal error encountered.", ex);
        // Go's version panics. This is equivalent to a critical failure.
        throw new InvalidOperationException("Fatal error in system burner.", ex);
    }
}
