using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class Programs
{
    private static readonly byte[] ParamsKey = [0];
    private static readonly byte[] ProgramDataKey = [1];
    private static readonly byte[] ModuleHashesKey = [2];
    private static readonly byte[] DataPricerKey = [3];
    private static readonly byte[] CacheManagersKey = [4];

    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;
    private readonly ArbosStorage _programsStorage;
    private readonly ArbosStorage _moduleHashesStorage;
    private readonly DataPricer _dataPricer;
    private readonly AddressSet _cacheManagersStorage;

    public Programs(ArbosStorage storage, ulong arbosVersion, ILogger logger)
    {
        _storage = storage;
        _logger = logger;
        ArbosVersion = arbosVersion;

        _programsStorage = storage.OpenSubStorage(ProgramDataKey);
        _moduleHashesStorage = storage.OpenSubStorage(ModuleHashesKey);
        _dataPricer = new DataPricer(storage.OpenSubStorage(DataPricerKey));
        _cacheManagersStorage = new AddressSet(storage.OpenSubStorage(CacheManagersKey), logger);
    }

    public ulong ArbosVersion { get; set; }

    public StylusParams GetParams()
    {
        _logger.Info("Programs: GetParams");

        var paramsStorage = _storage.OpenSubStorage(ParamsKey);
        return StylusParams.Create(paramsStorage, ArbosVersion);
    }

    public static void Initialize(ulong arbosVersion, ArbosStorage storage, ILogger logger)
    {
        logger.Info($"Programs: Initializing for ArbOS version {arbosVersion}...");

        StylusParams.Initialize(storage.OpenSubStorage(ParamsKey), arbosVersion);
        DataPricer.Initialize(storage.OpenSubStorage(DataPricerKey));
        AddressSet.Initialize(storage.OpenSubStorage(CacheManagersKey), logger);

        logger.Info("Programs: Storage initialized.");
    }
}
