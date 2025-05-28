using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class Blockhashes
{
    private readonly ArbosStorageBackedULong _l1BlockNumberStorage;

    public Blockhashes(ArbosStorage storage)
    {
        _l1BlockNumberStorage = new(storage, 0);
    }

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
    }
}
