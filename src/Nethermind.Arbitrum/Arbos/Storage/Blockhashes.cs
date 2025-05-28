using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class Blockhashes
{
    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;
    private readonly ArbosStorageBackedUint64 _l1BlockNumberStorage;

    public Blockhashes(ArbosStorage storage, ILogger logger)
    {
        _storage = storage;
        _logger = logger;

        _l1BlockNumberStorage = new(storage, 0);
    }

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        logger.Info("Blockhashes initialized.");
    }
}
