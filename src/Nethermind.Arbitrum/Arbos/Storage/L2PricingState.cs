using Nethermind.Core;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;
using Int256;

public class L2PricingState(ArbosStorage storage, ILogger logger)
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

    // V6 constants are used during upgrades in ArbosState.cs
    public const ulong InitialSpeedLimitPerSecondV6 = 7_000_000;
    public const ulong InitialPerBlockGasLimitV6 = 32 * 1_000_000;

    // params.GWei / 10 = 10^9 / 10 = 10^8 = 100_000_000
    public static readonly ulong InitialMinimumBaseFeeWei = (ulong)(Unit.GWei / 10);
    public static readonly ulong InitialBaseFeeWei = InitialMinimumBaseFeeWei;
    public const ulong InitialPricingInertia = 102;
    public const ulong InitialBacklogTolerance = 10;

    private readonly ArbosStorage _storage = storage;

    public static void Initialize(ArbosStorage storage, ILogger logger)
    {
        logger.Info("L2PricingState: Initializing...");
        storage.SetUint64ByUint64(SpeedLimitPerSecondOffset, InitialSpeedLimitPerSecondV0);
        logger.Info($"Set SpeedLimitPerSecond: {InitialSpeedLimitPerSecondV0}");

        storage.SetUint64ByUint64(PerBlockGasLimitOffset, InitialPerBlockGasLimitV0);
        logger.Info($"Set PerBlockGasLimit: {InitialPerBlockGasLimitV0}");

        storage.SetUint64ByUint64(BaseFeeWeiOffset, InitialBaseFeeWei);
        logger.Info($"Set BaseFeeWei: {InitialBaseFeeWei}");

        storage.SetUint64ByUint64(GasBacklogOffset, 0);
        logger.Info("Set GasBacklog: 0");

        storage.SetUint64ByUint64(PricingInertiaOffset, InitialPricingInertia);
        logger.Info($"Set PricingInertia: {InitialPricingInertia}");

        storage.SetUint64ByUint64(BacklogToleranceOffset, InitialBacklogTolerance);
        logger.Info($"Set BacklogTolerance: {InitialBacklogTolerance}");

        storage.SetUint64ByUint64(MinBaseFeeWeiOffset, InitialMinimumBaseFeeWei);
        logger.Info($"Set MinBaseFeeWei: {InitialMinimumBaseFeeWei}");

        logger.Info("L2PricingState initialization complete.");
    }

    public Task SetSpeedLimitPerSecondAsync(ulong limit)
    {
        logger.Info($"L2PricingState: SetSpeedLimitPerSecond {limit}"); /* TODO: Implement */
        return Task.CompletedTask;
    }

    public Task SetMaxPerBlockGasLimitAsync(ulong limit)
    {
        logger.Info($"L2PricingState: SetMaxPerBlockGasLimit {limit}"); /* TODO: Implement */
        return Task.CompletedTask;
    }
}
