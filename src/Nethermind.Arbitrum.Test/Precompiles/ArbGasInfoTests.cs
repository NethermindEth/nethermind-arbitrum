// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Specs.Forks;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbGasInfoTests
{
    [Test]
    public void GetMaxTxGasLimit_AfterArbosV50_Returns32Million()
    {
        // Initialize ArbOS state at version 50
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Forty)
            .WithReleaseSpec();

        // Perform upgrade to v50, which sets PerTxGasLimit
        context.ArbosState.UpgradeArbosVersion(ArbosVersion.Fifty, false, worldState, London.Instance);

        // Get max tx gas limit
        UInt256 maxTxGasLimit = ArbGasInfo.GetMaxTxGasLimit(context);

        // Verify it returns 32M
        maxTxGasLimit.Should().Be(32_000_000, "GetMaxTxGasLimit should return 32M after v50 upgrade");
        maxTxGasLimit.Should().Be(L2PricingState.InitialPerTxGasLimit);
    }

    [Test]
    public void GetMaxBlockGasLimit_Always_ReturnsPerBlockGasLimit()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Get the expected value from the L2 pricing state
        ulong expectedLimit = context.ArbosState.L2PricingState.PerBlockGasLimitStorage.Get();

        // Get max block gas limit via precompile
        UInt256 maxBlockGasLimit = ArbGasInfo.GetMaxBlockGasLimit(context);

        // Verify it matches the storage value
        maxBlockGasLimit.Should().Be(expectedLimit, "GetMaxBlockGasLimit should return the per-block gas limit from storage");
    }

    [Test]
    public void GetGasPricingConstraints_WithNoConstraints_ReturnsEmptyArray()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Get gas pricing constraints (no constraints set initially)
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify it returns an empty array when no constraints are set
        constraints.Should().BeEmpty("No constraints are set initially");
    }

    [Test]
    public void GetGasPricingConstraints_WithSingleConstraint_ReturnsCorrectValues()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Add a single constraint
        ulong expectedTarget = 1_000_000;
        ulong expectedAdjustmentWindow = 3600;
        ulong expectedBacklog = 500_000;

        context.ArbosState.L2PricingState.AddConstraint(expectedTarget, expectedAdjustmentWindow, expectedBacklog);

        // Get gas pricing constraints
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify the returned constraint
        constraints.Should().HaveCount(1);
        constraints[0].Should().HaveCount(3);
        constraints[0][0].Should().Be(expectedTarget, "First element should be target gas per second");
        constraints[0][1].Should().Be(expectedAdjustmentWindow, "Second element should be adjustment window");
        constraints[0][2].Should().Be(expectedBacklog, "Third element should be backlog");
    }

    [Test]
    public void GetGasPricingConstraints_WithMultipleConstraints_ReturnsAllConstraintsInOrder()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Add multiple constraints
        ulong[][] expectedConstraints =
        [
            [1_000_000, 3600, 500_000],
            [2_000_000, 7200, 1_000_000],
            [3_000_000, 1800, 750_000]
        ];

        foreach (ulong[] constraint in expectedConstraints)
            context.ArbosState.L2PricingState.AddConstraint(constraint[0], constraint[1], constraint[2]);

        // Get gas pricing constraints
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify all constraints are returned in order
        constraints.Should().HaveCount(expectedConstraints.Length);

        for (int i = 0; i < expectedConstraints.Length; i++)
        {
            constraints[i].Should().HaveCount(3);
            constraints[i][0].Should().Be(expectedConstraints[i][0], $"Constraint {i} target should match");
            constraints[i][1].Should().Be(expectedConstraints[i][1], $"Constraint {i} adjustment window should match");
            constraints[i][2].Should().Be(expectedConstraints[i][2], $"Constraint {i} backlog should match");
        }
    }

    [Test]
    public void GetGasPricingConstraints_WithZeroValues_HandlesCorrectly()
    {
        // Initialize ArbOS state
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, 1_000_000)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Fifty)
            .WithReleaseSpec();

        // Add constraint with zero values
        context.ArbosState.L2PricingState.AddConstraint(0, 0, 0);

        // Get gas pricing constraints
        ulong[][] constraints = ArbGasInfo.GetGasPricingConstraints(context);

        // Verify the constraint with zeros is returned correctly
        constraints.Should().HaveCount(1);
        constraints[0].Should().Equal([0UL, 0UL, 0UL], "Should handle zero values correctly");
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbGasInfo.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileTestAbiHelpers.GetMethodId("getPricesInWeiWithAggregator(address)"),
            PrecompileTestAbiHelpers.GetMethodId("getPricesInWei()"),
            PrecompileTestAbiHelpers.GetMethodId("getPricesInArbGasWithAggregator(address)"),
            PrecompileTestAbiHelpers.GetMethodId("getPricesInArbGas()"),
            PrecompileTestAbiHelpers.GetMethodId("getGasAccountingParams()"),
            PrecompileTestAbiHelpers.GetMethodId("getMinimumGasPrice()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1BaseFeeEstimate()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1BaseFeeEstimateInertia()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1RewardRate()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1RewardRecipient()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1GasPriceEstimate()"),
            PrecompileTestAbiHelpers.GetMethodId("getCurrentTxL1GasFees()"),
            PrecompileTestAbiHelpers.GetMethodId("getGasBacklog()"),
            PrecompileTestAbiHelpers.GetMethodId("getPricingInertia()"),
            PrecompileTestAbiHelpers.GetMethodId("getGasBacklogTolerance()"),
            PrecompileTestAbiHelpers.GetMethodId("getMaxTxGasLimit()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1PricingSurplus()"),
            PrecompileTestAbiHelpers.GetMethodId("getPerBatchGasCharge()"),
            PrecompileTestAbiHelpers.GetMethodId("getAmortizedCostCapBips()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1FeesAvailable()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1PricingEquilibrationUnits()"),
            PrecompileTestAbiHelpers.GetMethodId("getLastL1PricingUpdateTime()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1PricingFundsDueForRewards()"),
            PrecompileTestAbiHelpers.GetMethodId("getL1PricingUnitsSinceUpdate()"),
            PrecompileTestAbiHelpers.GetMethodId("getLastL1PricingSurplus()"),
            PrecompileTestAbiHelpers.GetMethodId("getMaxBlockGasLimit()"),
            PrecompileTestAbiHelpers.GetMethodId("getGasPricingConstraints()"),
            PrecompileTestAbiHelpers.GetMethodId("getMultiGasBaseFee()"),
            PrecompileTestAbiHelpers.GetMethodId("getMultiGasPricingConstraints()"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbGasInfo.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbGasInfo.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_GasPrices_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("getPricesInWeiWithAggregator(address)").Should().Be(Solgen.ArbGasInfo.Methods.GetPricesInWeiWithAggregator);
        PrecompileTestAbiHelpers.GetMethodId("getPricesInWei()").Should().Be(Solgen.ArbGasInfo.Methods.GetPricesInWei);
        PrecompileTestAbiHelpers.GetMethodId("getPricesInArbGasWithAggregator(address)").Should().Be(Solgen.ArbGasInfo.Methods.GetPricesInArbGasWithAggregator);
        PrecompileTestAbiHelpers.GetMethodId("getPricesInArbGas()").Should().Be(Solgen.ArbGasInfo.Methods.GetPricesInArbGas);
        PrecompileTestAbiHelpers.GetMethodId("getGasAccountingParams()").Should().Be(Solgen.ArbGasInfo.Methods.GetGasAccountingParams);
        PrecompileTestAbiHelpers.GetMethodId("getMinimumGasPrice()").Should().Be(Solgen.ArbGasInfo.Methods.GetMinimumGasPrice);
        PrecompileTestAbiHelpers.GetMethodId("getGasBacklog()").Should().Be(Solgen.ArbGasInfo.Methods.GetGasBacklog);
        PrecompileTestAbiHelpers.GetMethodId("getPricingInertia()").Should().Be(Solgen.ArbGasInfo.Methods.GetPricingInertia);
        PrecompileTestAbiHelpers.GetMethodId("getGasBacklogTolerance()").Should().Be(Solgen.ArbGasInfo.Methods.GetGasBacklogTolerance);
        PrecompileTestAbiHelpers.GetMethodId("getMaxTxGasLimit()").Should().Be(Solgen.ArbGasInfo.Methods.GetMaxTxGasLimit);
        PrecompileTestAbiHelpers.GetMethodId("getMaxBlockGasLimit()").Should().Be(Solgen.ArbGasInfo.Methods.GetMaxBlockGasLimit);
        PrecompileTestAbiHelpers.GetMethodId("getGasPricingConstraints()").Should().Be(Solgen.ArbGasInfo.Methods.GetGasPricingConstraints);
    }

    [Test]
    public void MethodIds_L1Pricing_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("getL1BaseFeeEstimate()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1BaseFeeEstimate);
        PrecompileTestAbiHelpers.GetMethodId("getL1BaseFeeEstimateInertia()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1BaseFeeEstimateInertia);
        PrecompileTestAbiHelpers.GetMethodId("getL1RewardRate()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1RewardRate);
        PrecompileTestAbiHelpers.GetMethodId("getL1RewardRecipient()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1RewardRecipient);
        PrecompileTestAbiHelpers.GetMethodId("getL1GasPriceEstimate()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1GasPriceEstimate);
        PrecompileTestAbiHelpers.GetMethodId("getCurrentTxL1GasFees()").Should().Be(Solgen.ArbGasInfo.Methods.GetCurrentTxL1GasFees);
        PrecompileTestAbiHelpers.GetMethodId("getL1PricingSurplus()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1PricingSurplus);
        PrecompileTestAbiHelpers.GetMethodId("getPerBatchGasCharge()").Should().Be(Solgen.ArbGasInfo.Methods.GetPerBatchGasCharge);
        PrecompileTestAbiHelpers.GetMethodId("getAmortizedCostCapBips()").Should().Be(Solgen.ArbGasInfo.Methods.GetAmortizedCostCapBips);
        PrecompileTestAbiHelpers.GetMethodId("getL1FeesAvailable()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1FeesAvailable);
        PrecompileTestAbiHelpers.GetMethodId("getL1PricingEquilibrationUnits()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1PricingEquilibrationUnits);
        PrecompileTestAbiHelpers.GetMethodId("getLastL1PricingUpdateTime()").Should().Be(Solgen.ArbGasInfo.Methods.GetLastL1PricingUpdateTime);
        PrecompileTestAbiHelpers.GetMethodId("getL1PricingFundsDueForRewards()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1PricingFundsDueForRewards);
        PrecompileTestAbiHelpers.GetMethodId("getL1PricingUnitsSinceUpdate()").Should().Be(Solgen.ArbGasInfo.Methods.GetL1PricingUnitsSinceUpdate);
        PrecompileTestAbiHelpers.GetMethodId("getLastL1PricingSurplus()").Should().Be(Solgen.ArbGasInfo.Methods.GetLastL1PricingSurplus);
    }
}
