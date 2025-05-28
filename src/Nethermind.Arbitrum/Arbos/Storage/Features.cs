namespace Nethermind.Arbitrum.Arbos.Storage;

public class Features
{
    private readonly ArbosStorageBackedUInt256 _featuresStorage;

    public Features(ArbosStorage storage)
    {
        _featuresStorage = new ArbosStorageBackedUInt256(storage, 0);
    }
}
