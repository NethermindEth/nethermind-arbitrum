using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class MerkleAccumulator
{
    private readonly ArbosStorageBackedULong _sizeStorage;

    public MerkleAccumulator(ArbosStorage storage)
    {
        _sizeStorage = new ArbosStorageBackedULong(storage, 0);
    }

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
    }
}
