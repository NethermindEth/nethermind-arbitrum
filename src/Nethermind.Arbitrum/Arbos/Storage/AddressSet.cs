using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class AddressSet
{
    private const ulong SizeOffset = 0;
    private static readonly byte[] ByAddressSubStorageKey = [0];

    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;
    private readonly ArbosStorageBackedUint64 _sizeStorage;
    private readonly ArbosStorage _byAddressStorage;

    public AddressSet(ArbosStorage storage, ILogger logger)
    {
        _storage = storage;
        _logger = logger;

        _sizeStorage = new ArbosStorageBackedUint64(storage, SizeOffset);
        _byAddressStorage = storage.OpenSubStorage(ByAddressSubStorageKey);
    }

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        logger.Info("AddressSet: Initializing...");
        storage.SetUint64ByUint64(0, 0);
        logger.Info("AddressSet initialized (size set to 0).");
    }

    public bool IsMember(Address address)
    {
        var member = _byAddressStorage.Get(address.ToHash2());
        return member != default;
    }

    public void Add(Address address)
    {
        _logger.Info($"AddressSet: Add {address}");
        if (IsMember(address))
        {
            return;
        }

        var size = _sizeStorage.Get();
        var slot = new ValueHash256(size + 1);

        _byAddressStorage.Set(address.ToHash2(), slot);
        ArbosStorageBackedAddress addressStorage = new(_storage, size + 1);
        addressStorage.Set(address);

        _sizeStorage.Increment();
    }

    public void Clear()
    {
        _logger.Info("AddressSet: ClearList");
        var size = _sizeStorage.Get();
        for (ulong i = 1; i <= size; i++)
        {
            _storage.ClearByUint64(i);
        }

        _sizeStorage.Set(0);
    }
}
