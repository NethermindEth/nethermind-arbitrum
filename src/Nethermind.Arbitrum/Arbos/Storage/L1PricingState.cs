using System.Buffers.Binary;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Execution.Transactions;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Eip2930;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Arbos.Storage;

public class L1PricingState(ArbosStorage storage)
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

    private const ulong EstimationPaddingUnits = 16 * GasCostOf.TxDataNonZeroEip2028;
    private const ulong EstimationPaddingBasisPoints = 100;

    private static readonly UInt256 randomNonce = new(Keccak.Compute("Nonce"u8.ToArray()).BytesToArray().AsSpan()[..8]);
    private static readonly UInt256 randomDecodedMaxFeePerGas = new(Keccak.Compute("DecodedMaxFeePerGas"u8.ToArray()).BytesToArray().AsSpan()[..4]);
    private static readonly UInt256 randomGasPrice = new(Keccak.Compute("GasPrice"u8.ToArray()).BytesToArray().AsSpan()[..4]);
    private static readonly long randomGasLimit = BinaryPrimitives.ReadInt32BigEndian(Keccak.Compute("GasLimit"u8.ToArray()).BytesToArray().AsSpan()[..4]);
    private const ulong ArbitrumOneChainId = 42161;
    private static readonly ulong randV = ArbitrumOneChainId * 3;
    private static readonly byte[] randR = Keccak.Compute("R"u8.ToArray()).BytesToArray();
    private static readonly byte[] randS = Keccak.Compute("S"u8.ToArray()).BytesToArray();

    public static readonly UInt256 InitialEquilibrationUnitsV0 = 60 * GasCostOf.TxDataNonZeroEip2028 * 100_000;
    public static readonly ulong InitialEquilibrationUnitsV6 = GasCostOf.TxDataNonZeroEip2028 * 10_000_000;

    public BatchPostersTable BatchPosterTable { get; } = new(storage.OpenSubStorage(BatchPosterTableKey));
    public ArbosStorageBackedAddress PayRewardsToStorage { get; } = new(storage, PayRewardsToOffset);
    public ArbosStorageBackedUInt256 EquilibrationUnitsStorage { get; } = new(storage, EquilibrationUnitsOffset);
    public ArbosStorageBackedULong InertiaStorage { get; } = new(storage, InertiaOffset);
    public ArbosStorageBackedULong PerUnitRewardStorage { get; } = new(storage, PerUnitRewardOffset);
    public ArbosStorageBackedULong LastUpdateTimeStorage { get; } = new(storage, LastUpdateTimeOffset);
    public ArbosStorageBackedUInt256 FundsDueForRewardsStorage { get; } = new(storage, FundsDueForRewardsOffset);
    public ArbosStorageBackedULong UnitsSinceStorage { get; } = new(storage, UnitsSinceOffset);
    public ArbosStorageBackedUInt256 PricePerUnitStorage { get; } = new(storage, PricePerUnitOffset);
    public ArbosStorageBackedUInt256 LastSurplusStorage { get; } = new(storage, LastSurplusOffset);
    public ArbosStorageBackedULong PerBatchGasCostStorage { get; } = new(storage, PerBatchGasCostOffset);
    public ArbosStorageBackedULong AmortizedCostCapBipsStorage { get; } = new(storage, AmortizedCostCapBipsOffset);
    public ArbosStorageBackedUInt256 L1FeesAvailableStorage { get; } = new(storage, L1FeesAvailableOffset);

    public static void Initialize(ArbosStorage storage, Address initialRewardsRecipient, UInt256 initialL1BaseFee)
    {
        var bptStorage = storage.OpenSubStorage(BatchPosterTableKey);
        BatchPostersTable.Initialize(bptStorage);

        var bpTable = new BatchPostersTable(bptStorage);
        bpTable.AddPoster(ArbosAddresses.BatchPosterAddress, ArbosAddresses.BatchPosterPayToAddress);

        storage.Set(PayRewardsToOffset, initialRewardsRecipient.ToHash());

        var equilibrationUnits = new ArbosStorageBackedUInt256(storage, EquilibrationUnitsOffset);
        equilibrationUnits.Set(InitialEquilibrationUnitsV0);

        var inertia = new ArbosStorageBackedULong(storage, InertiaOffset);
        inertia.Set(InitialInertia);

        var fundsDueForRewards = new ArbosStorageBackedUInt256(storage, FundsDueForRewardsOffset);
        fundsDueForRewards.Set(UInt256.Zero);

        var perUnitReward = new ArbosStorageBackedULong(storage, PerUnitRewardOffset);
        perUnitReward.Set(InitialPerUnitReward);

        var pricePerUnit = new ArbosStorageBackedUInt256(storage, PricePerUnitOffset);
        pricePerUnit.Set(initialL1BaseFee);
    }

    public void SetLastSurplus(UInt256 surplus)
    {
        LastSurplusStorage.Set(surplus);
    }

    public void SetPerBatchGasCost(ulong cost)
    {
        PerBatchGasCostStorage.Set(cost);
    }

    public void SetAmortizedCostCapBips(ulong bips)
    {
        AmortizedCostCapBipsStorage.Set(bips);
    }

    public void SetL1FeesAvailable(UInt256 fees)
    {
        L1FeesAvailableStorage.Set(fees);
    }

    public UInt256 AddToL1FeesAvailable(UInt256 delta)
    {
        var currentFees = L1FeesAvailableStorage.Get();
        var newFees = currentFees + delta;
        L1FeesAvailableStorage.Set(newFees);
        return newFees;
    }

    public ulong AmortizedCostCapBips()
    {
        return AmortizedCostCapBipsStorage.Get();
    }

    public void SetEquilibrationUnits(ulong units)
    {
        EquilibrationUnitsStorage.Set(new UInt256(units));
    }

    public (UInt256, ulong) PosterDataCost(
        Transaction? tx, Address poster, ulong brotliCompressionLevel,
        byte[] calldata = null!, AccessList accessList = null!
    )
    {
        if (tx is not null)
            return GetPosterInfo(tx, poster, brotliCompressionLevel);

        // If tx is null, we're in gas estimation. In that case, fill tx with hardcoded fillers to estimate the
        // poster gas cost.
        Transaction fakeTx = new()
        {
            Nonce = randomNonce,
            DecodedMaxFeePerGas = randomDecodedMaxFeePerGas,
            GasPrice = randomGasPrice,
            GasLimit = randomGasLimit,
            To = Address.Zero,
            Value = 1,
            Data = calldata,
            AccessList = accessList,
            Signature = new Signature(randR, randS, randV)
        };
        ulong units = GetPosterUnitsWithoutCache(fakeTx, poster, brotliCompressionLevel);
        // We don't have the full tx in gas estimation, so we assume it might be a bit bigger in practice.
        units = Math.Utils.UlongMulByBips(
            units + EstimationPaddingUnits, Math.Utils.BipsMultiplier + EstimationPaddingBasisPoints
        );

        return (PricePerUnitStorage.Get() * units, units);
    }

    // GetPosterInfo returns the poster cost and the calldata units for a transaction
    private (UInt256, ulong) GetPosterInfo(Transaction tx, Address poster, ulong brotliCompressionLevel)
    {
        if (poster != ArbosAddresses.BatchPosterAddress)
            return (UInt256.Zero, 0);

        ulong units = tx.GetCachedCalldataUnits(brotliCompressionLevel);
        if (units == 0)
        {
            // The cache is empty or invalid, so we need to compute the calldata units
            units = GetPosterUnitsWithoutCache(tx, poster, brotliCompressionLevel);
            tx.SetCachedCalldataUnits(brotliCompressionLevel, units);
        }

	    // Approximate the l1 fee charged for posting this tx's calldata
        return (PricePerUnitStorage.Get() * units, units);
    }

    private static ulong GetPosterUnitsWithoutCache(Transaction tx, Address poster, ulong brotliCompressionLevel)
    {
        if (poster != ArbosAddresses.BatchPosterAddress || !TxTypeHasPosterCosts((ArbitrumTxType)tx.Type))
            return 0;

        Rlp encodedTx = Rlp.Encode(tx);
        ulong l1Bytes = (ulong) Native.Compress(encodedTx.Bytes, brotliCompressionLevel).Length;

        return l1Bytes * GasCostOf.TxDataNonZeroEip2028;
    }

    private static bool TxTypeHasPosterCosts(ArbitrumTxType txType) =>
        txType switch
        {
            ArbitrumTxType.ArbitrumUnsigned or
            ArbitrumTxType.ArbitrumContract or
            ArbitrumTxType.ArbitrumRetry or
            ArbitrumTxType.ArbitrumInternal or
            ArbitrumTxType.ArbitrumSubmitRetryable => false,
            _ => true
        };

    public void AddToUnitsSinceUpdate(ulong units) =>
        UnitsSinceStorage.Set(UnitsSinceStorage.Get() + units);
}
