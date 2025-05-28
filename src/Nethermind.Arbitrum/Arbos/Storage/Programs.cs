using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class Programs(ArbosStorage storage, ulong arbosVersion)
{
    private static readonly byte[] ParamsKey = [0];
    private static readonly byte[] ProgramDataKey = [1];
    private static readonly byte[] ModuleHashesKey = [2];
    private static readonly byte[] DataPricerKey = [3];
    private static readonly byte[] CacheManagersKey = [4];

    private readonly ArbosStorage _programsStorage = storage.OpenSubStorage(ProgramDataKey);
    private readonly ArbosStorage _moduleHashesStorage = storage.OpenSubStorage(ModuleHashesKey);
    private readonly DataPricer _dataPricer = new(storage.OpenSubStorage(DataPricerKey));
    private readonly AddressSet _cacheManagersStorage = new(storage.OpenSubStorage(CacheManagersKey));

    public ulong ArbosVersion { get; set; } = arbosVersion;

    public StylusParams GetParams()
    {
        var paramsStorage = storage.OpenSubStorage(ParamsKey);
        return StylusParams.Create(paramsStorage, ArbosVersion);
    }

    public static void Initialize(ulong arbosVersion, ArbosStorage storage)
    {
        StylusParams.Initialize(storage.OpenSubStorage(ParamsKey), arbosVersion);
        DataPricer.Initialize(storage.OpenSubStorage(DataPricerKey));
        AddressSet.Initialize(storage.OpenSubStorage(CacheManagersKey));
    }
}
