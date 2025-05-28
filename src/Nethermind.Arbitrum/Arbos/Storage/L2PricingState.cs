using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class L2PricingState
{
    private const ulong SpeedLimitPerSecondOffset = 0;
    private const ulong PerBlockGasLimitOffset = 1;
    private const ulong BaseFeeWeiOffset = 2;
    private const ulong MinBaseFeeWeiOffset = 3;
    private const ulong GasBacklogOffset = 4;
    private const ulong PricingInertiaOffset = 5;
    private const ulong BacklogToleranceOffset = 6;

    public const ulong InitialSpeedLimitPerSecondV0 = 1_000_000;
    public const ulong InitialPerBlockGasLimitV0 = 20 * 1_000_000;

    public const ulong InitialSpeedLimitPerSecondV6 = 7_000_000;
    public const ulong InitialPerBlockGasLimitV6 = 32 * 1_000_000;

    // params.GWei / 10 = 10^9 / 10 = 10^8 = 100_000_000
    public static readonly ulong InitialMinimumBaseFeeWei = (ulong)(Unit.GWei / 10);
    public static readonly ulong InitialBaseFeeWei = InitialMinimumBaseFeeWei;
    public const ulong InitialPricingInertia = 102;
    public const ulong InitialBacklogTolerance = 10;

    private readonly ArbosStorage _storage;
    private readonly ILogger _logger;

    public L2PricingState(ArbosStorage storage, ILogger logger)
    {
        _logger = logger;
        _storage = storage;

        SpeedLimitPerSecondStorage = new ArbosStorageBackedULong(storage, SpeedLimitPerSecondOffset);
        PerBlockGasLimitStorage = new ArbosStorageBackedULong(storage, PerBlockGasLimitOffset);
        BaseFeeWeiStorage = new ArbosStorageBackedUInt256(storage, BaseFeeWeiOffset);
        MinBaseFeeWeiStorage = new ArbosStorageBackedUInt256(storage, MinBaseFeeWeiOffset);
        GasBacklogStorage = new ArbosStorageBackedULong(storage, GasBacklogOffset);
        PricingInertiaStorage = new ArbosStorageBackedULong(storage, PricingInertiaOffset);
        BacklogToleranceStorage = new ArbosStorageBackedULong(storage, BacklogToleranceOffset);
    }

    public ArbosStorageBackedULong SpeedLimitPerSecondStorage { get; }
    public ArbosStorageBackedULong PerBlockGasLimitStorage { get; }
    public ArbosStorageBackedUInt256 BaseFeeWeiStorage { get; }
    public ArbosStorageBackedUInt256 MinBaseFeeWeiStorage { get; }
    public ArbosStorageBackedULong GasBacklogStorage { get; }
    public ArbosStorageBackedULong PricingInertiaStorage { get; }
    public ArbosStorageBackedULong BacklogToleranceStorage { get; }

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        logger.Info("L2PricingState: Initializing...");
        storage.SetULongByULong(SpeedLimitPerSecondOffset, InitialSpeedLimitPerSecondV0);
        logger.Info($"Set SpeedLimitPerSecond: {InitialSpeedLimitPerSecondV0}");

        storage.SetULongByULong(PerBlockGasLimitOffset, InitialPerBlockGasLimitV0);
        logger.Info($"Set PerBlockGasLimit: {InitialPerBlockGasLimitV0}");

        storage.SetULongByULong(BaseFeeWeiOffset, InitialBaseFeeWei);
        logger.Info($"Set BaseFeeWei: {InitialBaseFeeWei}");

        storage.SetULongByULong(GasBacklogOffset, 0);
        logger.Info("Set GasBacklog: 0");

        storage.SetULongByULong(PricingInertiaOffset, InitialPricingInertia);
        logger.Info($"Set PricingInertia: {InitialPricingInertia}");

        storage.SetULongByULong(BacklogToleranceOffset, InitialBacklogTolerance);
        logger.Info($"Set BacklogTolerance: {InitialBacklogTolerance}");

        storage.SetULongByULong(MinBaseFeeWeiOffset, InitialMinimumBaseFeeWei);
        logger.Info($"Set MinBaseFeeWei: {InitialMinimumBaseFeeWei}");

        logger.Info("L2PricingState initialization complete.");
    }

    public void SetSpeedLimitPerSecond(ulong limit)
    {
        _logger.Info($"L2PricingState: SetSpeedLimitPerSecond {limit}");
        SpeedLimitPerSecondStorage.Set(limit);
    }

    public void SetMaxPerBlockGasLimit(ulong limit)
    {
        _logger.Info($"L2PricingState: SetMaxPerBlockGasLimit {limit}");
        PerBlockGasLimitStorage.Set(limit);
    }
}
