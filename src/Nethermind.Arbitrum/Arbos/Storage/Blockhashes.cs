using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class Blockhashes(ArbosStorage storage)
{
    private readonly ArbosStorageBackedULong _l1BlockNumberStorage = new(storage, 0);

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
    }
}
