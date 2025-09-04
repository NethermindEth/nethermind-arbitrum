using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.State;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Core.Extensions;
using Nethermind.Core.Crypto;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Arbitrum.Arbos.Storage;
using System.Numerics;
using static Nethermind.Arbitrum.Arbos.Storage.BatchPostersTable;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Abi;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbGasInfoParserTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    private const ulong DefaultGasSupplied = 1_000_000;
    private Block _genesisBlock = null!;
    private PrecompileTestContextBuilder _context = null!;
    private ArbosState _freeArbosState = null!;
    private ArbGasInfoParser _parser = null!;

    private static readonly Dictionary<string, AbiFunctionDescription> precompileFunctions =
        AbiMetadata.GetAllFunctionDescriptions(ArbGasInfo.Abi);

    [SetUp]
    public void SetUp()
    {
        (IWorldState worldState, _genesisBlock) = ArbOSInitialization.Create();

        _context = new PrecompileTestContextBuilder(worldState, DefaultGasSupplied).WithArbosState();
        _context.ResetGasLeft();

        _freeArbosState = ArbosState.OpenArbosState(worldState, new ZeroGasBurner(), LimboLogs.Instance.GetClassLogger());

        _parser = new ArbGasInfoParser();
    }

    [Test]
    public void ParsesGetPricesInWeiWithAggregator_ArbosVersionFourOrHigher_ReturnsPrices()
    {
        ulong l2GasPrice = 1_000;
        _genesisBlock.Header.BaseFeePerGas = l2GasPrice;
        _context
            .WithArbosVersion(ArbosVersion.Four)
            .WithBlockExecutionContext(_genesisBlock.Header);

        ulong l1GasPrice = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1GasPrice);
        _freeArbosState.L2PricingState.MinBaseFeeWeiStorage.Set(l2GasPrice + 1);

        string getPricesInWeiWithAggregatorMethodId = "0xba9c916e";
        Address aggregator = Address.Zero;
        string leftPaddedAggregator = aggregator.ToString(withZeroX: false, false).PadLeft(64, '0');

        byte[] inputData = Bytes.FromHexString($"{getPricesInWeiWithAggregatorMethodId}{leftPaddedAggregator}");

        byte[] result = _parser.RunAdvanced(_context, inputData);

        UInt256 expectedWeiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 expectedPerL2Tx = expectedWeiForL1Calldata * ArbGasInfo.AssumedSimpleTxSize;
        UInt256 expectedWeiForL2Storage = l2GasPrice * ArbGasInfo.StorageArbGas;

        byte[] expectedResult = new byte[Hash256.Size * 6];
        int offset = 0;

        expectedPerL2Tx.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedWeiForL1Calldata.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedWeiForL2Storage.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        new UInt256(l2GasPrice).ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        UInt256.Zero.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        new UInt256(l2GasPrice).ToBigEndian().CopyTo(expectedResult, offset);

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - 2 * ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetPricesInWeiWithAggregator_ArbosVersionBelowFour_ReturnsPrices()
    {
        ulong l2GasPrice = 1_000;
        _genesisBlock.Header.BaseFeePerGas = l2GasPrice;
        _context
            .WithArbosVersion(ArbosVersion.Three)
            .WithBlockExecutionContext(_genesisBlock.Header);

        ulong l1GasPrice = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1GasPrice);

        string getPricesInWeiWithAggregatorMethodId = "0xba9c916e";
        Address aggregator = Address.Zero;
        string leftPaddedAggregator = aggregator.ToString(withZeroX: false, false).PadLeft(64, '0');

        byte[] inputData = Bytes.FromHexString($"{getPricesInWeiWithAggregatorMethodId}{leftPaddedAggregator}");

        byte[] result = _parser.RunAdvanced(_context, inputData);

        UInt256 expectedWeiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 expectedPerL2Tx = expectedWeiForL1Calldata * ArbGasInfo.AssumedSimpleTxSize;
        UInt256 expectedWeiForL2Storage = l2GasPrice * ArbGasInfo.StorageArbGas;

        byte[] expectedResult = new byte[Hash256.Size * 6];
        int offset = 0;

        expectedPerL2Tx.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedWeiForL1Calldata.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedWeiForL2Storage.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        new UInt256(l2GasPrice).ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        UInt256.Zero.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        new UInt256(l2GasPrice).ToBigEndian().CopyTo(expectedResult, offset);

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetPricesInWei_ArbosVersionFourOrHigher_ReturnsPrices()
    {
        ulong l2GasPrice = 1_000;
        _genesisBlock.Header.BaseFeePerGas = l2GasPrice;

        _context
            .WithArbosVersion(ArbosVersion.Four)
            .WithBlockExecutionContext(_genesisBlock.Header);

        ulong l1GasPrice = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1GasPrice);

        ulong minBaseFeeWei = l2GasPrice - 1;
        _freeArbosState.L2PricingState.MinBaseFeeWeiStorage.Set(minBaseFeeWei);

        string getPricesInWeiMethodId = "0x41b247a8";
        byte[] inputData = Bytes.FromHexString(getPricesInWeiMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        UInt256 expectedWeiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 expectedPerL2Tx = expectedWeiForL1Calldata * ArbGasInfo.AssumedSimpleTxSize;
        UInt256 expectedWeiForL2Storage = l2GasPrice * ArbGasInfo.StorageArbGas;
        UInt256 expectedPerArbGasBase = minBaseFeeWei;
        UInt256 expectedPerArbGasCongestion = l2GasPrice - expectedPerArbGasBase;

        byte[] expectedResult = new byte[Hash256.Size * 6];
        int offset = 0;

        expectedPerL2Tx.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedWeiForL1Calldata.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedWeiForL2Storage.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedPerArbGasBase.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedPerArbGasCongestion.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        new UInt256(l2GasPrice).ToBigEndian().CopyTo(expectedResult, offset);

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - 2 * ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetPricesInArbGasWithAggregator_ArbosVersionFourOrHigher_ReturnsPrices()
    {
        ulong l2GasPrice = 1_000;
        _genesisBlock.Header.BaseFeePerGas = l2GasPrice;
        _context
            .WithArbosVersion(ArbosVersion.Four)
            .WithBlockExecutionContext(_genesisBlock.Header);

        ulong l1GasPrice = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1GasPrice);

        string getPricesInArbGasWithAggregatorMethodId = "0x7a1ea732";
        Address aggregator = Address.Zero;
        string leftPaddedAggregator = aggregator.ToString(withZeroX: false, false).PadLeft(64, '0');

        byte[] inputData = Bytes.FromHexString($"{getPricesInArbGasWithAggregatorMethodId}{leftPaddedAggregator}");

        byte[] result = _parser.RunAdvanced(_context, inputData);

        UInt256 weiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 weiPerL2Tx = weiForL1Calldata * ArbGasInfo.AssumedSimpleTxSize;

        UInt256 expectedGasForL1Calldata = weiForL1Calldata / l2GasPrice;
        UInt256 expectedGasPerL2Tx = weiPerL2Tx / l2GasPrice;

        byte[] expectedResult = new byte[Hash256.Size * 3];
        int offset = 0;

        expectedGasPerL2Tx.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedGasForL1Calldata.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        ArbGasInfo.StorageArbGas.ToBigEndian().CopyTo(expectedResult, offset);

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetPricesInArbGasWithAggregator_ArbosVersionBelowFour_ReturnsPrices()
    {
        ulong l2GasPrice = 1_000;
        _genesisBlock.Header.BaseFeePerGas = l2GasPrice;
        _context
            .WithArbosVersion(ArbosVersion.Three)
            .WithBlockExecutionContext(_genesisBlock.Header);

        ulong l1GasPrice = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1GasPrice);

        string getPricesInArbGasWithAggregatorMethodId = "0x7a1ea732";
        Address aggregator = Address.Zero;
        string leftPaddedAggregator = aggregator.ToString(withZeroX: false, false).PadLeft(64, '0');

        byte[] inputData = Bytes.FromHexString($"{getPricesInArbGasWithAggregatorMethodId}{leftPaddedAggregator}");

        byte[] result = _parser.RunAdvanced(_context, inputData);

        UInt256 weiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 expectedGasForL1Calldata = weiForL1Calldata / l2GasPrice;
        UInt256 expectedGasPerL2Tx = ArbGasInfo.AssumedSimpleTxSize;

        byte[] expectedResult = new byte[Hash256.Size * 3];
        int offset = 0;

        expectedGasPerL2Tx.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        expectedGasForL1Calldata.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        ArbGasInfo.StorageArbGas.ToBigEndian().CopyTo(expectedResult, offset);

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetPricesInArbGas_ArbosVersionFourOrHigher_ReturnsPrices()
    {
        ulong l2GasPrice = 0;
        _genesisBlock.Header.BaseFeePerGas = l2GasPrice;
        _context
            .WithArbosVersion(ArbosVersion.Four)
            .WithBlockExecutionContext(_genesisBlock.Header);

        ulong l1GasPrice = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1GasPrice);

        string getPricesInArbGasMethodId = "0x02199f34";
        byte[] inputData = Bytes.FromHexString(getPricesInArbGasMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        byte[] expectedResult = new byte[Hash256.Size * 3];
        int offset = 0;

        UInt256.Zero.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        UInt256.Zero.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        ArbGasInfo.StorageArbGas.ToBigEndian().CopyTo(expectedResult, offset);

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetGasAccountingParams_Always_ReturnsValues()
    {
        ulong speedLimitPerSecond = 100;
        _freeArbosState.L2PricingState.SpeedLimitPerSecondStorage.Set(speedLimitPerSecond);

        ulong gasLimitPerBlock = 200;
        _freeArbosState.L2PricingState.PerBlockGasLimitStorage.Set(gasLimitPerBlock);

        string getGasAccountingParamsMethodId = "0x612af178";
        byte[] inputData = Bytes.FromHexString(getGasAccountingParamsMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        byte[] expectedResult = new byte[Hash256.Size * 3];
        int offset = 0;

        new UInt256(speedLimitPerSecond).ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        UInt256 gasLimitPerBlockUInt256 = gasLimitPerBlock;

        gasLimitPerBlockUInt256.ToBigEndian().CopyTo(expectedResult, offset);
        offset += Hash256.Size;

        gasLimitPerBlockUInt256.ToBigEndian().CopyTo(expectedResult, offset);

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - 2 * ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetMinimumGasPrice_Always_ReturnsMinGasPrice()
    {
        ulong minBaseFeeWei = 100;
        _freeArbosState.L2PricingState.MinBaseFeeWeiStorage.Set(minBaseFeeWei);

        string getMinimumGasPriceMethodId = "0xf918379a";
        byte[] inputData = Bytes.FromHexString(getMinimumGasPriceMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(minBaseFeeWei).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1BaseFeeEstimate_Always_ReturnsL1BaseFeeEstimate()
    {
        ulong l1BaseFeeEstimate = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1BaseFeeEstimate);

        string getL1BaseFeeEstimateMethodId = "0xf5d6ded7";
        byte[] inputData = Bytes.FromHexString(getL1BaseFeeEstimateMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(l1BaseFeeEstimate).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1BaseFeeEstimateInertia_Always_ReturnsL1BaseFeeEstimateInertia()
    {
        ulong l1BaseFeeEstimateInertia = 100;
        _freeArbosState.L1PricingState.InertiaStorage.Set(l1BaseFeeEstimateInertia);

        string getL1BaseFeeEstimateInertiaMethodId = "0x29eb31ee";
        byte[] inputData = Bytes.FromHexString(getL1BaseFeeEstimateInertiaMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(l1BaseFeeEstimateInertia).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1RewardRate_Always_ReturnsL1RewardRate()
    {
        ulong l1RewardRate = 100;
        _freeArbosState.L1PricingState.PerUnitRewardStorage.Set(l1RewardRate);

        string getL1RewardRateMethodId = "0x8a5b1d28";
        byte[] inputData = Bytes.FromHexString(getL1RewardRateMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(l1RewardRate).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1RewardRecipient_Always_ReturnsL1RewardRecipient()
    {
        Address l1RewardRecipient = new("0x000000000000000000000000000000000000123");
        _freeArbosState.L1PricingState.PayRewardsToStorage.Set(l1RewardRecipient);

        string getL1RewardRecipientMethodId = "0x9e6d7e31";
        byte[] inputData = Bytes.FromHexString(getL1RewardRecipientMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        byte[] expectedResult = new byte[Hash256.Size];
        l1RewardRecipient.Bytes.CopyTo(expectedResult, Hash256.Size - Address.Size);

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1GasPriceEstimate_Always_ReturnsL1GasPriceEstimate()
    {
        ulong l1BaseFeeEstimate = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1BaseFeeEstimate);

        string getL1GasPriceEstimateMethodId = "0x055f362f";
        byte[] inputData = Bytes.FromHexString(getL1GasPriceEstimateMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(l1BaseFeeEstimate).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetCurrentTxL1GasFees_Always_ReturnsCurrentTxL1GasFees()
    {
        ulong posterFee = 100;
        _context = _context.WithPosterFee(posterFee);

        string getCurrentTxL1GasFeesMethodId = "0xc6f7de0e";
        byte[] inputData = Bytes.FromHexString(getCurrentTxL1GasFeesMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(posterFee).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied);
    }

    [Test]
    public void ParsesGetGasBacklog_Always_ReturnsGasBacklog()
    {
        ulong gasBacklog = 100;
        _freeArbosState.L2PricingState.GasBacklogStorage.Set(gasBacklog);

        string getGasBacklogMethodId = "0x1d5b5c20";
        byte[] inputData = Bytes.FromHexString(getGasBacklogMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(gasBacklog).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetPricingInertia_Always_ReturnsPricingInertia()
    {
        ulong pricingInertia = 100;
        _freeArbosState.L2PricingState.PricingInertiaStorage.Set(pricingInertia);

        string getPricingInertiaMethodId = "0x3dfb45b9";
        byte[] inputData = Bytes.FromHexString(getPricingInertiaMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(pricingInertia).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetGasBacklogTolerance_Always_ReturnsGasBacklogTolerance()
    {
        ulong gasBacklogTolerance = 100;
        _freeArbosState.L2PricingState.BacklogToleranceStorage.Set(gasBacklogTolerance);

        string getGasBacklogToleranceMethodId = "0x25754f91";
        byte[] inputData = Bytes.FromHexString(getGasBacklogToleranceMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(gasBacklogTolerance).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1PricingSurplus_ArbosVersionAboveOrEqualToTen_ReturnsL1PricingSurplus()
    {
        _context.WithArbosVersion(ArbosVersion.Ten).WithReleaseSpec();

        Address posterAddress = new("0x000000000000000000000000000000000000123");
        Address payToAddress = new("0x000000000000000000000000000000000000456");
        BatchPoster batchPoster = _freeArbosState.L1PricingState.BatchPosterTable.AddPoster(posterAddress, payToAddress);
        ulong fundsDue = 100;
        batchPoster.SetFundsDueSaturating(new BigInteger(fundsDue));

        UInt256 fundsDueForRewards = 200;
        _freeArbosState.L1PricingState.FundsDueForRewardsStorage.Set(fundsDueForRewards);

        UInt256 fundsAvailable = 150; // make it lower than fundsDue + fundsDueForRewards to test negative value returned
        _freeArbosState.L1PricingState.L1FeesAvailableStorage.Set(fundsAvailable);

        string getL1PricingSurplusMethodId = "0x520acdd7";
        byte[] inputData = Bytes.FromHexString(getL1PricingSurplusMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        BigInteger fundsDueForRefunds = fundsDue;
        BigInteger fundsNeeded = fundsDueForRefunds + (BigInteger)fundsDueForRewards;
        BigInteger expectedL1PricingSurplus = (BigInteger)fundsAvailable - fundsNeeded;

        UInt256 expectedValue = (UInt256)BigInteger.Abs(expectedL1PricingSurplus); // positive value

        // If the original value was negative, convert it to its unsigned representation
        if (expectedL1PricingSurplus < 0)
            expectedValue = ~expectedValue + 1; // twos complement

        result.Should().BeEquivalentTo(expectedValue.ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - 3 * ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1PricingSurplus_ArbosVersionBelowTen_ReturnsL1PricingSurplus()
    {
        _context.WithArbosVersion(ArbosVersion.Nine).WithReleaseSpec();

        Address posterAddress = new("0x000000000000000000000000000000000000123");
        Address payToAddress = new("0x000000000000000000000000000000000000456");
        BatchPoster batchPoster = _freeArbosState.L1PricingState.BatchPosterTable.AddPoster(posterAddress, payToAddress);
        ulong fundsDue = 100;
        batchPoster.SetFundsDueSaturating(new BigInteger(fundsDue));

        UInt256 fundsDueForRewards = 200;
        _freeArbosState.L1PricingState.FundsDueForRewardsStorage.Set(fundsDueForRewards);

        UInt256 fundsAvailable = 500; // make it greater than fundsDue + fundsDueForRewards to test positive value returned
        _context.WorldState.AddToBalanceAndCreateIfNotExists(ArbosAddresses.L1PricerFundsPoolAddress, fundsAvailable, _context.ReleaseSpec);

        string getL1PricingSurplusMethodId = "0x520acdd7";
        byte[] inputData = Bytes.FromHexString(getL1PricingSurplusMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        BigInteger fundsDueForRefunds = fundsDue;
        BigInteger fundsNeeded = fundsDueForRefunds + (BigInteger)fundsDueForRewards;
        BigInteger expectedL1PricingSurplus = (BigInteger)fundsAvailable - fundsNeeded;

        UInt256 expectedValue = (UInt256)BigInteger.Abs(expectedL1PricingSurplus); // positive value

        // If the original value was negative, convert it to its unsigned representation
        if (expectedL1PricingSurplus < 0)
            expectedValue = ~expectedValue + 1; // twos complement

        result.Should().BeEquivalentTo(expectedValue.ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - 2 * ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetPerBatchGasCharge_Always_ReturnsPerBatchGasCharge()
    {
        ulong perBatchGasCharge = 100;
        _freeArbosState.L1PricingState.PerBatchGasCostStorage.Set(perBatchGasCharge);

        string getPerBatchGasChargeMethodId = "0x6ecca45a";
        byte[] inputData = Bytes.FromHexString(getPerBatchGasChargeMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(perBatchGasCharge).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetAmortizedCostCapBips_Always_ReturnsAmortizedCostCapBips()
    {
        ulong amortizedCostCapBips = 100;
        _freeArbosState.L1PricingState.AmortizedCostCapBipsStorage.Set(amortizedCostCapBips);

        string getAmortizedCostCapBipsMethodId = "0x7a7d6beb";
        byte[] inputData = Bytes.FromHexString(getAmortizedCostCapBipsMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(amortizedCostCapBips).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1FeesAvailable_Always_ReturnsL1FeesAvailable()
    {
        UInt256 l1FeesAvailable = 100;
        _freeArbosState.L1PricingState.L1FeesAvailableStorage.Set(l1FeesAvailable);

        string getL1FeesAvailableMethodId = "0x5b39d23c";
        byte[] inputData = Bytes.FromHexString(getL1FeesAvailableMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(l1FeesAvailable.ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1PricingEquilibrationUnits_Always_ReturnsL1PricingEquilibrationUnits()
    {
        UInt256 l1PricingEquilibrationUnits = 100;
        _freeArbosState.L1PricingState.EquilibrationUnitsStorage.Set(l1PricingEquilibrationUnits);

        string getL1PricingEquilibrationUnitsMethodId = "0xad26ce90";
        byte[] inputData = Bytes.FromHexString(getL1PricingEquilibrationUnitsMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(l1PricingEquilibrationUnits.ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetLastL1PricingUpdateTime_Always_ReturnsLastL1PricingUpdateTime()
    {
        ulong lastL1PricingUpdateTime = 100;
        _freeArbosState.L1PricingState.LastUpdateTimeStorage.Set(lastL1PricingUpdateTime);

        string getLastL1PricingUpdateTimeMethodId = "0x138b47b4";
        byte[] inputData = Bytes.FromHexString(getLastL1PricingUpdateTimeMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(lastL1PricingUpdateTime).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1PricingFundsDueForRewards_Always_ReturnsL1PricingFundsDueForRewards()
    {
        UInt256 l1PricingFundsDueForRewards = 100;
        _freeArbosState.L1PricingState.FundsDueForRewardsStorage.Set(l1PricingFundsDueForRewards);

        string getL1PricingFundsDueForRewardsMethodId = "0x963d6002";
        byte[] inputData = Bytes.FromHexString(getL1PricingFundsDueForRewardsMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(l1PricingFundsDueForRewards.ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1PricingUnitsSinceUpdate_Always_ReturnsL1PricingUnitsSinceUpdate()
    {
        ulong l1PricingUnitsSinceUpdate = 100;
        _freeArbosState.L1PricingState.UnitsSinceStorage.Set(l1PricingUnitsSinceUpdate);

        string getL1PricingUnitsSinceUpdateMethodId = "0xeff01306";
        byte[] inputData = Bytes.FromHexString(getL1PricingUnitsSinceUpdateMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(l1PricingUnitsSinceUpdate).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetLastL1PricingSurplus_Always_ReturnsLastL1PricingSurplus()
    {
        ulong lastL1PricingSurplus = 100;
        _freeArbosState.L1PricingState.LastSurplusStorage.Set(lastL1PricingSurplus);

        string getLastL1PricingSurplusMethodId = "0x2987d027";
        byte[] inputData = Bytes.FromHexString(getLastL1PricingSurplusMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().BeEquivalentTo(new UInt256(lastL1PricingSurplus).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }
}
