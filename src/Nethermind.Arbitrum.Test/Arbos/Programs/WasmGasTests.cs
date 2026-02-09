// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Evm;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Arbos.Programs;

[TestFixture]
public class WasmGasTests
{
    [Test]
    public void WasmLogCost_NoTopicsNoData_ReturnsZeroHistoryGrowth()
    {
        MultiGas gas = WasmGas.WasmLogCost(0, 0);

        gas.Get(ResourceKind.HistoryGrowth).Should().Be(0UL);
        gas.SingleGas().Should().Be(0UL);
    }

    [Test]
    public void WasmLogCost_TwoTopics64ByteData_ReturnsCorrectHistoryGrowth()
    {
        // LogTopicHistoryGas = 256, LogDataGas = 8
        // Expected: 2 * 256 + 64 * 8 = 512 + 512 = 1024
        MultiGas gas = WasmGas.WasmLogCost(2, 64);

        const ulong expected = 2UL * ArbitrumGasCostOf.LogTopicHistoryGas + 64UL * GasCostOf.LogData;
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(expected);
        gas.SingleGas().Should().Be(expected);
    }

    [Test]
    public void WasmLogCost_FourTopicsOnly_ReturnsTopicHistoryGrowth()
    {
        // LogTopicHistoryGas = 256
        // Expected: 4 * 256 = 1024
        MultiGas gas = WasmGas.WasmLogCost(4, 0);

        const ulong expected = 4UL * ArbitrumGasCostOf.LogTopicHistoryGas;
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(expected);
    }

    [Test]
    public void WasmLogCost_DataOnly_ReturnsDataHistoryGrowth()
    {
        // LogDataGas = 8
        // Expected: 100 * 8 = 800
        MultiGas gas = WasmGas.WasmLogCost(0, 100);

        const ulong expected = 100UL * GasCostOf.LogData;
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(expected);
    }

    [Test]
    public void WasmLogCost_OneTopicLargeData_ReturnsDataHistoryGrowth()
    {
        // LogTopicHistoryGas = 256, LogDataGas = 8
        // Expected: 1 * 256 + 1024 * 8 = 256 + 8192 = 8448
        MultiGas gas = WasmGas.WasmLogCost(1, 1024);

        const ulong expected = 1UL * ArbitrumGasCostOf.LogTopicHistoryGas + 1024UL * GasCostOf.LogData;
        gas.Get(ResourceKind.HistoryGrowth).Should().Be(expected);
    }

    // WasmStateLoadCost tests

    [Test]
    public void WasmStateLoadCost_ColdSlot_ReturnsStorageAccessAndComputation()
    {
        using WasmGasTestHelper helper = new();
        StorageCell cell = new(TestItem.AddressA, UInt256.One);

        MultiGas gas = WasmGas.WasmStateLoadCost(helper.VmHost, cell);

        // Cold: StorageAccess = ColdSLoad - WarmStateRead (2100 - 100 = 2000)
        // Computation = WarmStateRead (100)
        const ulong expectedStorageAccess = GasCostOf.ColdSLoad - GasCostOf.WarmStateRead;
        const ulong expectedComputation = GasCostOf.WarmStateRead;

        gas.Get(ResourceKind.StorageAccess).Should().Be(expectedStorageAccess);
        gas.Get(ResourceKind.Computation).Should().Be(expectedComputation);
        gas.SingleGas().Should().Be(expectedStorageAccess + expectedComputation);
    }

    [Test]
    public void WasmStateLoadCost_WarmSlot_ReturnsComputationOnly()
    {
        using WasmGasTestHelper helper = new();
        StorageCell cell = new(TestItem.AddressA, UInt256.One);

        // Pre-warm the slot
        helper.WarmUpSlot(TestItem.AddressA, UInt256.One);

        MultiGas gas = WasmGas.WasmStateLoadCost(helper.VmHost, cell);

        // Warm: Computation only (100)
        gas.Get(ResourceKind.StorageAccess).Should().Be(0UL);
        gas.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        gas.SingleGas().Should().Be(GasCostOf.WarmStateRead);
    }

    // WasmStateStoreCost tests

    [Test]
    public void WasmStateStoreCost_ColdSlotNewValue_ReturnsStorageAccessAndGrowth()
    {
        using WasmGasTestHelper helper = new();
        helper.CreateAccount(TestItem.AddressA);
        StorageCell cell = new(TestItem.AddressA, UInt256.One);
        byte[] newValue = new byte[32];
        newValue[31] = 1; // Non-zero value

        MultiGas gas = WasmGas.WasmStateStoreCost(helper.VmHost, cell, newValue);

        // Cold access + new slot creation:
        // StorageAccess = ColdSLoad (2100)
        // StorageGrowth = SSet (20000)
        const ulong expectedStorageAccess = GasCostOf.ColdSLoad;
        const ulong expectedStorageGrowth = GasCostOf.SSet;

        gas.Get(ResourceKind.StorageAccess).Should().Be(expectedStorageAccess);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(expectedStorageGrowth);
    }

