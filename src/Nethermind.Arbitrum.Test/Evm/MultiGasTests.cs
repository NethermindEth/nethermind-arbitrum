// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Evm;

namespace Nethermind.Arbitrum.Test.Evm;

[TestFixture]
public class MultiGasTests
{
    [Test]
    public void Increment_Computation_SetsValueAndTotal()
    {
        MultiGas gas = default;

        gas.Increment(ResourceKind.Computation, 100);

        gas.Get(ResourceKind.Computation).Should().Be(100UL);
        gas.Total.Should().Be(100UL);
    }

    [Test]
    public void Increment_MultipleKinds_SetsAllCorrectly()
    {
        MultiGas gas = default;

        gas.Increment(ResourceKind.Computation, 10);
        gas.Increment(ResourceKind.HistoryGrowth, 11);
        gas.Increment(ResourceKind.StorageAccess, 12);
        gas.Increment(ResourceKind.StorageGrowth, 13);
        gas.Increment(ResourceKind.L1Calldata, 14);
        gas.Increment(ResourceKind.L2Calldata, 15);
        gas.Increment(ResourceKind.WasmComputation, 16);

        gas.Total.Should().Be(91UL);
        gas.Get(ResourceKind.Computation).Should().Be(10UL);
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(11UL);
        gas.Get(ResourceKind.StorageAccess).Should().Be(12UL);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(13UL);
        gas.Get(ResourceKind.L1Calldata).Should().Be(14UL);
        gas.Get(ResourceKind.L2Calldata).Should().Be(15UL);
        gas.Get(ResourceKind.WasmComputation).Should().Be(16UL);
    }

    [Test]
    public void Increment_SameDimensionTwice_AccumulatesValue()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 10);

        gas.Increment(ResourceKind.Computation, 11);

        gas.Get(ResourceKind.Computation).Should().Be(21UL);
        gas.Total.Should().Be(21UL);
    }

    [Test]
    public void Increment_KindOverflow_ClampsToMaxValue()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, ulong.MaxValue);

        gas.Increment(ResourceKind.Computation, 1);

        gas.Get(ResourceKind.Computation).Should().Be(ulong.MaxValue);
        gas.Total.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void Increment_TotalOverflowOnly_ClampsTotal()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, ulong.MaxValue);

        gas.Increment(ResourceKind.HistoryGrowth, 1);

        gas.Get(ResourceKind.Computation).Should().Be(ulong.MaxValue);
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(1UL);
        gas.Total.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void Add_DifferentDimensions_MergesBothValues()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 10);

        MultiGas other = default;
        other.Increment(ResourceKind.HistoryGrowth, 20);

        gas.Add(other);

        gas.Get(ResourceKind.Computation).Should().Be(10UL);
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(20UL);
        gas.Get(ResourceKind.StorageAccess).Should().Be(0UL);
        gas.Total.Should().Be(30UL);
    }

    [Test]
    public void Add_KindOverflow_ClampsToMaxValue()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, ulong.MaxValue);

        MultiGas other = default;
        other.Increment(ResourceKind.Computation, 1);

        gas.Add(other);

        gas.Get(ResourceKind.Computation).Should().Be(ulong.MaxValue);
        gas.Total.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void Add_TotalOverflow_ClampsTotal()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, ulong.MaxValue);

        MultiGas other = default;
        other.Increment(ResourceKind.HistoryGrowth, 1);

        gas.Add(other);

        gas.Get(ResourceKind.Computation).Should().Be(ulong.MaxValue);
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(1UL);
        gas.Total.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void WithRefund_SetsRefundValue_ReturnsCopyWithRefund()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 1000);

        MultiGas withRefund = gas.WithRefund(500);

        withRefund.Refund.Should().Be(500UL);
        withRefund.Total.Should().Be(1000UL);
        withRefund.Get(ResourceKind.Computation).Should().Be(1000UL);
        gas.Refund.Should().Be(0UL); // Original unchanged (copy semantics)
    }

    [Test]
    public void Add_WithRefund_MergesRefundValues()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 100);
        gas = gas.WithRefund(50);

        MultiGas other = default;
        other.Increment(ResourceKind.HistoryGrowth, 200);
        other = other.WithRefund(30);

        gas.Add(other);

        gas.Refund.Should().Be(80UL);
        gas.Total.Should().Be(300UL);
    }

    [Test]
    public void Get_InvalidResourceKind_ThrowsException()
    {
        MultiGas gas = default;

        // Out of range kind should throw (index >= NumResourceKinds which is 8)
        Action getOutOfRange = () => gas.Get((ResourceKind)99);

        getOutOfRange.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Get_UnknownResourceKind_ReturnsZero()
    {
        MultiGas gas = default;

        // In C# implementation, Unknown (0) is a valid index - returns 0 from uninitialized buffer
        ulong value = gas.Get(ResourceKind.Unknown);

        value.Should().Be(0UL);
    }

    [Test]
    public void Total_MultipleKinds_SumsAllKinds()
    {
        MultiGas gas = default;

        gas.Increment(ResourceKind.Computation, 21);
        gas.Increment(ResourceKind.HistoryGrowth, 15);
        gas.Increment(ResourceKind.StorageAccess, 5);
        gas.Increment(ResourceKind.StorageGrowth, 6);
        gas.Increment(ResourceKind.L1Calldata, 7);
        gas.Increment(ResourceKind.L2Calldata, 8);
        gas.Increment(ResourceKind.WasmComputation, 9);

        gas.Total.Should().Be(71UL);
    }

    [Test]
    public void Operations_Sequential_MaintainsTotal()
    {
        // Start with zero gas
        MultiGas gas = default;
        gas.Total.Should().Be(0UL);

        // Increment computation by 5
        gas.Increment(ResourceKind.Computation, 5);
        gas.Total.Should().Be(5UL);

        // Increment again by 7 (total should be 12)
        gas.Increment(ResourceKind.HistoryGrowth, 7);
        gas.Total.Should().Be(12UL);

        // Add another MultiGas with 8 (total should be 20)
        MultiGas other = default;
        other.Increment(ResourceKind.StorageAccess, 8);
        gas.Add(other);
        gas.Total.Should().Be(20UL);

        // Saturating add to MaxValue
        MultiGas maxGas = default;
        maxGas.Increment(ResourceKind.Computation, ulong.MaxValue);
        gas.Add(maxGas);
        gas.Total.Should().Be(ulong.MaxValue);
    }
}
