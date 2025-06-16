using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

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
        ValueHash256 member = _byAddressStorage.Get(address.ToHash());
        return member != default;
    }

    public void Add(Address address)
    {
        if (IsMember(address))
        {
            return;
        }

        ulong size = _sizeStorage.Get();
        ulong nextSlot = size + 1;

        _byAddressStorage.Set(address.ToHash(), new UInt256(nextSlot).ToValueHash());
        ArbosStorageBackedAddress addressStorage = new(storage, nextSlot);
        addressStorage.Set(address);

        _sizeStorage.Increment();
    }

    public void ClearList()
    {
        var size = _sizeStorage.Get();
        for (ulong i = 1; i <= size; i++)
        {
            storage.Clear(i);
        }

        _sizeStorage.Set(0);
    }
}
