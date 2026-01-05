// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.GasPolicy;
using Nethermind.Int256;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Evm;

[TestFixture]
public class ArbitrumGasPolicyTests
{
    [Test]
    public void ConsumeSelfDestructGas_Called_SplitsComputationAndStorageAccess()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeSelfDestructGas(ref gas);

        MultiGas accumulated = gas.GetAccumulated();

        accumulated.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        accumulated.Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.SelfDestructEip150 - GasCostOf.WarmStateRead);
    }

    [Test]
    public void Consume_GenericOpcode_TracksComputationGas()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.Consume(ref gas, 3);

        MultiGas accumulated = gas.GetAccumulated();

        accumulated.Get(ResourceKind.Computation).Should().Be(3);
        accumulated.Total.Should().Be(3);
    }

    [Test]
    public void Consume_NonEip150Cost_TracksComputationGas()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.Consume(ref gas, 1000);

        MultiGas accumulated = gas.GetAccumulated();

        accumulated.Get(ResourceKind.Computation).Should().Be(1000);
        accumulated.Total.Should().Be(1000);
    }

    [Test]
    public void Refund_WithChildGasState_AccumulatesChildMultiGas()
    {
        ArbitrumGasPolicy parentGasState = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy childGasState = ArbitrumGasPolicy.FromLong(50_000);

        // Parent consumes some gas
        ArbitrumGasPolicy.Consume(ref parentGasState, 100);

        // Child consumes some gas
        ArbitrumGasPolicy.Consume(ref childGasState, 50);

        // Child tracking is merged to parent
        ArbitrumGasPolicy.Refund(ref parentGasState, childGasState);

        // Parent should have both its own and child's gas
        parentGasState.GetAccumulated().Get(ResourceKind.Computation).Should().Be(150);
    }

    [Test]
    public void Consume_Called_DeductsGasAndTracksAccumulated()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.Consume(ref gas, 5000);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(95_000);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(5000);
    }

    [Test]
    public void ConsumeStorageWrite_SlotCreation_TracksStorageGrowth()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeStorageWrite(ref gas, isSlotCreation: true, Cancun.Instance);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - GasCostOf.SSet);
        gas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(GasCostOf.SSet);
    }

    [Test]
    public void ConsumeStorageWrite_SlotUpdate_TracksStorageAccess()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeStorageWrite(ref gas, isSlotCreation: false, Cancun.Instance);

        long expectedCost = Cancun.Instance.GetSStoreResetCost();
        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - expectedCost);
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be((ulong)expectedCost);
    }

    [Test]
    public void ConsumeCallValueTransfer_Called_TracksComputation()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeCallValueTransfer(ref gas);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - GasCostOf.CallValue);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.CallValue);
    }

    [Test]
    public void ConsumeNewAccountCreation_Called_TracksStorageGrowth()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeNewAccountCreation(ref gas);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - GasCostOf.NewAccount);
        gas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(GasCostOf.NewAccount);
    }

    [Test]
    public void ConsumeLogEmission_Called_SplitsComputationAndHistoryGrowth()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        const long topicCount = 2;
        const long dataSize = 64;

        ArbitrumGasPolicy.ConsumeLogEmission(ref gas, topicCount, dataSize);

        long expectedTotalCost = GasCostOf.Log + topicCount * GasCostOf.LogTopic + dataSize * GasCostOf.LogData;
        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - expectedTotalCost);

        // Base + topic computation portion
        const long logTopicComputationGas = 119; // 375 - 256
        ulong expectedComputation = GasCostOf.Log + topicCount * logTopicComputationGas;
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(expectedComputation);

        // Topic history + data history
        const long logTopicHistoryGas = 256; // 32 bytes * 8 gas/byte
        ulong expectedHistory = topicCount * logTopicHistoryGas + dataSize * GasCostOf.LogData;
        gas.GetAccumulated().Get(ResourceKind.HistoryGrowth).Should().Be(expectedHistory);
    }

    [Test]
    public void ConsumeAccountAccessGas_ColdAccount_TracksStorageAccess()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();

        bool result = ArbitrumGasPolicy.ConsumeAccountAccessGas(
            ref gas, Cancun.Instance, in accessTracker, isTracingAccess: false, TestItem.AddressA);

        result.Should().BeTrue();
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.ColdAccountAccess);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(0);
    }

    [Test]
    public void ConsumeAccountAccessGas_WarmAccount_TracksComputation()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();

        // First access warms up the address
        accessTracker.WarmUp(TestItem.AddressA);

        bool result = ArbitrumGasPolicy.ConsumeAccountAccessGas(
            ref gas, Cancun.Instance, in accessTracker, isTracingAccess: false, TestItem.AddressA);

        result.Should().BeTrue();
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(0);
    }

    [Test]
    public void ConsumeStorageAccessGas_ColdSload_TracksStorageAccess()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();
        StorageCell storageCell = new(TestItem.AddressA, UInt256.One);

        bool result = ArbitrumGasPolicy.ConsumeStorageAccessGas(
            ref gas, in accessTracker, isTracingAccess: false, in storageCell, StorageAccessType.SLOAD, Cancun.Instance);

        result.Should().BeTrue();
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.ColdSLoad);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(0);
    }

    [Test]
    public void ConsumeStorageAccessGas_WarmSload_TracksComputation()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();
        StorageCell storageCell = new(TestItem.AddressA, UInt256.One);

        // First access warms up the storage cell
        accessTracker.WarmUp(in storageCell);

        bool result = ArbitrumGasPolicy.ConsumeStorageAccessGas(
            ref gas, in accessTracker, isTracingAccess: false, in storageCell, StorageAccessType.SLOAD, Cancun.Instance);

        result.Should().BeTrue();
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(0);
    }

    [Test]
    public void ConsumeStorageAccessGas_WarmSstore_NoWarmCharge()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();
        StorageCell storageCell = new(TestItem.AddressA, UInt256.One);

        // First access warms up the storage cell
        accessTracker.WarmUp(in storageCell);

        bool result = ArbitrumGasPolicy.ConsumeStorageAccessGas(
            ref gas, in accessTracker, isTracingAccess: false, in storageCell, StorageAccessType.SSTORE, Cancun.Instance);

        result.Should().BeTrue();
        // SSTORE on warm cell doesn't charge warm read cost
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(0);
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(0);
    }

    [Test]
    public void ConsumeAccountAccessGasWithDelegation_BothAddresses_ChargesBothAccesses()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();

        bool result = ArbitrumGasPolicy.ConsumeAccountAccessGasWithDelegation(
            ref gas, Cancun.Instance, in accessTracker, isTracingAccess: false,
            TestItem.AddressA, TestItem.AddressB);

        result.Should().BeTrue();
        // Both addresses are cold, so 2 * ColdAccountAccess
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(2 * GasCostOf.ColdAccountAccess);
    }

    [Test]
    public void ConsumeAccountAccessGas_OutOfGas_ReturnsFalse()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100); // Not enough for cold access (2600)
        using StackAccessTracker accessTracker = new();

        bool result = ArbitrumGasPolicy.ConsumeAccountAccessGas(
            ref gas, Cancun.Instance, in accessTracker, isTracingAccess: false, TestItem.AddressA);

        result.Should().BeFalse();
    }

    [Test]
    public void CalculateIntrinsicGas_SimpleTransaction_TracksBaseAsComputation()
    {
        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressA)
            .WithData(Array.Empty<byte>())
            .TestObject;

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance);

        intrinsicGas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.Transaction);
        intrinsicGas.GetAccumulated().Get(ResourceKind.L2Calldata).Should().Be(0);
        intrinsicGas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(0);
        intrinsicGas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(0);
    }

    [Test]
    public void CalculateIntrinsicGas_ContractCreation_TracksCreateCostAsComputation()
    {
        Transaction tx = Build.A.Transaction
            .WithTo(null) // Contract creation
            .WithData(Array.Empty<byte>())
            .TestObject;

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance);

        ulong expectedComputation = GasCostOf.Transaction + GasCostOf.TxCreate;
        intrinsicGas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(expectedComputation);
    }

    [Test]
    public void CalculateIntrinsicGas_WithMixedCalldata_TracksAsL2Calldata()
    {
        // Create calldata with 2 zero bytes and 3 non-zero bytes
        byte[] calldata = [0x00, 0x00, 0x01, 0x02, 0x03];
        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressA)
            .WithData(calldata)
            .TestObject;

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance);

        // L2Calldata = (zeroBytes + nonZeroBytes * multiplier) * TxDataZero
        // = (2 + 3 * 4) * 4 = 14 * 4 = 56
        int zeroBytes = 2;
        int nonZeroBytes = 3;
        long txDataNonZeroMultiplier = GasCostOf.TxDataNonZeroMultiplierEip2028; // 4
        long expectedL2Calldata = (zeroBytes + nonZeroBytes * txDataNonZeroMultiplier) * GasCostOf.TxDataZero;
        intrinsicGas.GetAccumulated().Get(ResourceKind.L2Calldata).Should().Be((ulong)expectedL2Calldata);
    }

    [Test]
    public void CalculateIntrinsicGas_ContractWithInitCode_TracksInitCodeAsComputation()
    {
        // 64 bytes of init code = 2 words
        byte[] initCode = new byte[64];
        initCode[0] = 0x60; // PUSH1 to make it non-zero
        Transaction tx = Build.A.Transaction
            .WithTo(null) // Contract creation
            .WithData(initCode)
            .TestObject;

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance);

        // Base + Create + InitCode
        long initCodeWords = (64 + 31) / 32; // = 2
        long initCodeCost = initCodeWords * GasCostOf.InitCodeWord;
        ulong expectedComputation = GasCostOf.Transaction + GasCostOf.TxCreate + (ulong)initCodeCost;
        intrinsicGas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(expectedComputation);
    }

    [Test]
    public void CalculateIntrinsicGas_WithAccessList_TracksAsStorageAccess()
    {
        AccessList accessList = new AccessList.Builder()
            .AddAddress(TestItem.AddressA)
            .AddStorage(UInt256.One)
            .AddStorage(UInt256.MaxValue)
            .AddAddress(TestItem.AddressB)
            .Build();

        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressC)
            .WithData(Array.Empty<byte>())
            .TestObject;
        tx.AccessList = accessList;

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance);

        // 2 addresses * 2400 + 2 storage keys * 1900 = 4800 + 3800 = 8600
        long expectedStorageAccess = 2 * GasCostOf.AccessAccountListEntry + 2 * GasCostOf.AccessStorageListEntry;
        intrinsicGas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be((ulong)expectedStorageAccess);
    }

    [Test]
    public void ConsumeLogEmission_NoTopicsNoData_TracksOnlyBaseComputation()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeLogEmission(ref gas, topicCount: 0, dataSize: 0);

        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.Log);
        gas.GetAccumulated().Get(ResourceKind.HistoryGrowth).Should().Be(0);
    }

    [Test]
    public void ConsumeLogEmission_FourTopicsNoData_TracksMaxTopicCosts()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        const long topicCount = 4;

        ArbitrumGasPolicy.ConsumeLogEmission(ref gas, topicCount, dataSize: 0);

        const long logTopicComputationGas = 119;
        const long logTopicHistoryGas = 256;
        ulong expectedComputation = GasCostOf.Log + topicCount * logTopicComputationGas;
        ulong expectedHistory = topicCount * logTopicHistoryGas;

        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(expectedComputation);
        gas.GetAccumulated().Get(ResourceKind.HistoryGrowth).Should().Be(expectedHistory);
    }

    [Test]
    public void ConsumeLogEmission_OneTopicLargeData_TracksDataAsHistoryGrowth()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        const long topicCount = 1;
        const long dataSize = 1024;

        ArbitrumGasPolicy.ConsumeLogEmission(ref gas, topicCount, dataSize);

        const long logTopicHistoryGas = 256;
        ulong expectedHistory = topicCount * logTopicHistoryGas + dataSize * GasCostOf.LogData;

        gas.GetAccumulated().Get(ResourceKind.HistoryGrowth).Should().Be(expectedHistory);
    }

    [Test]
    public void UpdateGas_InsufficientGas_ReturnsFalse()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100);

        bool result = ArbitrumGasPolicy.UpdateGas(ref gas, 200);

        result.Should().BeFalse();
    }

    [Test]
    public void UpdateGas_SufficientGas_ReturnsTrue()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(1000);

        bool result = ArbitrumGasPolicy.UpdateGas(ref gas, 500);

        result.Should().BeTrue();
        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(500);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(500);
    }

    [Test]
    public void ConsumeStorageWrite_OutOfGas_ReturnsFalse()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100);

        bool result = ArbitrumGasPolicy.ConsumeStorageWrite(ref gas, isSlotCreation: true, Cancun.Instance);

        result.Should().BeFalse();
    }

    [Test]
    public void SetOutOfGas_Called_SetsRemainingToZero()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.SetOutOfGas(ref gas);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(0);
    }

    [Test]
    public void UpdateGasUp_Called_AddsGasBack()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.Consume(ref gas, 50_000);

        ArbitrumGasPolicy.UpdateGasUp(ref gas, 10_000);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(60_000);
    }

    [Test]
    public void Max_TwoStates_ReturnsHigherRemaining()
    {
        ArbitrumGasPolicy gasA = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy gasB = ArbitrumGasPolicy.FromLong(50_000);

        ArbitrumGasPolicy max = ArbitrumGasPolicy.Max(in gasA, in gasB);

        ArbitrumGasPolicy.GetRemainingGas(in max).Should().Be(100_000);
    }

    [Test]
    public void CreateAvailableFromIntrinsic_Called_PreservesAccumulatedBreakdown()
    {
        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressA)
            .WithData(new byte[] { 0x01, 0x02 }) // 2 non-zero bytes
            .TestObject;

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance);
        ArbitrumGasPolicy availableGas = ArbitrumGasPolicy.CreateAvailableFromIntrinsic(100_000, in intrinsicGas);

        // Accumulated breakdown should be preserved
        availableGas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.Transaction);
        availableGas.GetAccumulated().Get(ResourceKind.L2Calldata).Should().BeGreaterThan(0);
    }

    [Test]
    public void ApplyRefund_Called_SetsRefundOnAccumulatedMultiGas()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.Consume(ref gas, 50_000);

        ArbitrumGasPolicy.ApplyRefund(ref gas, 10_000);

        gas.GetAccumulated().Refund.Should().Be(10_000);
        gas.GetAccumulated().Total.Should().Be(50_000);
    }

    [Test]
    public void Refund_ChildDoesNoWork_RetainedAnnihilatesAllocated()
    {
        const long initialGas = 100_000;
        const long callGasTemp = 55_000;

        ArbitrumGasPolicy parent = ArbitrumGasPolicy.FromLong(initialGas);
        // Parent "uses" the gas it's about to give to child (like Nitro's UseMultiGas)
        ArbitrumGasPolicy.UpdateGas(ref parent, callGasTemp);

        // Child frame is created with callGasTemp gas
        ArbitrumGasPolicy child = ArbitrumGasPolicy.FromLong(callGasTemp);
        // Child does nothing - empty execution

        ArbitrumGasPolicy.Refund(ref parent, in child);

        // Retained equals allocation, net usage is zero
        MultiGas total = parent.GetTotalAccumulated();
        total.SingleGas().Should().Be(0UL, "allocated gas should be annihilated by retained");
    }

    /// <summary>
    /// Port of Nitro TestOpCallsMultiGas - validates retained gas tracking.
    /// Nitro: require.Equal(t, callGasTemp, scope.Contract.RetainedMultiGas.Get(multigas.ResourceKindComputation))
    /// </summary>
    [Test]
    public void Refund_ChildDoesNoWork_RetainedEqualsChildInitialGas()
    {
        const long callGasTemp = 55_000;

        ArbitrumGasPolicy parent = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.UpdateGas(ref parent, callGasTemp);

        ArbitrumGasPolicy child = ArbitrumGasPolicy.FromLong(callGasTemp);

        ArbitrumGasPolicy.Refund(ref parent, in child);

        // parent._accumulated.Computation = callGasTemp (from UpdateGas)
        // parent._retained.Computation = callGasTemp (from Refund tracking child._initialGas)
        // GetTotalAccumulated() = _accumulated - _retained
        MultiGas accumulated = parent.GetAccumulated();
        MultiGas total = parent.GetTotalAccumulated();

        accumulated.Get(ResourceKind.Computation).Should().Be((ulong)callGasTemp, "accumulated should equal allocated gas");
        total.Get(ResourceKind.Computation).Should().Be(0UL, "retained should cancel accumulated");
    }

    /// <summary>
    /// Validates that when child does work, only the child's actual usage is counted.
    /// Parent allocates 55,000, child uses 5,000 → net usage = 5,000.
    /// </summary>
    [Test]
    public void Refund_ChildDoesWork_NetUsageEqualsChildWork()
    {
        const long callGasTemp = 55_000;
        const long childWork = 5_000;

        ArbitrumGasPolicy parent = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.UpdateGas(ref parent, callGasTemp);

        ArbitrumGasPolicy child = ArbitrumGasPolicy.FromLong(callGasTemp);
        // Child does some work
        ArbitrumGasPolicy.UpdateGas(ref child, childWork);

        ArbitrumGasPolicy.Refund(ref parent, in child);

        // net = allocated + child_work - retained = 55,000 + 5,000 - 55,000 = 5,000
        MultiGas total = parent.GetTotalAccumulated();
        total.SingleGas().Should().Be((ulong)childWork, "net usage should equal child's actual work");
    }

    /// <summary>
    /// Validates nested calls: parent → child → grandchild.
    /// Each level's retained gas is tracked correctly.
    /// </summary>
    [Test]
    public void Refund_NestedCalls_TracksRetainedAtEachLevel()
    {
        const long parentWork = 10_000;
        const long childAllocation = 50_000;
        const long childWork = 3_000;
        const long grandchildAllocation = 20_000;
        const long grandchildWork = 1_000;

        // Parent does work and allocates to child
        ArbitrumGasPolicy parent = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.UpdateGas(ref parent, parentWork);
        ArbitrumGasPolicy.UpdateGas(ref parent, childAllocation);

        // Child does work and allocates to grandchild
        ArbitrumGasPolicy child = ArbitrumGasPolicy.FromLong(childAllocation);
        ArbitrumGasPolicy.UpdateGas(ref child, childWork);
        ArbitrumGasPolicy.UpdateGas(ref child, grandchildAllocation);

        // Grandchild does work
        ArbitrumGasPolicy grandchild = ArbitrumGasPolicy.FromLong(grandchildAllocation);
        ArbitrumGasPolicy.UpdateGas(ref grandchild, grandchildWork);

        // Unwind the call stack
        ArbitrumGasPolicy.Refund(ref child, in grandchild);
        ArbitrumGasPolicy.Refund(ref parent, in child);

        // Total = parentWork + childWork + grandchildWork
        MultiGas total = parent.GetTotalAccumulated();
        ulong expectedTotal = (ulong)(parentWork + childWork + grandchildWork);
        total.SingleGas().Should().Be(expectedTotal, "nested retained gas should correctly annihilate allocations");
    }

    /// <summary>
    /// Validates FromLong sets _initialGas correctly.
    /// </summary>
    [Test]
    public void FromLong_SetsInitialGas_UsedInRetainedTracking()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(42_000);

        // _initialGas is internal, but we can verify its effect through Refund
        ArbitrumGasPolicy parent = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.UpdateGas(ref parent, 42_000);
        ArbitrumGasPolicy.Refund(ref parent, in gas);

        MultiGas total = parent.GetTotalAccumulated();
        total.SingleGas().Should().Be(0UL, "_initialGas from FromLong should be used in retained tracking");
    }

    /// <summary>
    /// Validates GetTotalAccumulated returns accumulated when no retained gas.
    /// </summary>
    [Test]
    public void GetTotalAccumulated_NoRetained_ReturnsAccumulated()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.UpdateGas(ref gas, 5_000);
        ArbitrumGasPolicy.UpdateGas(ref gas, 3_000);

        MultiGas total = gas.GetTotalAccumulated();

        // No refund happened, so total = accumulated
        total.SingleGas().Should().Be(8_000UL);
        total.Get(ResourceKind.Computation).Should().Be(8_000UL);
    }

    [Test]
    public void ConsumeCodeCopyGas_ExternalCode_CategorizesWordCostAsStorageAccess()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        bool success = ArbitrumGasPolicy.ConsumeDataCopyGas(
            ref gas,
            isExternalCode: true,
            baseCost: 20,
            dataCost: 96);

        success.Should().BeTrue();
        MultiGas accumulated = gas.GetAccumulated();
        accumulated.Get(ResourceKind.Computation).Should().Be(20UL);
        accumulated.Get(ResourceKind.StorageAccess).Should().Be(96UL);
    }

    [Test]
    public void ConsumeCodeCopyGas_InternalCode_CategorizesAllAsComputation()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        bool success = ArbitrumGasPolicy.ConsumeDataCopyGas(
            ref gas,
            isExternalCode: false,
            baseCost: 3,
            dataCost: 96);

        success.Should().BeTrue();
        MultiGas accumulated = gas.GetAccumulated();
        accumulated.Get(ResourceKind.Computation).Should().Be(99UL);
        accumulated.Get(ResourceKind.StorageAccess).Should().Be(0UL);
    }

    [Test]
    public void ConsumeCodeCopyGas_OutOfGas_ReturnsFalse()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(50);

        bool success = ArbitrumGasPolicy.ConsumeDataCopyGas(
            ref gas,
            isExternalCode: true,
            baseCost: 20,
            dataCost: 96);

        success.Should().BeFalse();
    }
}
