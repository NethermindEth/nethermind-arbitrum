using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;

public class AddressTable
{
    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;
    private readonly ArbosStorage _byAddressStorage;
    private readonly ArbosStorageBackedUint64 _numItemsStorage;

    public AddressTable(ArbosStorage storage, ILogger logger)
    {
        _storage = storage;
        _logger = logger;

        _byAddressStorage = _storage.OpenSubStorage([]);
        _numItemsStorage = new ArbosStorageBackedUint64(_storage, 0);
    }

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        logger.Info("AddressTable initialized.");
    }
}
