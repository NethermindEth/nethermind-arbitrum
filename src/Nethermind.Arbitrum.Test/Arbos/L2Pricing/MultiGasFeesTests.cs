// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;

namespace Nethermind.Arbitrum.Test.Arbos.L2Pricing;

[TestFixture]
public class MultiGasFeesTests
{
    [Test]
    public void SetAndGet_NextBlockFee_ReturnsCorrectValue()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Enable multi-gas model by adding a constraint
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);

        // Set next-block fees and commit to current block
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.Computation, 100);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead, 200);
        l2Pricing.CommitMultiGasFees();

        // Reading current block fees after commit
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.Computation).Should().Be(new UInt256(100));
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead).Should().Be(new UInt256(200));
    }

    [Test]
    public void CommitNextToCurrent_WhenCalled_PersistsFees()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Enable multi-gas model
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);

        // Set initial next-block fees and commit
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.Computation, 100);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead, 200);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.HistoryGrowth, 300);
        l2Pricing.CommitMultiGasFees();

        // Verify initial commit
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.Computation).Should().Be(new UInt256(100));
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead).Should().Be(new UInt256(200));

        // Update next-block fees to different values and commit again
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.Computation, 150);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead, 250);
        l2Pricing.CommitMultiGasFees();

        // current block fees should now be the new committed values
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.Computation).Should().Be(new UInt256(150));
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead).Should().Be(new UInt256(250));
    }

    [Test]
    public void MultiGasFees_AllResourceKinds_StoreIndependently()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Enable multi-gas model
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);

        // Set different fees for each resource kind and commit
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.Computation, 100);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.HistoryGrowth, 200);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead, 300);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.StorageGrowth, 400);
        // L1Calldata always returns baseFeeWei as fallback
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.L2Calldata, 600);
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.WasmComputation, 700);
        l2Pricing.CommitMultiGasFees();

        // Each resource has its own independent fee (except L1Calldata and Unknown which use fallbacks)
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.Computation).Should().Be(new UInt256(100));
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.HistoryGrowth).Should().Be(new UInt256(200));
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.StorageAccessRead).Should().Be(new UInt256(300));
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.StorageGrowth).Should().Be(new UInt256(400));
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.L2Calldata).Should().Be(new UInt256(600));
        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.WasmComputation).Should().Be(new UInt256(700));
    }

    [Test]
    public void MultiGasFees_LargeValues_HandledCorrectly()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        L2PricingState l2Pricing = context.ArbosState.L2PricingState;

        // Enable multi-gas model
        Dictionary<ResourceKind, ulong> weights = new()
        {
            { ResourceKind.Computation, 1 },
        };
        l2Pricing.AddMultiGasConstraint(7_000_000, 60, 0, weights);

        // Set large UInt256 values
        UInt256 largeValue = UInt256.MaxValue / 2;
        l2Pricing.SetNextBlockMultiGasBaseFee(ResourceKind.Computation, largeValue);
        l2Pricing.CommitMultiGasFees();

        l2Pricing.GetNextBlockMultiGasBaseFee(ResourceKind.Computation).Should().Be(largeValue);
    }
}