    [Test]
    public void WasmStateStoreCost_WarmSlotNewValue_ReturnsStorageGrowthOnly()
    {
        using WasmGasTestHelper helper = new();
        helper.CreateAccount(TestItem.AddressA);
        StorageCell cell = new(TestItem.AddressA, UInt256.One);

        // Pre-warm the slot
        helper.WarmUpSlot(TestItem.AddressA, UInt256.One);

        byte[] newValue = new byte[32];
        newValue[31] = 1; // Non-zero value

        MultiGas gas = WasmGas.WasmStateStoreCost(helper.VmHost, cell, newValue);

        // Warm + new slot creation:
        // StorageAccess = 0 (warm)
        // StorageGrowth = SSet (20000)
        gas.Get(ResourceKind.StorageAccess).Should().Be(0UL);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(GasCostOf.SSet);
    }

    [Test]
    public void WasmStateStoreCost_SameValue_ReturnsComputationOnly()
    {
        using WasmGasTestHelper helper = new();
        helper.CreateAccount(TestItem.AddressA);
        StorageCell cell = new(TestItem.AddressA, UInt256.One);

        // Set initial value
        byte[] initialValue = new byte[32];
        initialValue[31] = 5;
        helper.SetStorageValue(TestItem.AddressA, UInt256.One, initialValue);

        // Pre-warm the slot
        helper.WarmUpSlot(TestItem.AddressA, UInt256.One);

        // Try to store the same value
        MultiGas gas = WasmGas.WasmStateStoreCost(helper.VmHost, cell, initialValue);

        // Same value on warm slot: Computation = WarmStateRead (100)
        gas.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        gas.Get(ResourceKind.StorageAccess).Should().Be(0UL);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0UL);
    }

    // WasmAccountTouchCost tests

    [Test]
    public void WasmAccountTouchCost_ColdAccountNoCode_ReturnsStorageAccessAndComputation()
    {
        using WasmGasTestHelper helper = new();

        MultiGas gas = WasmGas.WasmAccountTouchCost(helper.VmHost, TestItem.AddressA, withCode: false);

        // Cold account: StorageAccess = ColdAccountAccess - WarmStateRead (2600 - 100 = 2500)
        // Computation = WarmStateRead (100)
        const ulong expectedStorageAccess = GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead;
        const ulong expectedComputation = GasCostOf.WarmStateRead;

        gas.Get(ResourceKind.StorageAccess).Should().Be(expectedStorageAccess);
        gas.Get(ResourceKind.Computation).Should().Be(expectedComputation);
        gas.SingleGas().Should().Be(expectedStorageAccess + expectedComputation);
    }

    [Test]
    public void WasmAccountTouchCost_WarmAccountNoCode_ReturnsComputationOnly()
    {
        using WasmGasTestHelper helper = new();

        // Pre-warm the address
        helper.WarmUpAddress(TestItem.AddressA);

        MultiGas gas = WasmGas.WasmAccountTouchCost(helper.VmHost, TestItem.AddressA, withCode: false);

        // Warm account: Computation only (100)
        gas.Get(ResourceKind.StorageAccess).Should().Be(0UL);
        gas.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
    }

    [Test]
    public void WasmAccountTouchCost_WithCode_AddsCodeAccessCost()
    {
        using WasmGasTestHelper helper = new();

        // Pre-warm to isolate code access cost
        helper.WarmUpAddress(TestItem.AddressA);

        MultiGas gas = WasmGas.WasmAccountTouchCost(helper.VmHost, TestItem.AddressA, withCode: true);

        // MaxCodeSize = 24576 (Cancun)
        // Code access: (MaxCodeSize / 24576) * ExtCodeEip150 = 1 * 700 = 700
        long maxCodeSize = Cancun.Instance.MaxCodeSize;
        ulong expectedCodeAccessGas = (ulong)maxCodeSize / 24576 * GasCostOf.ExtCodeEip150;

        gas.Get(ResourceKind.StorageAccess).Should().Be(expectedCodeAccessGas);
        gas.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
    }

    [Test]
    public void WasmAccountTouchCost_ColdAccountWithCode_CombinesBothCosts()
    {
        using WasmGasTestHelper helper = new();

        MultiGas gas = WasmGas.WasmAccountTouchCost(helper.VmHost, TestItem.AddressA, withCode: true);

        // Cold + code:
        // Code access: (MaxCodeSize / 24576) * ExtCodeEip150 = 700
        // Cold account: ColdAccountAccess - WarmStateRead = 2500
        // Total StorageAccess = 700 + 2500 = 3200
        long maxCodeSize = Cancun.Instance.MaxCodeSize;
        ulong codeAccessGas = (ulong)maxCodeSize / 24576 * GasCostOf.ExtCodeEip150;
        const ulong coldAccountGas = GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead;

        gas.Get(ResourceKind.StorageAccess).Should().Be(codeAccessGas + coldAccountGas);
        gas.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
    }

    // WasmCallCost tests

    [Test]
    public void WasmCallCost_ColdContractNoValue_ReturnsStorageAccessAndComputation()
    {
        using WasmGasTestHelper helper = new();

        (MultiGas gas, bool outOfGas) = WasmGas.WasmCallCost(helper.VmHost, TestItem.AddressA, hasValue: false, gasLeft: 100_000);

        // Cold contract: StorageAccess = ColdAccountAccess - WarmStateRead (2500)
        // Computation = WarmStateRead (100)
        const ulong expectedStorageAccess = GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead;
        const ulong expectedComputation = GasCostOf.WarmStateRead;

        outOfGas.Should().BeFalse();
        gas.Get(ResourceKind.StorageAccess).Should().Be(expectedStorageAccess);
        gas.Get(ResourceKind.Computation).Should().Be(expectedComputation);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0UL);
    }

    [Test]
    public void WasmCallCost_WarmContractNoValue_ReturnsComputationOnly()
    {
        using WasmGasTestHelper helper = new();

        // Pre-warm the contract address
        helper.WarmUpAddress(TestItem.AddressA);

        (MultiGas gas, bool outOfGas) = WasmGas.WasmCallCost(helper.VmHost, TestItem.AddressA, hasValue: false, gasLeft: 100_000);

        // Warm: Computation only (100)
        outOfGas.Should().BeFalse();
        gas.Get(ResourceKind.StorageAccess).Should().Be(0UL);
        gas.Get(ResourceKind.Computation).Should().Be(GasCostOf.WarmStateRead);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0UL);
    }

    [Test]
    public void WasmCallCost_ColdContractWithValueToNewAccount_ReturnsStorageGrowth()
    {
        using WasmGasTestHelper helper = new();

        // Account doesn't exist, so value transfer creates it
        (MultiGas gas, bool outOfGas) = WasmGas.WasmCallCost(helper.VmHost, TestItem.AddressA, hasValue: true, gasLeft: 100_000);

        // Cold + new account + value transfer:
        // StorageAccess = ColdAccountAccess - WarmStateRead = 2500
        // StorageGrowth = NewAccount = 25000
        // Computation = WarmStateRead + CallValue = 100 + 9000 = 9100
        const ulong expectedStorageAccess = GasCostOf.ColdAccountAccess - GasCostOf.WarmStateRead;
        const ulong expectedStorageGrowth = GasCostOf.NewAccount;
        const ulong expectedComputation = GasCostOf.WarmStateRead + GasCostOf.CallValue;

        outOfGas.Should().BeFalse();
        gas.Get(ResourceKind.StorageAccess).Should().Be(expectedStorageAccess);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(expectedStorageGrowth);
        gas.Get(ResourceKind.Computation).Should().Be(expectedComputation);
    }

    [Test]
    public void WasmCallCost_WarmContractWithValueToExistingAccount_NoStorageGrowth()
    {
        using WasmGasTestHelper helper = new();

        // Create and warm up the account
        helper.CreateAccount(TestItem.AddressA, 100);
        helper.WarmUpAddress(TestItem.AddressA);

        (MultiGas gas, bool outOfGas) = WasmGas.WasmCallCost(helper.VmHost, TestItem.AddressA, hasValue: true, gasLeft: 100_000);

        // Warm + existing account + value transfer:
        // No StorageAccess (warm)
        // No StorageGrowth (an account exists)
        // Computation = WarmStateRead + CallValue = 100 + 9000 = 9100
        const ulong expectedComputation = GasCostOf.WarmStateRead + GasCostOf.CallValue;

        outOfGas.Should().BeFalse();
        gas.Get(ResourceKind.StorageAccess).Should().Be(0UL);
        gas.Get(ResourceKind.StorageGrowth).Should().Be(0UL);
        gas.Get(ResourceKind.Computation).Should().Be(expectedComputation);
    }

    [Test]
    public void WasmCallCost_InsufficientGas_ReturnsOutOfGas()
    {
        using WasmGasTestHelper helper = new();

        // Cold account + value to a new account + value transfer:
        // 2,600 (cold) + 25,000 (new account) + 9,000 (value transfer) = 36,600
        // Provide only gas 100
        (MultiGas _, bool outOfGas) = WasmGas.WasmCallCost(helper.VmHost, TestItem.AddressA, hasValue: true, gasLeft: 100);

        outOfGas.Should().BeTrue();
    }
}
