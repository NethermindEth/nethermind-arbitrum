using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class MerkleAccumulator(ArbosStorage storage)
{
    private readonly ArbosStorageBackedULong _sizeStorage = new(storage, 0);

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
    }
}
