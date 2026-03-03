// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Test;
using Nethermind.Evm.State;

namespace Nethermind.Arbitrum.Test.Precompiles;

/// <summary>
/// Tests for precompile context gas tracking.
/// Ported from Nitro's context_test.go
/// </summary>
[TestFixture]
public class PrecompileContextTests
{
    [Test]
    public void BurnMultiGas_MultipleResources_TracksAllDimensions()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable disposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);

        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(worldState, ulong.MaxValue)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.Sixty)
            .WithReleaseSpec();

        // Create MultiGas with multiple resources
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 100);
        gas.Increment(ResourceKind.StorageAccess, 200);
        gas.Increment(ResourceKind.HistoryGrowth, 50);

        gas.Get(ResourceKind.Computation).Should().Be(100);
        gas.Get(ResourceKind.StorageAccess).Should().Be(200);
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(50);
        gas.Get(ResourceKind.Unknown).Should().Be(0);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0);
    }

    [Test]
    public void MultiGas_Increment_AccumulatesCorrectly()
    {
        MultiGas gas = default;

        gas.Increment(ResourceKind.Computation, 100);
        gas.Increment(ResourceKind.Computation, 50);
        gas.Increment(ResourceKind.StorageAccess, 200);
        gas.Increment(ResourceKind.StorageAccess, 100);

        gas.Get(ResourceKind.Computation).Should().Be(150);
        gas.Get(ResourceKind.StorageAccess).Should().Be(300);
    }

    [Test]
    public void MultiGas_Add_CombinesMultiGas()
    {
        MultiGas gas1 = default;
        gas1.Increment(ResourceKind.Computation, 100);
        gas1.Increment(ResourceKind.StorageAccess, 200);

        MultiGas gas2 = default;
        gas2.Increment(ResourceKind.Computation, 50);
        gas2.Increment(ResourceKind.HistoryGrowth, 75);

        gas1.Add(gas2);

        gas1.Get(ResourceKind.Computation).Should().Be(150);
        gas1.Get(ResourceKind.StorageAccess).Should().Be(200);
        gas1.Get(ResourceKind.HistoryGrowth).Should().Be(75);
    }

    [Test]
    public void MultiGas_AllResourceKinds_HandledCorrectly()
    {
        MultiGas gas = default;

        gas.Increment(ResourceKind.Computation, 1);
        gas.Increment(ResourceKind.HistoryGrowth, 2);
        gas.Increment(ResourceKind.StorageAccess, 3);
        gas.Increment(ResourceKind.StorageGrowth, 4);
        gas.Increment(ResourceKind.L1Calldata, 5);
        gas.Increment(ResourceKind.L2Calldata, 6);
        gas.Increment(ResourceKind.WasmComputation, 7);

        gas.Get(ResourceKind.Unknown).Should().Be(0);
        gas.Get(ResourceKind.Computation).Should().Be(1);
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(2);
        gas.Get(ResourceKind.StorageAccess).Should().Be(3);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(4);
        gas.Get(ResourceKind.L1Calldata).Should().Be(5);
        gas.Get(ResourceKind.L2Calldata).Should().Be(6);
        gas.Get(ResourceKind.WasmComputation).Should().Be(7);
    }

    [Test]
    public void MultiGas_Total_SumsAllResources()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 100);
        gas.Increment(ResourceKind.StorageAccess, 200);
        gas.Increment(ResourceKind.HistoryGrowth, 50);

        ulong total = gas.Total;

        total.Should().Be(350);
    }

    [Test]
    public void MultiGas_IsZero_DetectsEmptyAndNonEmpty()
    {
        MultiGas empty = default;
        MultiGas nonEmpty = default;
        nonEmpty.Increment(ResourceKind.Computation, 1);

        empty.IsZero().Should().BeTrue();
        nonEmpty.IsZero().Should().BeFalse();
    }
}
