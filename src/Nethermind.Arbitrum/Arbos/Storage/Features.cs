using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;

public class Features
{
    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;

    public Features(ArbosStorage storage, ILogger logger)
    {
        _storage = storage;
        _logger = logger;
        _logger.Info("Features instance created.");
    }

    public static Task InitializeAsync(ArbosStorage storage, ILogger logger)
    {
        logger.Info("Features: Initializing...");
        // Actual init logic
        return Task.CompletedTask;
    }
}