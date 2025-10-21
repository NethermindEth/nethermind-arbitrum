using Nethermind.Core;
using Nethermind.Core.Crypto;
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

    public ulong Size()
    {
        return _sizeStorage.Get();
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

    public void Remove(Address address, ulong arbosVersion)
    {
        ValueHash256 addressHash = address.ToHash();

        ulong slot = _byAddressStorage.GetULong(addressHash);
        if (slot == 0)
        {
            return;
        }

        _byAddressStorage.Clear(addressHash);

        ulong size = _sizeStorage.Get();
        if (slot < size)
        {
            ValueHash256 addressAtSize = storage.Get(size);
            storage.Set(slot, addressAtSize);

            if (arbosVersion >= ArbosVersion.Eleven)
            {
                _byAddressStorage.Set(addressAtSize, slot);
            }
        }

        storage.Clear(size);
        _sizeStorage.Set(size - 1);
    }

    public Address[] AllMembers(ulong maxNumToReturn)
    {
        ulong size = System.Math.Min(_sizeStorage.Get(), maxNumToReturn);
        Address[] members = new Address[size];
        for (ulong i = 0; i < size; i++)
        {
            ArbosStorageBackedAddress addressStorage = new(storage, i + 1);
            members[i] = addressStorage.Get();
        }

        return members;
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

    public void RectifyMapping(Address address) // This method is used to fix the mapping of an address in pre ArbOS version 11
    {
        bool isOwner = IsMember(address);
        if (!isOwner)
        {
            throw new InvalidOperationException($"Address {address} is not an owner.");
        }

        // If the mapping is correct, RectifyMapping shouldn't do anything
        // Additional safety check to avoid corruption of mapping after the initial fix
        ValueHash256 addressHash = address.ToHash();
        ulong slot = _byAddressStorage.GetULong(addressHash);
        ValueHash256 addressAtSlot = storage.Get(slot);
        ulong size = _sizeStorage.Get();
        if (addressHash == addressAtSlot && slot <= size)
        {
            throw new InvalidOperationException($"Owner address {address} is correctly mapped.");
        }

        // Remove the owner from map and add them as a new owner
        _byAddressStorage.Clear(addressHash);

        // This one will increase the size... that's how it works in Nitro v3.6.5
        Add(address);
    }
}
