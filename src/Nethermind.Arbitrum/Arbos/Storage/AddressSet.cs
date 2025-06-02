using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class AddressSet(ArbosStorage storage)
{
    private const ulong SizeOffset = 0;
    private static readonly byte[] ByAddressSubStorageKey = [0];

    private readonly ArbosStorageBackedULong _sizeStorage = new(storage, SizeOffset);
    private readonly ArbosStorage _byAddressStorage = storage.OpenSubStorage(ByAddressSubStorageKey);

    public static void Initialize(ArbosStorage storage)
    {
        storage.Set(SizeOffset, 0);
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
        ArbosStorageBackedAddress addressStorage = new(storage, size + 1);
        addressStorage.Set(address);

        _sizeStorage.Increment();
    }

    public void Clear()
    {
        var size = _sizeStorage.Get();
        for (ulong i = 1; i <= size; i++)
        {
            storage.Clear(i);
        }

        _sizeStorage.Set(0);
    }
}
