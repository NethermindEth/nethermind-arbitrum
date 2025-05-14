using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;

public class Programs
{
    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;
    public ulong ArbosVersion { get; set; } // Set by ArbosState constructor and upgrades

    public Programs(ArbosStorage storage, ILogger logger, ulong arbosVersion)
    {
        _storage = storage;
        _logger = logger;
        ArbosVersion = arbosVersion;
        _logger.Info($"Programs instance created for ArbOS version {ArbosVersion}.");
    }

    // This is called from ArbitrumGenesisLoader.InitializeArbosStateAsync
    // For initial genesis, Go's arbosState.InitializeArbosState doesn't call programs.Initialize.
    // programs.Initialize is called during specific version upgrades (e.g., to version 30).
    // So, this can be a no-op for the initial call from ArbitrumGenesisLoader.
    public static Task InitializeAsync(ulong nextArbosVersion, ArbosStorage storage, ILogger logger)
    {
        logger.Info($"Programs: Initializing for ArbOS version {nextArbosVersion}...");
        // This is a no-op for the initial call from ArbitrumGenesisLoader.
        // Actual init logic from Go's programs.Initialize for specific versions would go here.
        // For example, setting up program parameters based on 'nextArbosVersion'.
        return Task.CompletedTask;
    }

    // This method corresponds to Go's programs.Initialize(nextArbosVersion, ...) called during upgrades
    public Task InitializeForUpgradeAsync(ulong nextArbosVersion)
    {
        _logger.Info($"Programs: Performing version-specific initialization for ArbOS version {nextArbosVersion}...");
        // Actual init logic from Go's programs.Initialize for specific versions would go here.
        // For example, setting up program parameters based on 'nextArbosVersion'.
        ArbosVersion = nextArbosVersion; // Update internal version tracking
        return Task.CompletedTask;
    }

    public Task<ProgramsParams> GetParamsAsync()
    {
        _logger.Info("Programs: GetParams");
        return Task.FromResult<ProgramsParams>(new ProgramsParams(_logger));
    }
}

public class ProgramsParams
{
    private readonly ILogger _logger;

    public ProgramsParams(ILogger logger)
    {
        _logger = logger;
        _logger.Info("ProgramsParams instance created.");
    }

    public Task UpgradeToVersionAsync(int version)
    {
        _logger.Info($"ProgramsParams: UpgradeToVersion {version}");
        return Task.CompletedTask;
    }

    public Task UpgradeToArbosVersionAsync(ulong version)
    {
        _logger.Info($"ProgramsParams: UpgradeToArbosVersion {version}");
        return Task.CompletedTask;
    }

    public Task SaveAsync()
    {
        _logger.Info("ProgramsParams: Save");
        return Task.CompletedTask;
    }
}
