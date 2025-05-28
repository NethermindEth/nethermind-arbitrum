using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class MerkleAccumulator
{
    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;
    private readonly ArbosStorageBackedULong _sizeStorage;

    public MerkleAccumulator(ArbosStorage storage, ILogger logger)
    {
        _storage = storage;
        _logger = logger;

        _sizeStorage = new ArbosStorageBackedULong(_storage, 0);
    }

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        logger.Info("MerkleAccumulator initialized.");
    }
}
