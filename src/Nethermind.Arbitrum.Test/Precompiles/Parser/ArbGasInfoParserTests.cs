using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Evm;
using Nethermind.Logging;
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
using Nethermind.Core.Test.Builders;
using System.Security.Cryptography;
using Nethermind.JsonRpc;
using Nethermind.Arbitrum.Data;
using Nethermind.Core.Test;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

public class ArbGasInfoParserTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;

    private const ulong DefaultGasSupplied = 1_000_000;
    private Block _genesisBlock = null!;
    private PrecompileTestContextBuilder _context = null!;
    private ArbosState _freeArbosState = null!;
    private IDisposable _worldStateScope = null!;
    private IWorldState _worldState = null!;

    private static readonly uint _getPricesInWeiWithAggregatorId = PrecompileHelper.GetMethodId("getPricesInWeiWithAggregator(address)");
    private static readonly uint _getPricesInWeiId = PrecompileHelper.GetMethodId("getPricesInWei()");
    private static readonly uint _getPricesInArbGasWithAggregatorId = PrecompileHelper.GetMethodId("getPricesInArbGasWithAggregator(address)");
    private static readonly uint _getPricesInArbGasId = PrecompileHelper.GetMethodId("getPricesInArbGas()");
    private static readonly uint _getGasAccountingParamsId = PrecompileHelper.GetMethodId("getGasAccountingParams()");
    private static readonly uint _getMinimumGasPriceId = PrecompileHelper.GetMethodId("getMinimumGasPrice()");
    private static readonly uint _getL1BaseFeeEstimateId = PrecompileHelper.GetMethodId("getL1BaseFeeEstimate()");
    private static readonly uint _getL1BaseFeeEstimateInertiaId = PrecompileHelper.GetMethodId("getL1BaseFeeEstimateInertia()");
    private static readonly uint _getL1RewardRateId = PrecompileHelper.GetMethodId("getL1RewardRate()");
    private static readonly uint _getL1RewardRecipientId = PrecompileHelper.GetMethodId("getL1RewardRecipient()");
    private static readonly uint _getL1GasPriceEstimateId = PrecompileHelper.GetMethodId("getL1GasPriceEstimate()");
    private static readonly uint _getCurrentTxL1GasFeesId = PrecompileHelper.GetMethodId("getCurrentTxL1GasFees()");
    private static readonly uint _getGasBacklogId = PrecompileHelper.GetMethodId("getGasBacklog()");
    private static readonly uint _getPricingInertiaId = PrecompileHelper.GetMethodId("getPricingInertia()");
    private static readonly uint _getGasBacklogToleranceId = PrecompileHelper.GetMethodId("getGasBacklogTolerance()");
    private static readonly uint _getL1PricingSurplusId = PrecompileHelper.GetMethodId("getL1PricingSurplus()");
    private static readonly uint _getPerBatchGasChargeId = PrecompileHelper.GetMethodId("getPerBatchGasCharge()");
    private static readonly uint _getAmortizedCostCapBipsId = PrecompileHelper.GetMethodId("getAmortizedCostCapBips()");
    private static readonly uint _getL1FeesAvailableId = PrecompileHelper.GetMethodId("getL1FeesAvailable()");
    private static readonly uint _getL1PricingEquilibrationUnitsId = PrecompileHelper.GetMethodId("getL1PricingEquilibrationUnits()");
    private static readonly uint _getLastL1PricingUpdateTimeId = PrecompileHelper.GetMethodId("getLastL1PricingUpdateTime()");
    private static readonly uint _getL1PricingFundsDueForRewardsId = PrecompileHelper.GetMethodId("getL1PricingFundsDueForRewards()");
    private static readonly uint _getL1PricingUnitsSinceUpdateId = PrecompileHelper.GetMethodId("getL1PricingUnitsSinceUpdate()");
    private static readonly uint _getLastL1PricingSurplusId = PrecompileHelper.GetMethodId("getLastL1PricingSurplus()");

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = _worldState.BeginScope(IWorldState.PreGenesis); // Store the scope

        _genesisBlock = ArbOSInitialization.Create(_worldState);

        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied).WithArbosState();
        _context.ResetGasLeft();

        _freeArbosState = ArbosState.OpenArbosState(_worldState, new ZeroGasBurner(), LimboLogs.Instance.GetClassLogger());
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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getPricesInWeiWithAggregatorId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbGasInfoParser.PrecompileFunctionDescription[_getPricesInWeiWithAggregatorId].AbiFunctionDescription;

        Address aggregator = Address.Zero;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            aggregator
        );

        byte[] result = implementation!(_context, calldata);

        UInt256 expectedWeiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 expectedPerL2Tx = expectedWeiForL1Calldata * ArbGasInfo.AssumedSimpleTxSize;
        UInt256 expectedWeiForL2Storage = l2GasPrice * ArbGasInfo.StorageArbGas;

        byte[] expectedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [expectedPerL2Tx, expectedWeiForL1Calldata, expectedWeiForL2Storage,
            new UInt256(l2GasPrice), UInt256.Zero, new UInt256(l2GasPrice)]
        );

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getPricesInWeiWithAggregatorId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbGasInfoParser.PrecompileFunctionDescription[_getPricesInWeiWithAggregatorId].AbiFunctionDescription;

        Address aggregator = Address.Zero;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            aggregator
        );

        byte[] result = implementation!(_context, calldata);

        UInt256 expectedWeiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 expectedPerL2Tx = expectedWeiForL1Calldata * ArbGasInfo.AssumedSimpleTxSize;
        UInt256 expectedWeiForL2Storage = l2GasPrice * ArbGasInfo.StorageArbGas;

        byte[] expectedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [expectedPerL2Tx, expectedWeiForL1Calldata, expectedWeiForL2Storage,
            new UInt256(l2GasPrice), UInt256.Zero, new UInt256(l2GasPrice)]
        );

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getPricesInWeiId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        UInt256 expectedWeiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 expectedPerL2Tx = expectedWeiForL1Calldata * ArbGasInfo.AssumedSimpleTxSize;
        UInt256 expectedWeiForL2Storage = l2GasPrice * ArbGasInfo.StorageArbGas;
        UInt256 expectedPerArbGasBase = minBaseFeeWei;
        UInt256 expectedPerArbGasCongestion = l2GasPrice - expectedPerArbGasBase;

        AbiFunctionDescription function = ArbGasInfoParser.PrecompileFunctionDescription[_getPricesInWeiId].AbiFunctionDescription;

        byte[] expectedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [expectedPerL2Tx, expectedWeiForL1Calldata, expectedWeiForL2Storage,
            expectedPerArbGasBase, expectedPerArbGasCongestion, new UInt256(l2GasPrice)]
        );

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getPricesInArbGasWithAggregatorId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbGasInfoParser.PrecompileFunctionDescription[_getPricesInArbGasWithAggregatorId].AbiFunctionDescription;

        Address aggregator = Address.Zero;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            aggregator
        );

        byte[] result = implementation!(_context, calldata);

        UInt256 weiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 weiPerL2Tx = weiForL1Calldata * ArbGasInfo.AssumedSimpleTxSize;

        UInt256 expectedGasForL1Calldata = weiForL1Calldata / l2GasPrice;
        UInt256 expectedGasPerL2Tx = weiPerL2Tx / l2GasPrice;

        byte[] expectedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [expectedGasPerL2Tx, expectedGasForL1Calldata, ArbGasInfo.StorageArbGas]
        );

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getPricesInArbGasWithAggregatorId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        AbiFunctionDescription function = ArbGasInfoParser.PrecompileFunctionDescription[_getPricesInArbGasWithAggregatorId].AbiFunctionDescription;

        Address aggregator = Address.Zero;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetCallInfo().Signature,
            aggregator
        );

        byte[] result = implementation!(_context, calldata);

        UInt256 weiForL1Calldata = l1GasPrice * GasCostOf.TxDataNonZeroEip2028;
        UInt256 expectedGasForL1Calldata = weiForL1Calldata / l2GasPrice;
        UInt256 expectedGasPerL2Tx = ArbGasInfo.AssumedSimpleTxSize;

        byte[] expectedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [expectedGasPerL2Tx, expectedGasForL1Calldata, ArbGasInfo.StorageArbGas]
        );

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getPricesInArbGasId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        AbiFunctionDescription function = ArbGasInfoParser.PrecompileFunctionDescription[_getPricesInArbGasId].AbiFunctionDescription;

        byte[] expectedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [UInt256.Zero, UInt256.Zero, ArbGasInfo.StorageArbGas]
        );

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getGasAccountingParamsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        UInt256 gasLimitPerBlockUInt256 = gasLimitPerBlock;

        AbiFunctionDescription function = ArbGasInfoParser.PrecompileFunctionDescription[_getGasAccountingParamsId].AbiFunctionDescription;

        byte[] expectedResult = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            function.GetReturnInfo().Signature,
            [new UInt256(speedLimitPerSecond), gasLimitPerBlockUInt256, gasLimitPerBlockUInt256]
        );

        result.Should().BeEquivalentTo(expectedResult);

        _context.GasLeft.Should().Be(DefaultGasSupplied - 2 * ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetMinimumGasPrice_Always_ReturnsMinGasPrice()
    {
        ulong minBaseFeeWei = 100;
        _freeArbosState.L2PricingState.MinBaseFeeWeiStorage.Set(minBaseFeeWei);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getMinimumGasPriceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(minBaseFeeWei).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1BaseFeeEstimate_Always_ReturnsL1BaseFeeEstimate()
    {
        ulong l1BaseFeeEstimate = 100;
        _freeArbosState.L1PricingState.PricePerUnitStorage.Set(l1BaseFeeEstimate);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1BaseFeeEstimateId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(l1BaseFeeEstimate).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1BaseFeeEstimateInertia_Always_ReturnsL1BaseFeeEstimateInertia()
    {
        ulong l1BaseFeeEstimateInertia = 100;
        _freeArbosState.L1PricingState.InertiaStorage.Set(l1BaseFeeEstimateInertia);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1BaseFeeEstimateInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(l1BaseFeeEstimateInertia).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1RewardRate_Always_ReturnsL1RewardRate()
    {
        ulong l1RewardRate = 100;
        _freeArbosState.L1PricingState.PerUnitRewardStorage.Set(l1RewardRate);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1RewardRateId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(l1RewardRate).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1RewardRecipient_Always_ReturnsL1RewardRecipient()
    {
        Address l1RewardRecipient = new("0x000000000000000000000000000000000000123");
        _freeArbosState.L1PricingState.PayRewardsToStorage.Set(l1RewardRecipient);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1RewardRecipientId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1GasPriceEstimateId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(l1BaseFeeEstimate).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetCurrentTxL1GasFees_Always_ReturnsCurrentTxL1GasFees()
    {
        ulong posterFee = 100;
        _context = _context.WithPosterFee(posterFee);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getCurrentTxL1GasFeesId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(posterFee).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied);
    }

    [Test]
    public void ParsesGetGasBacklog_Always_ReturnsGasBacklog()
    {
        ulong gasBacklog = 100;
        _freeArbosState.L2PricingState.GasBacklogStorage.Set(gasBacklog);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getGasBacklogId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(gasBacklog).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetPricingInertia_Always_ReturnsPricingInertia()
    {
        ulong pricingInertia = 100;
        _freeArbosState.L2PricingState.PricingInertiaStorage.Set(pricingInertia);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getPricingInertiaId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(pricingInertia).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetGasBacklogTolerance_Always_ReturnsGasBacklogTolerance()
    {
        ulong gasBacklogTolerance = 100;
        _freeArbosState.L2PricingState.BacklogToleranceStorage.Set(gasBacklogTolerance);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getGasBacklogToleranceId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1PricingSurplusId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1PricingSurplusId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

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

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getPerBatchGasChargeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(perBatchGasCharge).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetAmortizedCostCapBips_Always_ReturnsAmortizedCostCapBips()
    {
        ulong amortizedCostCapBips = 100;
        _freeArbosState.L1PricingState.AmortizedCostCapBipsStorage.Set(amortizedCostCapBips);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getAmortizedCostCapBipsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(amortizedCostCapBips).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1FeesAvailable_Always_ReturnsL1FeesAvailable()
    {
        UInt256 l1FeesAvailable = 100;
        _freeArbosState.L1PricingState.L1FeesAvailableStorage.Set(l1FeesAvailable);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1FeesAvailableId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(l1FeesAvailable.ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1PricingEquilibrationUnits_Always_ReturnsL1PricingEquilibrationUnits()
    {
        UInt256 l1PricingEquilibrationUnits = 100;
        _freeArbosState.L1PricingState.EquilibrationUnitsStorage.Set(l1PricingEquilibrationUnits);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1PricingEquilibrationUnitsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(l1PricingEquilibrationUnits.ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetLastL1PricingUpdateTime_Always_ReturnsLastL1PricingUpdateTime()
    {
        ulong lastL1PricingUpdateTime = 100;
        _freeArbosState.L1PricingState.LastUpdateTimeStorage.Set(lastL1PricingUpdateTime);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getLastL1PricingUpdateTimeId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(lastL1PricingUpdateTime).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1PricingFundsDueForRewards_Always_ReturnsL1PricingFundsDueForRewards()
    {
        UInt256 l1PricingFundsDueForRewards = 100;
        _freeArbosState.L1PricingState.FundsDueForRewardsStorage.Set(l1PricingFundsDueForRewards);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1PricingFundsDueForRewardsId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(l1PricingFundsDueForRewards.ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetL1PricingUnitsSinceUpdate_Always_ReturnsL1PricingUnitsSinceUpdate()
    {
        ulong l1PricingUnitsSinceUpdate = 100;
        _freeArbosState.L1PricingState.UnitsSinceStorage.Set(l1PricingUnitsSinceUpdate);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getL1PricingUnitsSinceUpdateId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(l1PricingUnitsSinceUpdate).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public void ParsesGetLastL1PricingSurplus_Always_ReturnsLastL1PricingSurplus()
    {
        ulong lastL1PricingSurplus = 100;
        _freeArbosState.L1PricingState.LastSurplusStorage.Set(lastL1PricingSurplus);

        bool exists = ArbGasInfoParser.PrecompileImplementation.TryGetValue(_getLastL1PricingSurplusId, out PrecompileHandler? implementation);
        exists.Should().BeTrue();

        byte[] result = implementation!(_context, []);

        result.Should().BeEquivalentTo(new UInt256(lastL1PricingSurplus).ToBigEndian());

        _context.GasLeft.Should().Be(DefaultGasSupplied - ArbosStorage.StorageReadCost);
    }

    [Test]
    public async Task GetMinimumGasPrice_Always_ConsumesRightAmountOfGas()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce;

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);
        }

        // Calldata to call getMinimumGasPrice() on ArbGasInfo precompile
        byte[] calldata = KeccakHash.ComputeHashBytes("getMinimumGasPrice()"u8)[..4];

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbGasInfoAddress)
            .WithData(calldata)
            .WithValue(0)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(1_000_000)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, 92, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2); // 2 transactions succeeded: internal, contract call

        long txDataCost = 64; // See IntrinsicGasCalculator.Calculate(tx, spec);
        long precompileOutputCost = 3; // 1 word
        long expectedCost = GasCostOf.Transaction + (long)ArbosStorage.StorageReadCost * 2 + txDataCost + precompileOutputCost;
        receipts[1].GasUsed.Should().Be(expectedCost);
    }

    [Test]
    public async Task GetGasAccountingParams_Always_ConsumesRightAmountOfGas()
    {
        ArbitrumRpcTestBlockchain chain = new ArbitrumTestBlockchainBuilder()
            .WithRecording(new FullChainSimulationRecordingFile("./Recordings/1__arbos32_basefee92.jsonl"))
            .Build();

        Hash256 requestId = new(RandomNumberGenerator.GetBytes(Hash256.Size));
        Address sender = FullChainSimulationAccounts.Owner.Address;
        UInt256 nonce;

        using (chain.WorldStateManager.GlobalWorldState.BeginScope(chain.BlockTree.Head!.Header))
        {
            nonce = chain.WorldStateManager.GlobalWorldState.GetNonce(sender);
        }

        // Calldata to call getGasAccountingParams() on ArbGasInfo precompile
        byte[] calldata = KeccakHash.ComputeHashBytes("getGasAccountingParams()"u8)[..4];

        Transaction transaction = Build.A.Transaction
            .WithType(TxType.EIP1559)
            .WithTo(ArbosAddresses.ArbGasInfoAddress)
            .WithData(calldata)
            .WithValue(0)
            .WithMaxFeePerGas(10.GWei())
            .WithGasLimit(1_000_000)
            .WithNonce(nonce)
            .SignedAndResolved(FullChainSimulationAccounts.Owner)
            .TestObject;

        ResultWrapper<MessageResult> result = await chain.Digest(new TestL2Transactions(requestId, 92, sender, transaction));
        result.Result.Should().Be(Result.Success);

        TxReceipt[] receipts = chain.ReceiptStorage.Get(chain.BlockTree.Head!.Hash!);
        receipts.Should().HaveCount(2); // 2 transactions succeeded: internal, contract call

        long txDataCost = 64; // See IntrinsicGasCalculator.Calculate(tx, spec);
        long precompileOutputCost = 9; // 3 words
        long expectedCost = GasCostOf.Transaction + (long)ArbosStorage.StorageReadCost * 3 + txDataCost + precompileOutputCost;
        receipts[1].GasUsed.Should().Be(expectedCost);
    }

    [TearDown]
    public void TearDown()
    {
        _worldStateScope?.Dispose();
    }
}
