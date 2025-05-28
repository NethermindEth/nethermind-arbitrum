namespace Nethermind.Arbitrum.Arbos.Storage;

public class AddressTable
{
    private readonly ArbosStorage _byAddressStorage;
    private readonly ArbosStorageBackedULong _numItemsStorage;

    public AddressTable(ArbosStorage storage)
    {
        _byAddressStorage = storage.OpenSubStorage([]);
        _numItemsStorage = new ArbosStorageBackedULong(storage, 0);
    }

    public static void Initialize(ArbosStorage storage)
    {
    }
}
