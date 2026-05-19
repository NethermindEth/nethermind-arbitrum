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
        gas.Increment(ResourceKind.StorageAccessRead, 12);
        gas.Increment(ResourceKind.StorageGrowth, 13);
        gas.Increment(ResourceKind.L1Calldata, 14);
        gas.Increment(ResourceKind.L2Calldata, 15);
        gas.Increment(ResourceKind.WasmComputation, 16);

        gas.Total.Should().Be(91UL);
        gas.Get(ResourceKind.Computation).Should().Be(10UL);
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(11UL);
        gas.Get(ResourceKind.StorageAccessRead).Should().Be(12UL);
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
        gas.Get(ResourceKind.StorageAccessRead).Should().Be(0UL);
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
        gas.Increment(ResourceKind.StorageAccessRead, 5);
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
        other.Increment(ResourceKind.StorageAccessRead, 8);
        gas.Add(other);
        gas.Total.Should().Be(20UL);

        // Saturating add to MaxValue
        MultiGas maxGas = default;
        maxGas.Increment(ResourceKind.Computation, ulong.MaxValue);
        gas.Add(maxGas);
        gas.Total.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void Default_IsZero_ReturnsTrue()
    {
        MultiGas gas = default;

        gas.SingleGas().Should().Be(0UL);
        gas.IsZero().Should().BeTrue();
    }

    [Test]
    public void IsZero_AfterIncrement_ReturnsFalse()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 100);

        gas.Get(ResourceKind.Computation).Should().Be(100UL);
        gas.SingleGas().Should().Be(100UL);
        gas.IsZero().Should().BeFalse();
    }

    [Test]
    public void SafeSub_Normal_ReturnsResultWithNoUnderflow()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 30);
        gas.Increment(ResourceKind.HistoryGrowth, 40);
        gas.Increment(ResourceKind.StorageAccessRead, 50);

        MultiGas toSubtract = default;
        toSubtract.Increment(ResourceKind.Computation, 10);
        toSubtract.Increment(ResourceKind.HistoryGrowth, 20);

        (MultiGas result, bool underflow) = gas.SafeSub(toSubtract);

        underflow.Should().BeFalse();
        result.Get(ResourceKind.Computation).Should().Be(20UL);
        result.Get(ResourceKind.HistoryGrowth).Should().Be(20UL);
        result.Get(ResourceKind.StorageAccessRead).Should().Be(50UL);
        result.Get(ResourceKind.StorageGrowth).Should().Be(0UL);
        result.Get(ResourceKind.L1Calldata).Should().Be(0UL);
        result.Get(ResourceKind.L2Calldata).Should().Be(0UL);
        result.Get(ResourceKind.WasmComputation).Should().Be(0UL);
        result.SingleGas().Should().Be(90UL);
    }

    [Test]
    public void SafeSub_WhenTotalUnderflows_ReturnsUnderflowTrue()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 10);

        MultiGas toSubtract = default;
        toSubtract.Increment(ResourceKind.Computation, 20);

        (MultiGas result, bool underflow) = gas.SafeSub(toSubtract);

        underflow.Should().BeTrue();
        result.Get(ResourceKind.Computation).Should().Be(0UL);
        result.Total.Should().Be(0UL);
    }

    [Test]
    public void SaturatingSub_Normal_ReturnsCorrectResult()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 30);
        gas.Increment(ResourceKind.HistoryGrowth, 40);
        gas.Increment(ResourceKind.StorageAccessRead, 50);

        MultiGas toSubtract = default;
        toSubtract.Increment(ResourceKind.Computation, 10);
        toSubtract.Increment(ResourceKind.HistoryGrowth, 20);

        MultiGas result = gas.SaturatingSub(toSubtract);

        result.Get(ResourceKind.Computation).Should().Be(20UL);
        result.Get(ResourceKind.HistoryGrowth).Should().Be(20UL);
        result.Get(ResourceKind.StorageAccessRead).Should().Be(50UL);
        result.Total.Should().Be(90UL);
    }

    [Test]
    public void SaturatingSub_Underflow_ClampsToZero()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 10);

        MultiGas toSubtract = default;
        toSubtract.Increment(ResourceKind.Computation, 20);

        MultiGas result = gas.SaturatingSub(toSubtract);

        result.Get(ResourceKind.Computation).Should().Be(0UL);
        result.Total.Should().Be(0UL);
    }

    [Test]
    public void SingleGas_WithRefund_SubtractsRefundFromTotal()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 1000);

        MultiGas withRefund = gas.WithRefund(300);

        withRefund.Total.Should().Be(1000UL);
        withRefund.Refund.Should().Be(300UL);
        withRefund.SingleGas().Should().Be(700UL);
    }

    [Test]
    public void SingleGas_RefundExceedsTotal_ClampsToZero()
    {
        MultiGas gas = default;
        gas.Increment(ResourceKind.Computation, 100);

        MultiGas withRefund = gas.WithRefund(200);

        withRefund.SingleGas().Should().Be(0UL);
    }

    [Test]
    public void IsZero_WithOnlyRefund_ReturnsFalse()
    {
        MultiGas gas = default;
        gas = gas.WithRefund(100);

        gas.IsZero().Should().BeFalse();
    }

    [Test]
    public void CheckResourceKind_ValidKind_DoesNotThrow()
    {
        // All valid resource kinds should not throw
        Action checkUnknown = () => MultiGas.CheckResourceKind(ResourceKind.Unknown);
        Action checkComputation = () => MultiGas.CheckResourceKind(ResourceKind.Computation);
        Action checkHistoryGrowth = () => MultiGas.CheckResourceKind(ResourceKind.HistoryGrowth);
        Action checkStorageAccessRead = () => MultiGas.CheckResourceKind(ResourceKind.StorageAccessRead);
        Action checkStorageAccessWrite = () => MultiGas.CheckResourceKind(ResourceKind.StorageAccessWrite);
        Action checkStorageGrowth = () => MultiGas.CheckResourceKind(ResourceKind.StorageGrowth);
        Action checkL1Calldata = () => MultiGas.CheckResourceKind(ResourceKind.L1Calldata);
        Action checkL2Calldata = () => MultiGas.CheckResourceKind(ResourceKind.L2Calldata);
        Action checkWasmComputation = () => MultiGas.CheckResourceKind(ResourceKind.WasmComputation);

        checkUnknown.Should().NotThrow();
        checkComputation.Should().NotThrow();
        checkHistoryGrowth.Should().NotThrow();
        checkStorageAccessRead.Should().NotThrow();
        checkStorageAccessWrite.Should().NotThrow();
        checkStorageGrowth.Should().NotThrow();
        checkL1Calldata.Should().NotThrow();
        checkL2Calldata.Should().NotThrow();
        checkWasmComputation.Should().NotThrow();
    }

    [Test]
    public void CheckResourceKind_InvalidKind_ThrowsException()
    {
        // Out of range kind should throw
        Action checkOutOfRange = () => MultiGas.CheckResourceKind((ResourceKind)99);
        Action checkNegative = () => MultiGas.CheckResourceKind((ResourceKind)255);

        checkOutOfRange.Should().Throw<ArgumentOutOfRangeException>();
        checkNegative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void Increment_InvalidResourceKind_ThrowsException()
    {
        MultiGas gas = default;

        Action incrementOutOfRange = () => gas.Increment((ResourceKind)99, 100);

        incrementOutOfRange.Should().Throw<ArgumentOutOfRangeException>();
    }
}
