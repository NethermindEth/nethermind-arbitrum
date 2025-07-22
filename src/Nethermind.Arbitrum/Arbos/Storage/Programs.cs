using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class Programs(ArbosStorage storage, ulong arbosVersion)
{
    private static readonly byte[] ParamsKey = [0];
    private static readonly byte[] ProgramDataKey = [1];
    private static readonly byte[] ModuleHashesKey = [2];
    private static readonly byte[] DataPricerKey = [3];
    private static readonly byte[] CacheManagersKey = [4];

    public ArbosStorage ProgramsStorage { get; } = storage.OpenSubStorage(ProgramDataKey);
    public ArbosStorage ModuleHashesStorage { get; } = storage.OpenSubStorage(ModuleHashesKey);
    public DataPricer DataPricerStorage { get; } = new(storage.OpenSubStorage(DataPricerKey));
    public AddressSet CacheManagersStorage { get; } = new(storage.OpenSubStorage(CacheManagersKey));

    public ulong ArbosVersion { get; set; } = arbosVersion;

    public StylusParams GetParams()
    {
        var paramsStorage = storage.OpenSubStorage(ParamsKey);
        return StylusParams.CreateFromStorage(paramsStorage, ArbosVersion);
    }

    public static void Initialize(ulong arbosVersion, ArbosStorage storage)
    {
        StylusParams.InitializeWithDefaults(storage.OpenSubStorage(ParamsKey), arbosVersion);
        DataPricer.Initialize(storage.OpenSubStorage(DataPricerKey));
        AddressSet.Initialize(storage.OpenSubStorage(CacheManagersKey));
    }
}
