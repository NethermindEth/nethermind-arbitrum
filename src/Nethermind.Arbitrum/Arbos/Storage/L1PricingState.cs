using System.Buffers.Binary;
using Nethermind.Arbitrum.Arbos.Compression;
using Nethermind.Arbitrum.Execution.Transactions;
using System.Numerics;
using Nethermind.Arbitrum.Execution;
using Nethermind.Arbitrum.Math;
using Nethermind.Arbitrum.Tracing;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Evm.TransactionProcessing;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;
using Nethermind.State;

namespace Nethermind.Arbitrum.Arbos.Storage;

public partial class L1PricingState(ArbosStorage storage)
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

    private static readonly UInt256 DefaultNonce = new(Keccak.Compute("Nonce"u8.ToArray()).BytesToArray().AsSpan()[..8]);
    private static readonly UInt256 DefaultDecodedMaxFeePerGas = new(Keccak.Compute("GasTipCap"u8.ToArray()).BytesToArray().AsSpan()[..4]);
    private static readonly UInt256 DefaultGasPrice = new(Keccak.Compute("GasFeeCap"u8.ToArray()).BytesToArray().AsSpan()[..4]);
    private static readonly long DefaultGasLimit = BinaryPrimitives.ReadInt32BigEndian(Keccak.Compute("Gas"u8.ToArray()).BytesToArray().AsSpan()[..4]);
    private const ulong ArbitrumOneChainId = 42_161; // see nitro's arbitrum_chain_info.json or arbitrum docs
    private static readonly ulong DefaultSignatureV = ArbitrumOneChainId * 3;
    private static readonly byte[] DefaultSignatureR = Keccak.Compute("R"u8.ToArray()).BytesToArray();
    private static readonly byte[] DefaultSignatureS = Keccak.Compute("S"u8.ToArray()).BytesToArray();

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
    public ArbosStorageBackedBigInteger LastSurplusStorage { get; } = new(storage, LastSurplusOffset);
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

    public void SetEquilibrationUnits(UInt256 units)
    {
        EquilibrationUnitsStorage.Set(units);
    }

    public void SetInertia(ulong inertia)
    {
        InertiaStorage.Set(inertia);
    }

    public void SetPayRewardsTo(Address newPayRewardsTo)
    {
        PayRewardsToStorage.Set(newPayRewardsTo);
    }

    public void SetPerUnitReward(ulong perUnitReward)
    {
        PerUnitRewardStorage.Set(perUnitReward);
    }

    public void SetPricePerUnit(UInt256 pricePerUnit)
    {
        PricePerUnitStorage.Set(pricePerUnit);
    }

    public void AddToUnitsSinceUpdate(ulong units) =>
        UnitsSinceStorage.Set(UnitsSinceStorage.Get() + units);

    // In Nitro, this function checks for null tx. It seems like the tx is null only in the case
    // where this function is not called within tx processing.
    public (UInt256, ulong) PosterDataCost(
        Transaction tx, Address poster, ulong brotliCompressionLevel, bool isTransactionProcessing
    )
    {
        if (isTransactionProcessing)
            return GetPosterInfo(tx, poster, brotliCompressionLevel);

        // If we're not in tx processing, fill tx with as many fields as possible from passed tx
        // and otherwise with hardcoded fillers to estimate the poster gas cost.
        Transaction fakeTx = new()
        {
            Type = TxType.EIP1559,
            Nonce = tx.Nonce != 0 ? tx.Nonce : DefaultNonce,
            GasPrice = tx.GasPrice != 0 ? tx.GasPrice : DefaultGasPrice, // to set MaxPriorityFeePerGas property
            DecodedMaxFeePerGas = tx.DecodedMaxFeePerGas != 0 ? tx.DecodedMaxFeePerGas : DefaultDecodedMaxFeePerGas,
            // During gas estimation, we don't want the gas limit variability to change the L1 cost.
            // Make sure to set gasLimit to RandomGasLimit during gasEstimation.
            GasLimit = tx.GasLimit != 0 ? tx.GasLimit : DefaultGasLimit,
            To = tx.To,
            Value = tx.Value,
            Data = tx.Data,
            AccessList = tx.AccessList,
            Signature = new Signature(DefaultSignatureR, DefaultSignatureS, DefaultSignatureV)
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
        ulong l1Bytes = (ulong)BrotliCompression.Compress(encodedTx.Bytes, brotliCompressionLevel).Length;

        return l1Bytes * GasCostOf.TxDataNonZeroEip2028;
    }

    private static bool TxTypeHasPosterCosts(ArbitrumTxType txType) =>
        txType is not
            ArbitrumTxType.ArbitrumUnsigned or
            ArbitrumTxType.ArbitrumContract or
            ArbitrumTxType.ArbitrumRetry or
            ArbitrumTxType.ArbitrumInternal or
            ArbitrumTxType.ArbitrumSubmitRetryable;

    public ArbosStorageUpdateResult UpdateForBatchPosterSpending(ulong updateTime, ulong currentTime, Address batchPosterAddress, BigInteger weiSpent, UInt256 l1BaseFee, ArbosState arbosState, IWorldState worldState, IReleaseSpec releaseSpec, TracingInfo? tracingInfo)
    {
        var currentArbosVersion = arbosState.CurrentArbosVersion;
        if (currentArbosVersion < ArbosVersion.Ten)
        {
            return UpdateForBatchPosterSpending_v10(updateTime, currentTime, batchPosterAddress, weiSpent, l1BaseFee,
                arbosState, worldState, releaseSpec, tracingInfo);
        }

        var batchPoster = BatchPosterTable.OpenPoster(batchPosterAddress, true);

        var lastUpdateTime = LastUpdateTimeStorage.Get();
        if (lastUpdateTime == 0 && updateTime > 0)
        {
            // it's the first update, so there isn't a last update time
            lastUpdateTime = updateTime - 1;
        }

        if (updateTime > currentTime || updateTime < lastUpdateTime)
            return ArbosStorageUpdateResult.InvalidTime;

        var allocationNumerator = updateTime - lastUpdateTime;
        var allocationDenominator = currentTime - lastUpdateTime;

        if (allocationDenominator == 0)
        {
            allocationNumerator = allocationDenominator = 1;
        }

        var unitsSinceUpdate = UnitsSinceStorage.Get();
        ulong unitsAllocated = unitsSinceUpdate.SaturateMul(allocationNumerator) / allocationDenominator;
        unitsSinceUpdate -= unitsAllocated;
        UnitsSinceStorage.Set(unitsSinceUpdate);

        if (currentArbosVersion >= ArbosVersion.Three)
        {
            var amortizedCostCapBips = AmortizedCostCapBipsStorage.Get();

            if (amortizedCostCapBips > 0)
            {
                UInt256 weiSpentCap = (l1BaseFee * unitsAllocated * amortizedCostCapBips) / Utils.BipsMultiplier;
                if (weiSpentCap < (UInt256)weiSpent)
                {
                    // apply the cap on assignment of amortized cost;
                    // the difference will be a loss for the batch poster
                    weiSpent = (BigInteger)weiSpentCap;
                }
            }
        }

        batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() + weiSpent);

        UInt256 fundsDueForRewards = FundsDueForRewardsStorage.Get() + unitsAllocated * PerUnitRewardStorage.Get();
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        // pay rewards, as much as possible
        UInt256 paymentForRewards = PerUnitRewardStorage.Get() * new UInt256(unitsAllocated);
        UInt256 l1FeesAvailable = L1FeesAvailableStorage.Get();
        if (l1FeesAvailable < paymentForRewards)
            paymentForRewards = l1FeesAvailable;

        fundsDueForRewards -= paymentForRewards;
        FundsDueForRewardsStorage.Set(fundsDueForRewards);

        var result = TransferFromL1FeesAvailable(PayRewardsToStorage.Get(), paymentForRewards, arbosState, worldState, releaseSpec, ref l1FeesAvailable, tracingInfo);
        if (result != ArbosStorageUpdateResult.Ok)
            return result;

        BigInteger balanceToTransfer = batchPoster.GetFundsDue();
        if (l1FeesAvailable < (UInt256)balanceToTransfer)
            balanceToTransfer = (BigInteger)l1FeesAvailable;

        if (balanceToTransfer > 0)
        {
            result = TransferFromL1FeesAvailable(batchPoster.GetPayTo(), (UInt256)balanceToTransfer, arbosState, worldState, releaseSpec, ref l1FeesAvailable, tracingInfo);
            if (result != ArbosStorageUpdateResult.Ok)
                return result;
            batchPoster.SetFundsDueSaturating(batchPoster.GetFundsDue() - balanceToTransfer);
        }

        LastUpdateTimeStorage.Set(updateTime);

        if (unitsAllocated > 0)
        {
            BigInteger totalFundsDue = BatchPosterTable.GetTotalFundsDue();
            BigInteger surplus = (BigInteger)l1FeesAvailable - (totalFundsDue + (BigInteger)fundsDueForRewards);

            UInt256 inertiaUnits = EquilibrationUnitsStorage.Get() / InertiaStorage.Get();

            UInt256 allocPlusInert = inertiaUnits + unitsAllocated;
            BigInteger lastSurplus = LastSurplusStorage.Get();

            BigInteger desiredDerivative = Utils.FloorDiv(BigInteger.Negate(surplus), (BigInteger)EquilibrationUnitsStorage.Get());
            BigInteger actualDerivative = Utils.FloorDiv(surplus - lastSurplus, unitsAllocated);
            BigInteger changeDerivativeBy = desiredDerivative - actualDerivative;
            BigInteger priceChange = Utils.FloorDiv(changeDerivativeBy * unitsAllocated, (BigInteger)allocPlusInert);

            SetLastSurplus(surplus, arbosState.CurrentArbosVersion);
            BigInteger newPrice = (BigInteger)PricePerUnitStorage.Get() + priceChange;

            if (newPrice < 0)
                newPrice = 0;

            PricePerUnitStorage.Set((UInt256)newPrice);
        }
        return ArbosStorageUpdateResult.Ok;
    }

    public void SetLastSurplus(BigInteger surplus, ulong arbosVersion)
    {
        if (arbosVersion < ArbosVersion.Seven)
            LastSurplusStorage.SetPreVersion7(surplus);
        else
            LastSurplusStorage.SetSaturating(surplus);
    }

    public ArbosStorageUpdateResult TransferFromL1FeesAvailable(Address recipient, UInt256 amount, ArbosState arbosState, IWorldState worldState, IReleaseSpec releaseSpec, ref UInt256 l1FeesLeft, TracingInfo? tracingInfo)
    {
        var tr = ArbitrumTransactionProcessor.TransferBalance(ArbosAddresses.L1PricerFundsPoolAddress,
            recipient,
            amount, arbosState, worldState, releaseSpec, tracingInfo);

        if (tr != TransactionResult.Ok)
            return new ArbosStorageUpdateResult(tr.Error);

        var l1FeesAvailable = L1FeesAvailableStorage.Get();
        if (amount > l1FeesAvailable)
            return ArbosStorageUpdateResult.InsufficientFunds;

        l1FeesLeft = l1FeesAvailable - amount;
        L1FeesAvailableStorage.Set(l1FeesLeft);

        return ArbosStorageUpdateResult.Ok;
    }

    public BigInteger GetL1PricingSurplus()
    {
        BigInteger fundsDueForRefunds = BatchPosterTable.GetTotalFundsDue();
        UInt256 fundsDueForRewards = FundsDueForRewardsStorage.Get();

        BigInteger fundsNeeded = fundsDueForRefunds + (BigInteger)fundsDueForRewards;

        UInt256 fundsAvailable = L1FeesAvailableStorage.Get();

        return (BigInteger)fundsAvailable - fundsNeeded;
    }
}
