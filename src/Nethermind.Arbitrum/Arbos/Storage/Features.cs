using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class Features
{
    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;
    private readonly ArbosStorageBackedInt256 _featuresStorage;

    public Features(ArbosStorage storage, ILogger logger)
    {
        _storage = storage;
        _logger = logger;

        _featuresStorage = new ArbosStorageBackedInt256(_storage, 0);
    }
}
