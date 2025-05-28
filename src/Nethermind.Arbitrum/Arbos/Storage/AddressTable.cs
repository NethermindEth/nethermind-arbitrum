namespace Nethermind.Arbitrum.Arbos.Storage;

public class AddressTable(ArbosStorage storage)
{
    private readonly ArbosStorage _byAddressStorage = storage.OpenSubStorage([]);
    private readonly ArbosStorageBackedULong _numItemsStorage = new(storage, 0);

    public static void Initialize(ArbosStorage storage)
    {
    }
}
