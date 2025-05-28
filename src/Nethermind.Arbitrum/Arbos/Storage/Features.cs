namespace Nethermind.Arbitrum.Arbos.Storage;

public class Features(ArbosStorage storage)
{
    private readonly ArbosStorageBackedUInt256 _featuresStorage = new(storage, 0);
}
