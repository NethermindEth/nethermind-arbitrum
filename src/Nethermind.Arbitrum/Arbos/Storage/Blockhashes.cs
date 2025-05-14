using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;

public class Blockhashes(ArbosStorage storage, ILogger logger)
{
    private readonly ArbosStorage _storage = storage;
    private readonly ILogger _logger = logger;

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        logger.Info("Blockhashes initialized.");
    }
}
