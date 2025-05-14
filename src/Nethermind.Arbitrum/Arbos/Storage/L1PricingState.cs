using System.Numerics;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Arbos;
using Int256;

public class L1PricingState(ArbosStorage storage, ILogger logger)
{
    private const ulong PayRewardsToOffset = 0;
    private const ulong EquilibrationUnitsOffset = 1;
    private const ulong InertiaOffset = 2;
    private const ulong PerUnitRewardOffset = 3;
    private const ulong LastUpdateTimeOffset = 4;
    private const ulong FundsDueForRewardsOffset = 5;
    private const ulong UnitsSinceOffset = 6;
    private const ulong PricePerUnitOffset = 7;
    private const ulong LastSurplusOffset = 8;
    private const ulong PerBatchGasCostOffset = 9;
    private const ulong AmortizedCostCapBipsOffset = 10;
    private const ulong L1FeesAvailableOffset = 11;

    private static readonly byte[] BatchPosterTableKey = [0];

    private const ulong InitialInertia = 10;
    private const ulong InitialPerUnitReward = 10;
    public const ulong InitialPerBatchGasCostV6 = 100_000;
    public const ulong InitialPerBatchGasCostV12 = 210_000;

    public static readonly Int256 InitialEquilibrationUnitsV0 = 60 * GasCostOf.TxDataNonZeroEip2028 * 100_000;
    public static readonly ulong InitialEquilibrationUnitsV6 = GasCostOf.TxDataNonZeroEip2028 * 10_000_000;

    private readonly ArbosStorage _storage = storage;

    public ArbosStorageBackedAddress PayRewardsToStorage { get; } = new(storage, PayRewardsToOffset);
    public ArbosStorageBackedInt256 EquilibrationUnitsStorage { get; } = new(storage, EquilibrationUnitsOffset);

    public static void Initialize(ArbosStorage storage, Address initialRewardsRecipient, Int256 initialL1BaseFee, ILogger logger)
    {
        logger.Info($"L1PricingState: Initializing with recipient {initialRewardsRecipient}, base fee {initialL1BaseFee}...");

        var bptStorage = storage.OpenSubStorage(BatchPosterTableKey);
        BatchPostersTable.Initialize(bptStorage, logger);

        var bpTable = new BatchPostersTable(bptStorage, logger);
        bpTable.AddPoster(ArbosAddresses.BatchPosterAddress, ArbosAddresses.BatchPosterPayToAddress);
        logger.Info($"BatchPostersTable initialized and default poster {ArbosAddresses.BatchPosterAddress} added.");

        storage.SetByUint64(PayRewardsToOffset, initialRewardsRecipient.ToHash2());
        logger.Info($"Set PayRewardsTo: {initialRewardsRecipient}");

        var equilibrationUnits = new ArbosStorageBackedInt256(storage, EquilibrationUnitsOffset);
        equilibrationUnits.Set(InitialEquilibrationUnitsV0);
        logger.Info($"Set EquilibrationUnits: {InitialEquilibrationUnitsV0}");

        var inertia = new ArbosStorageBackedUint64(storage, InertiaOffset);
        inertia.Set(InitialInertia);
        logger.Info($"Set Inertia: {InitialInertia}");

        var fundsDueForRewards = new ArbosStorageBackedInt256(storage, FundsDueForRewardsOffset);
        fundsDueForRewards.Set(Int256.Zero);
        logger.Info("Set FundsDueForRewards: 0");

        var perUnitReward = new ArbosStorageBackedUint64(storage, PerUnitRewardOffset);
        perUnitReward.Set(InitialPerUnitReward);
        logger.Info($"Set PerUnitReward: {InitialPerUnitReward}");

        var pricePerUnit = new ArbosStorageBackedInt256(storage, PricePerUnitOffset);
        pricePerUnit.Set(initialL1BaseFee);
        logger.Info($"Set PricePerUnit (InitialL1BaseFee): {initialL1BaseFee}");

        logger.Info("L1PricingState initialization complete.");
    }

    public Task SetLastSurplusAsync(BigInteger surplus, ulong blockNum)
    {
        logger.Info($"L1PricingState: SetLastSurplus {surplus} at block {blockNum}"); /* TODO: Implement */
        return Task.CompletedTask;
    }

    public Task SetPerBatchGasCostAsync(ulong cost)
    {
        logger.Info($"L1PricingState: SetPerBatchGasCost {cost}"); /* TODO: Implement */
        return Task.CompletedTask;
    }

    public Task SetAmortizedCostCapBipsAsync(ulong bips)
    {
        logger.Info($"L1PricingState: SetAmortizedCostCapBips {bips}"); /* TODO: Implement */
        return Task.CompletedTask;
    }

    public Task SetL1FeesAvailableAsync(UInt256 fees)
    {
        logger.Info($"L1PricingState: SetL1FeesAvailable {fees}"); /* TODO: Implement */
        return Task.CompletedTask;
    }

    public Task<ulong> AmortizedCostCapBipsAsync()
    {
        logger.Info("L1PricingState: GetAmortizedCostCapBips"); /* TODO: Implement */
        return Task.FromResult(0ul);
    }

    public Task SetEquilibrationUnitsAsync(ulong units)
    {
        logger.Info($"L1PricingState: SetEquilibrationUnits {units}"); /* TODO: Implement, this should be BigInt */
        return Task.CompletedTask;
    }
}
