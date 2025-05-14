using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;

public class AddressSet(ArbosStorage storage, ILogger logger)
{
    private const ulong SizeOffset = 0;
    private static readonly byte[] ByAddressSubStorageKey = [0];

    private readonly ArbosStorage _backingStorage = storage;
    private readonly ArbosStorageBackedUint64 _sizeStorage = new(storage, SizeOffset);
    private readonly ArbosStorage _byAddressStorage = storage.OpenSubStorage(ByAddressSubStorageKey);

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
        logger.Info($"AddressSet: Add {address}");
        if (IsMember(address))
        {
            return;
        }

        var size = _sizeStorage.Get();
        var slot = new ValueHash256(size + 1);

        _byAddressStorage.Set(address.ToHash2(), slot);
        ArbosStorageBackedAddress addressStorage = new(_backingStorage, size + 1);
        addressStorage.Set(address);

        _sizeStorage.Increment();
    }

    public void Clear()
    {
        logger.Info("AddressSet: ClearList"); /* TODO: Implement storage write for clearing list based on Go logic */
    }
}
