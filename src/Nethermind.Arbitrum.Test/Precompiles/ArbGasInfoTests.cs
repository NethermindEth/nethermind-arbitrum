// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Abi;
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
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = AbiMetadata.GetAllFunctionDescriptions(Solgen.ArbGasInfo.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileHelper.GetMethodId("getPricesInWeiWithAggregator(address)"),
            PrecompileHelper.GetMethodId("getPricesInWei()"),
            PrecompileHelper.GetMethodId("getPricesInArbGasWithAggregator(address)"),
            PrecompileHelper.GetMethodId("getPricesInArbGas()"),
            PrecompileHelper.GetMethodId("getGasAccountingParams()"),
            PrecompileHelper.GetMethodId("getMinimumGasPrice()"),
            PrecompileHelper.GetMethodId("getL1BaseFeeEstimate()"),
            PrecompileHelper.GetMethodId("getL1BaseFeeEstimateInertia()"),
            PrecompileHelper.GetMethodId("getL1RewardRate()"),
            PrecompileHelper.GetMethodId("getL1RewardRecipient()"),
            PrecompileHelper.GetMethodId("getL1GasPriceEstimate()"),
            PrecompileHelper.GetMethodId("getCurrentTxL1GasFees()"),
            PrecompileHelper.GetMethodId("getGasBacklog()"),
            PrecompileHelper.GetMethodId("getPricingInertia()"),
            PrecompileHelper.GetMethodId("getGasBacklogTolerance()"),
            PrecompileHelper.GetMethodId("getMaxTxGasLimit()"),
            PrecompileHelper.GetMethodId("getL1PricingSurplus()"),
            PrecompileHelper.GetMethodId("getPerBatchGasCharge()"),
            PrecompileHelper.GetMethodId("getAmortizedCostCapBips()"),
            PrecompileHelper.GetMethodId("getL1FeesAvailable()"),
            PrecompileHelper.GetMethodId("getL1PricingEquilibrationUnits()"),
            PrecompileHelper.GetMethodId("getLastL1PricingUpdateTime()"),
            PrecompileHelper.GetMethodId("getL1PricingFundsDueForRewards()"),
            PrecompileHelper.GetMethodId("getL1PricingUnitsSinceUpdate()"),
            PrecompileHelper.GetMethodId("getLastL1PricingSurplus()"),
            PrecompileHelper.GetMethodId("getMaxBlockGasLimit()"),
            PrecompileHelper.GetMethodId("getGasPricingConstraints()"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        AbiMetadata.GetAllEventDescriptions(Solgen.ArbGasInfo.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        AbiMetadata.GetAllErrorDescriptions(Solgen.ArbGasInfo.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_GasPrices_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("getPricesInWeiWithAggregator(address)").Should().Be(0xba9c916eu);
        PrecompileHelper.GetMethodId("getPricesInWei()").Should().Be(0x41b247a8u);
        PrecompileHelper.GetMethodId("getPricesInArbGasWithAggregator(address)").Should().Be(0x7a1ea732u);
        PrecompileHelper.GetMethodId("getPricesInArbGas()").Should().Be(0x02199f34u);
        PrecompileHelper.GetMethodId("getGasAccountingParams()").Should().Be(0x612af178u);
        PrecompileHelper.GetMethodId("getMinimumGasPrice()").Should().Be(0xf918379au);
        PrecompileHelper.GetMethodId("getGasBacklog()").Should().Be(0x1d5b5c20u);
        PrecompileHelper.GetMethodId("getPricingInertia()").Should().Be(0x3dfb45b9u);
        PrecompileHelper.GetMethodId("getGasBacklogTolerance()").Should().Be(0x25754f91u);
        PrecompileHelper.GetMethodId("getMaxTxGasLimit()").Should().Be(0xaae1cd4cu);
        PrecompileHelper.GetMethodId("getMaxBlockGasLimit()").Should().Be(0x0371fdb4u);
        PrecompileHelper.GetMethodId("getGasPricingConstraints()").Should().Be(0x232027d1u);
    }

    [Test]
    public void MethodIds_L1Pricing_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("getL1BaseFeeEstimate()").Should().Be(0xf5d6ded7u);
        PrecompileHelper.GetMethodId("getL1BaseFeeEstimateInertia()").Should().Be(0x29eb31eeu);
        PrecompileHelper.GetMethodId("getL1RewardRate()").Should().Be(0x8a5b1d28u);
        PrecompileHelper.GetMethodId("getL1RewardRecipient()").Should().Be(0x9e6d7e31u);
        PrecompileHelper.GetMethodId("getL1GasPriceEstimate()").Should().Be(0x055f362fu);
        PrecompileHelper.GetMethodId("getCurrentTxL1GasFees()").Should().Be(0xc6f7de0eu);
        PrecompileHelper.GetMethodId("getL1PricingSurplus()").Should().Be(0x520acdd7u);
        PrecompileHelper.GetMethodId("getPerBatchGasCharge()").Should().Be(0x6ecca45au);
        PrecompileHelper.GetMethodId("getAmortizedCostCapBips()").Should().Be(0x7a7d6bebu);
        PrecompileHelper.GetMethodId("getL1FeesAvailable()").Should().Be(0x5b39d23cu);
        PrecompileHelper.GetMethodId("getL1PricingEquilibrationUnits()").Should().Be(0xad26ce90u);
        PrecompileHelper.GetMethodId("getLastL1PricingUpdateTime()").Should().Be(0x138b47b4u);
        PrecompileHelper.GetMethodId("getL1PricingFundsDueForRewards()").Should().Be(0x963d6002u);
        PrecompileHelper.GetMethodId("getL1PricingUnitsSinceUpdate()").Should().Be(0xeff01306u);
        PrecompileHelper.GetMethodId("getLastL1PricingSurplus()").Should().Be(0x2987d027u);
    }
}
