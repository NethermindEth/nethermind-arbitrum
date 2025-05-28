using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class AddressSet
{
    private const ulong SizeOffset = 0;
    private static readonly byte[] ByAddressSubStorageKey = [0];

    private readonly ArbosStorage _storage;
    private readonly ArbosStorageBackedULong _sizeStorage;
    private readonly ArbosStorage _byAddressStorage;

    public AddressSet(ArbosStorage storage)
    {
        _storage = storage;

        _sizeStorage = new ArbosStorageBackedULong(storage, SizeOffset);
        _byAddressStorage = storage.OpenSubStorage(ByAddressSubStorageKey);
    }

    public static void Initialize(ArbosStorage storage)
    {
        storage.SetULongByULong(0, 0);
    }

    public bool IsMember(Address address)
    {
        var member = _byAddressStorage.Get(address.ToHash2());
        return member != default;
    }

    public void Add(Address address)
    {
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
        var size = _sizeStorage.Get();
        for (ulong i = 1; i <= size; i++)
        {
            _storage.ClearByULong(i);
        }

        _sizeStorage.Set(0);
    }
}
