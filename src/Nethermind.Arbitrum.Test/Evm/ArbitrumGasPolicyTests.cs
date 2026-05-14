// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Evm;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Eip2930;
using Nethermind.Core.Specs;
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
    public void SelfDestruct_ColdInheritor_ChargesColdAccountAccess()
    {
        // SELFDESTRUCT with cold inheritor: base cost + cold account access (EIP-2929)
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();

        // Base SELFDESTRUCT cost
        ArbitrumGasPolicy.ConsumeSelfDestructGas(ref gas);
        // Inheritor account access (cold, SelfDestructBeneficiary → full cost to StorageAccess)
        bool result = ArbitrumGasPolicy.ConsumeAccountAccessGas(
            ref gas, Cancun.Instance, in accessTracker, isTracingAccess: false, TestItem.AddressA,
            AccountAccessKind.SelfDestructBeneficiary);

        result.Should().BeTrue();
        MultiGas accumulated = gas.GetAccumulated();
        // SELFDESTRUCT computation: WarmStateRead (from ConsumeSelfDestructGas only)
        // SELFDESTRUCT storage access: (SelfDestructEip150 - WarmStateRead) + full ColdAccountAccess
        accumulated.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        accumulated.Get(ResourceKind.StorageAccess).Should().Be(
            GasCostOf.SelfDestructEip150 - GasCostOf.WarmStateRead + GasCostOf.ColdAccountAccess);
    }

    [Test]
    public void SelfDestruct_WarmInheritor_NoAdditionalCharge()
    {
        // SELFDESTRUCT with warm inheritor: base cost only (SelfDestructBeneficiary skips warm charge)
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();

        // Warm up the inheritor address
        accessTracker.WarmUp(TestItem.AddressA);

        // Base SELFDESTRUCT cost
        ArbitrumGasPolicy.ConsumeSelfDestructGas(ref gas);
        // Inheritor account access (warm, SelfDestructBeneficiary → no warm charge)
        bool result = ArbitrumGasPolicy.ConsumeAccountAccessGas(
            ref gas, Cancun.Instance, in accessTracker, isTracingAccess: false, TestItem.AddressA,
            AccountAccessKind.SelfDestructBeneficiary);

        result.Should().BeTrue();
        MultiGas accumulated = gas.GetAccumulated();
        // SELFDESTRUCT computation: WarmStateRead only (warm inheritor not charged per EIP-2929 SELFDESTRUCT)
        // SELFDESTRUCT storage access: (SelfDestructEip150 - WarmStateRead)
        accumulated.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        accumulated.Get(ResourceKind.StorageAccess).Should().Be(
            GasCostOf.SelfDestructEip150 - GasCostOf.WarmStateRead);
    }

    [Test]
    public void SelfDestruct_ToSelf_ChargesBaseOnly()
    {
        // SELFDESTRUCT where inheritor == executing account (self-destruct to self)
        // The executing account is always warm (tx.to pre-warming per EIP-2929)
        // SelfDestructBeneficiary skips warm charge
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();

        Address selfAddress = TestItem.AddressA;

        // Self-address is warm (as tx.to would be in real execution)
        accessTracker.WarmUp(selfAddress);

        // Base SELFDESTRUCT cost
        ArbitrumGasPolicy.ConsumeSelfDestructGas(ref gas);
        // Inheritor is self (already warm, SelfDestructBeneficiary → no warm charge)
        bool result = ArbitrumGasPolicy.ConsumeAccountAccessGas(
            ref gas, Cancun.Instance, in accessTracker, isTracingAccess: false, selfAddress,
            AccountAccessKind.SelfDestructBeneficiary);

        result.Should().BeTrue();
        MultiGas accumulated = gas.GetAccumulated();
        // SELFDESTRUCT-to-self charges base cost only (no cold access, no warm charge)
        accumulated.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        accumulated.Get(ResourceKind.StorageAccess).Should().Be(
            GasCostOf.SelfDestructEip150 - GasCostOf.WarmStateRead);
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

        ArbitrumGasPolicy.ConsumeStorageWrite<OffFlag, OnFlag>(ref gas, Cancun.Instance);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - GasCostOf.SSet);
        gas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(GasCostOf.SSet);
    }

    [Test]
    public void ConsumeStorageWrite_SlotUpdate_TracksStorageAccess()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeStorageWrite<OffFlag, OffFlag>(ref gas, Cancun.Instance);

        long expectedCost = Cancun.Instance.GasCosts.SStoreResetCost;
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

        ArbitrumGasPolicy.ConsumeNewAccountCreation<OffFlag>(ref gas);

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

        const long expectedTotalCost = GasCostOf.Log + topicCount * GasCostOf.LogTopic + dataSize * GasCostOf.LogData;
        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - expectedTotalCost);

        // Base + topic computation portion
        const ulong expectedComputation = GasCostOf.Log + topicCount * (ulong)ArbitrumGasCostOf.LogTopicComputationGas;
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(expectedComputation);

        // Topic history + data history
        const ulong expectedHistory = topicCount * (ulong)ArbitrumGasCostOf.LogTopicHistoryGas + (ulong)dataSize * GasCostOf.LogData;
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
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
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
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.ColdSLoad - GasCostOf.WarmStateRead);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
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
    public void ConsumeStorageAccessGas_ColdSstore_TracksStorageAccess()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        using StackAccessTracker accessTracker = new();
        StorageCell storageCell = new(TestItem.AddressA, UInt256.One);

        bool result = ArbitrumGasPolicy.ConsumeStorageAccessGas(
            ref gas, in accessTracker, isTracingAccess: false, in storageCell, StorageAccessType.SSTORE, Cancun.Instance);

        result.Should().BeTrue();
        // Cold SSTORE charges ColdSLoad to StorageAccess (EIP-2929)
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(GasCostOf.ColdSLoad);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(0);
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
        gas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(2 * (GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead));
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

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance).Standard;

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

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance).Standard;

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

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance).Standard;

        // L2Calldata = (zeroBytes + nonZeroBytes * multiplier) * TxDataZero
        // = (2 + 3 * 4) * 4 = 14 * 4 = 56
        const int zeroBytes = 2;
        const int nonZeroBytes = 3;
        const long txDataNonZeroMultiplier = GasCostOf.TxDataNonZeroMultiplierEip2028; // 4
        const long expectedL2Calldata = (zeroBytes + nonZeroBytes * txDataNonZeroMultiplier) * GasCostOf.TxDataZero;
        intrinsicGas.GetAccumulated().Get(ResourceKind.L2Calldata).Should().Be(expectedL2Calldata);
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

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance).Standard;

        // Base + Create + InitCode
        const long initCodeWords = (64 + 31) / 32; // = 2
        const long initCodeCost = initCodeWords * GasCostOf.InitCodeWord;
        const ulong expectedComputation = GasCostOf.Transaction + GasCostOf.TxCreate + (ulong)initCodeCost;
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

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance).Standard;

        // 2 addresses * 2400 + 2 storage keys * 1900 = 4800 + 3800 = 8600
        const long expectedStorageAccess = 2 * GasCostOf.AccessAccountListEntry + 2 * GasCostOf.AccessStorageListEntry;
        intrinsicGas.GetAccumulated().Get(ResourceKind.StorageAccess).Should().Be(expectedStorageAccess);
    }

    [Test]
    public void CalculateIntrinsicGas_OnlyNonZeroBytes_TracksL2Calldata()
    {
        // 5 non-zero bytes only
        byte[] calldata = [0x01, 0x02, 0x03, 0x04, 0x05];
        Transaction tx = Build.A.Transaction
            .WithTo(TestItem.AddressA)
            .WithData(calldata)
            .TestObject;

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance).Standard;

        // L2Calldata = nonZeroBytes * TxDataNonZeroMultiplierEip2028 * TxDataZero
        // = 5 * 4 * 4 = 80
        const int nonZeroBytes = 5;
        const long expectedL2Calldata = nonZeroBytes * GasCostOf.TxDataNonZeroMultiplierEip2028 * GasCostOf.TxDataZero;
        intrinsicGas.GetAccumulated().Get(ResourceKind.L2Calldata).Should().Be(expectedL2Calldata);
        intrinsicGas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.Transaction);
    }

    [Test]
    public void CalculateIntrinsicGas_WithAuthorizationList_TracksStorageGrowth()
    {
        // Single authorization entry - EIP-7702 requires TxType.SetCode
        Transaction tx = Build.A.Transaction
            .WithType(TxType.SetCode)
            .WithTo(TestItem.AddressA)
            .WithData([])
            .TestObject;
        tx.AuthorizationList =
        [
            new AuthorizationTuple(1, TestItem.AddressB, 0, new Signature(new byte[64], 0))
        ];

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Prague.Instance).Standard;

        // StorageGrowth = 1 * NewAccount = 25000
        intrinsicGas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(GasCostOf.NewAccount);
        intrinsicGas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.Transaction);
    }

    [Test]
    public void CalculateIntrinsicGas_WithMultipleAuthorizations_TracksStorageGrowth()
    {
        // Multiple authorization entries - EIP-7702 requires TxType.SetCode
        Transaction tx = Build.A.Transaction
            .WithType(TxType.SetCode)
            .WithTo(TestItem.AddressA)
            .WithData([])
            .TestObject;
        tx.AuthorizationList =
        [
            new AuthorizationTuple(1, TestItem.AddressB, 0, new Signature(new byte[64], 0)),
            new AuthorizationTuple(1, TestItem.AddressC, 0, new Signature(new byte[64], 0)),
            new AuthorizationTuple(1, TestItem.AddressD, 0, new Signature(new byte[64], 0))
        ];

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Prague.Instance).Standard;

        // StorageGrowth = 3 * NewAccount = 75000
        const long expectedStorageGrowth = 3 * GasCostOf.NewAccount;
        intrinsicGas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(expectedStorageGrowth);
        intrinsicGas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.Transaction);
    }

    [Test]
    public void CalculateIntrinsicGas_WithEmptyAuthorizationList_TracksZeroStorageGrowth()
    {
        // Edge case: empty authorization list - EIP-7702 requires TxType.SetCode
        Transaction tx = Build.A.Transaction
            .WithType(TxType.SetCode)
            .WithTo(TestItem.AddressA)
            .WithData([])
            .TestObject;
        tx.AuthorizationList = [];

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Prague.Instance).Standard;

        intrinsicGas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(0);
        intrinsicGas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(GasCostOf.Transaction);
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

        const ulong expectedComputation = GasCostOf.Log + topicCount * (ulong)ArbitrumGasCostOf.LogTopicComputationGas;
        const ulong expectedHistory = topicCount * (ulong)ArbitrumGasCostOf.LogTopicHistoryGas;

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

        const ulong expectedHistory = topicCount * (ulong)ArbitrumGasCostOf.LogTopicHistoryGas + (ulong)dataSize * GasCostOf.LogData;

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

        bool result = ArbitrumGasPolicy.ConsumeStorageWrite<OffFlag, OnFlag>(ref gas, Cancun.Instance);

        result.Should().BeFalse();
    }

    [Test]
    public void ApplyRefund_StorageClear_TrackedInMultiGas()
    {
        // When SSTORE clears a slot (non-zero → zero), EIP-3529 provides a refund
        // This refund should be tracked in MultiGas.Refund
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        // Simulate storage write (slot clearing triggers refund calculation at tx end)
        ArbitrumGasPolicy.ConsumeStorageWrite<OffFlag, OffFlag>(ref gas, Cancun.Instance);

        // At the transaction end, apply a calculated refund (EIP-3529: SstoreClearsScheduleRefundEIP3529 = 4800)
        const ulong expectedRefund = 4800;
        ArbitrumGasPolicy.ApplyRefund(ref gas, expectedRefund);

        MultiGas accumulated = gas.GetAccumulated();
        accumulated.Refund.Should().Be(expectedRefund, "SSTORE refund should be tracked in MultiGas.Refund");
        // Verify refund doesn't affect remaining gas (refunds applied post-execution)
        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - Cancun.Instance.GasCosts.SStoreResetCost);
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
            .WithData([0x01, 0x02]) // 2 non-zero bytes
            .TestObject;

        IReleaseSpec releaseSpec = new ArbitrumReleaseSpec();

        ArbitrumGasPolicy intrinsicGas = ArbitrumGasPolicy.CalculateIntrinsicGas(tx, Cancun.Instance).Standard;
        ArbitrumGasPolicy availableGas = ArbitrumGasPolicy.CreateAvailableFromIntrinsic(100_000, in intrinsicGas, releaseSpec);

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
        // Parent "uses" the gas it's about to give to a child
        ArbitrumGasPolicy.UpdateGas(ref parent, callGasTemp);

        // Child frame is created with a callGasTemp gas
        ArbitrumGasPolicy child = ArbitrumGasPolicy.FromLong(callGasTemp);
        // Child does nothing - empty execution

        ArbitrumGasPolicy.Refund(ref parent, in child);

        // Retained equals allocation, net usage is zero
        MultiGas total = parent.GetTotalAccumulated();
        total.SingleGas().Should().Be(0UL, "allocated gas should be annihilated by retained");
    }

    [Test]
    public void Refund_ChildDoesNoWork_RetainedEqualsChildInitialGas()
    {
        const long callGasTemp = 55_000;

        ArbitrumGasPolicy parent = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.UpdateGas(ref parent, callGasTemp);

        ArbitrumGasPolicy child = ArbitrumGasPolicy.FromLong(callGasTemp);

        ArbitrumGasPolicy.Refund(ref parent, in child);

        // parent._accumulated.Computation = callGasTemp (from UpdateGas)
        // parent._retained.Computation = callGasTemp (from Refund tracking child._allocatedByParent)
        // GetTotalAccumulated() = _accumulated - _retained
        MultiGas accumulated = parent.GetAccumulated();
        MultiGas total = parent.GetTotalAccumulated();

        accumulated.Get(ResourceKind.Computation).Should().Be(callGasTemp, "accumulated should equal allocated gas");
        total.Get(ResourceKind.Computation).Should().Be(0UL, "retained should cancel accumulated");
    }

    /// <summary>
    /// Validates that when a child does work, only the child's actual usage is counted.
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
        total.SingleGas().Should().Be(childWork, "net usage should equal child's actual work");
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
        ulong expectedTotal = parentWork + childWork + grandchildWork;
        total.SingleGas().Should().Be(expectedTotal, "nested retained gas should correctly annihilate allocations");
    }

    /// <summary>
    /// Validates FromLong sets _allocatedByParent correctly.
    /// </summary>
    [Test]
    public void FromLong_SetsAllocatedByParent_UsedInRetainedTracking()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(42_000);

        // _allocatedByParent is internal, but we can verify its effect through Refund
        ArbitrumGasPolicy parent = ArbitrumGasPolicy.FromLong(100_000);
        ArbitrumGasPolicy.UpdateGas(ref parent, 42_000);
        ArbitrumGasPolicy.Refund(ref parent, in gas);

        MultiGas total = parent.GetTotalAccumulated();
        total.SingleGas().Should().Be(0UL, "_allocatedByParent from FromLong should be used in retained tracking");
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
    public void ConsumeDataCopyGas_ExternalCode_CategorizesWordCostAsStorageAccess()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeDataCopyGas(
            ref gas,
            isExternalCode: true,
            baseCost: 20,
            dataCost: 96);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - 116);
        MultiGas accumulated = gas.GetAccumulated();
        accumulated.Get(ResourceKind.Computation).Should().Be(20UL);
        accumulated.Get(ResourceKind.StorageAccess).Should().Be(96UL);
    }

    [Test]
    public void ConsumeDataCopyGas_InternalCode_CategorizesAllAsComputation()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeDataCopyGas(
            ref gas,
            isExternalCode: false,
            baseCost: 3,
            dataCost: 96);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - 99);
        MultiGas accumulated = gas.GetAccumulated();
        accumulated.Get(ResourceKind.Computation).Should().Be(99UL);
        accumulated.Get(ResourceKind.StorageAccess).Should().Be(0UL);
    }

    [Test]
    public void ConsumeDataCopyGas_InsufficientGas_GasGoesNegative()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(50);

        ArbitrumGasPolicy.ConsumeDataCopyGas(
            ref gas,
            isExternalCode: true,
            baseCost: 20,
            dataCost: 96);

        // Gas goes negative (like old Consume behavior) - detected later in VM
        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(50 - 116);
    }

    [Test]
    public void ConsumeCodeDeposit_Called_TracksStorageGrowth()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);
        const int codeLength = 500;
        const long codeDepositCost = GasCostOf.CodeDeposit * codeLength; // 200 * 500 = 100_000

        ArbitrumGasPolicy.ConsumeCodeDeposit(ref gas, codeDepositCost);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000 - codeDepositCost);
        gas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(codeDepositCost);
        gas.GetAccumulated().Get(ResourceKind.Computation).Should().Be(0);
    }

    [Test]
    public void ConsumeCodeDeposit_ZeroCost_NoGasCharged()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(100_000);

        ArbitrumGasPolicy.ConsumeCodeDeposit(ref gas, cost: 0);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(100_000);
        gas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(0);
    }

    [Test]
    public void ConsumeCodeDeposit_MaxCodeSize_TracksFullCost()
    {
        ArbitrumGasPolicy gas = ArbitrumGasPolicy.FromLong(10_000_000);
        const int maxCodeSize = 24576; // EIP-170 max code size
        const long codeDepositCost = GasCostOf.CodeDeposit * maxCodeSize; // 200 * 24576 = 4,915,200

        ArbitrumGasPolicy.ConsumeCodeDeposit(ref gas, codeDepositCost);

        ArbitrumGasPolicy.GetRemainingGas(in gas).Should().Be(10_000_000 - codeDepositCost);
        gas.GetAccumulated().Get(ResourceKind.StorageGrowth).Should().Be(codeDepositCost);
    }
}
