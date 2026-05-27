// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Programs;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Arbitrum.Test.Arbos.Stylus.Infrastructure;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Specs.Forks;

namespace Nethermind.Arbitrum.Test.Arbos.Programs;

[TestFixture]
public class StylusEvmApiTests
{
    private static readonly Address ActingAddress = TestItem.AddressB;
    private static readonly StylusMemoryModel NoOpMemoryModel = new(0, 0);

    [Test]
    public void GetBytes32_EmptySlot_ReturnsZeroPadded()
    {
        using TestStylusVm helper = new();
        helper.CreateAccount(ActingAddress);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = UInt256.One.ToBigEndian();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.GetBytes32, input);

        response.Result.Should().BeEquivalentTo(new byte[32]);
        response.GasCost.Should().BeGreaterThan(0);
    }

    [Test]
    public void GetBytes32_SlotWithValue_ReturnsStoredValue()
    {
        using TestStylusVm helper = new();
        helper.CreateAccount(ActingAddress);
        byte[] storedValue = new byte[32];
        storedValue[31] = 42;
        helper.SetStorageValue(ActingAddress, UInt256.One, storedValue);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = UInt256.One.ToBigEndian();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.GetBytes32, input);

        response.Result.Should().BeEquivalentTo(storedValue);
    }

    [Test]
    public void SetTrieSlots_SingleSlot_WritesToState()
    {
        using TestStylusVm helper = new();
        helper.CreateAccount(ActingAddress);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        UInt256 index = UInt256.One;
        byte[] value = new byte[32];
        value[31] = 99;
        byte[] input = EncodeSetTrieSlots(1_000_000, (index, value));
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.SetTrieSlots, input);

        response.Result[0].Should().Be((byte)StylusApiStatus.Success);

        byte[] readInput = index.ToBigEndian();
        StylusEvmResponse readResponse = api.Handle(StylusEvmRequestType.GetBytes32, readInput);
        readResponse.Result.Should().BeEquivalentTo(value);
    }

    [Test]
    public void SetTrieSlots_InsufficientGas_ReturnsFailure()
    {
        // Default test ArbOS version is 40 (< 50), so insufficient gas returns Failure
        using TestStylusVm helper = new();
        helper.CreateAccount(ActingAddress);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] value = new byte[32];
        value[31] = 1;
        byte[] input = EncodeSetTrieSlots(1, (UInt256.One, value));
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.SetTrieSlots, input);

        response.Result[0].Should().Be((byte)StylusApiStatus.Failure);
    }

    [Test]
    public void SetTrieSlots_MultipleSlots_WritesAll()
    {
        using TestStylusVm helper = new();
        helper.CreateAccount(ActingAddress);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] value1 = new byte[32];
        value1[31] = 10;
        byte[] value2 = new byte[32];
        value2[31] = 20;
        byte[] input = EncodeSetTrieSlots(1_000_000, (UInt256.One, value1), (new UInt256(2), value2));
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.SetTrieSlots, input);

        response.Result[0].Should().Be((byte)StylusApiStatus.Success);

        StylusEvmResponse read1 = api.Handle(StylusEvmRequestType.GetBytes32, UInt256.One.ToBigEndian());
        read1.Result.Should().BeEquivalentTo(value1);

        StylusEvmResponse read2 = api.Handle(StylusEvmRequestType.GetBytes32, new UInt256(2).ToBigEndian());
        read2.Result.Should().BeEquivalentTo(value2);
    }

    [Test]
    public void GetTransientBytes32_EmptySlot_ReturnsZeroPadded()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = UInt256.One.ToBigEndian();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.GetTransientBytes32, input);

        response.Result.Should().BeEquivalentTo(new byte[32]);
        response.GasCost.Should().Be(0UL);
    }

    [Test]
    public void GetTransientBytes32_AfterSet_ReturnsStoredValue()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] value = new byte[32];
        value[31] = 77;
        byte[] setInput = EncodeTransientSet(UInt256.One, value);
        api.Handle(StylusEvmRequestType.SetTransientBytes32, setInput);

        byte[] getInput = UInt256.One.ToBigEndian();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.GetTransientBytes32, getInput);

        response.Result.Should().BeEquivalentTo(value);
    }

    [Test]
    public void SetTransientBytes32_WritesValue_ReturnsSuccess()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] value = new byte[32];
        value[31] = 55;
        byte[] input = EncodeTransientSet(UInt256.One, value);
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.SetTransientBytes32, input);

        response.Result[0].Should().Be((byte)StylusApiStatus.Success);
        response.GasCost.Should().Be(0UL);
    }

    [Test]
    public void AccountBalance_NonExistentAccount_ReturnsZero()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = TestItem.AddressA.Bytes.ToArray();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AccountBalance, input);

        response.Result.Should().BeEquivalentTo(UInt256.Zero.ToBigEndian());
        response.GasCost.Should().BeGreaterThan(0);
    }

    [Test]
    public void AccountBalance_AccountWithBalance_ReturnsBalance()
    {
        using TestStylusVm helper = new();
        UInt256 balance = 1_000_000;
        helper.CreateAccount(TestItem.AddressA, balance);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = TestItem.AddressA.Bytes.ToArray();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AccountBalance, input);

        response.Result.Should().BeEquivalentTo(balance.ToBigEndian());
    }

    [Test]
    public void AccountCode_AccountWithCode_ReturnsCodeInRawData()
    {
        using TestStylusVm helper = new();
        helper.CreateAccount(TestItem.AddressA);
        byte[] code = [0x60, 0x00, 0x60, 0x00, 0xFD];
        ValueHash256 codeHash = ValueKeccak.Compute(code);
        helper.WorldState.InsertCode(TestItem.AddressA, codeHash, code, Cancun.Instance);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = EncodeAccountCode(TestItem.AddressA, 1_000_000);
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AccountCode, input);

        response.RawData.Should().BeEquivalentTo(code);
        response.GasCost.Should().BeGreaterThan(0);
    }

    [Test]
    public void AccountCode_InsufficientGas_ReturnsEmpty()
    {
        using TestStylusVm helper = new();
        helper.CreateAccount(TestItem.AddressA);
        byte[] code = [0x60, 0x00];
        ValueHash256 codeHash = ValueKeccak.Compute(code);
        helper.WorldState.InsertCode(TestItem.AddressA, codeHash, code, Cancun.Instance);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = EncodeAccountCode(TestItem.AddressA, 0);
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AccountCode, input);

        response.Result.Should().BeEmpty();
        response.RawData.Should().BeEmpty();
    }

    [Test]
    public void AccountCode_NonExistentAccount_ReturnsEmpty()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = EncodeAccountCode(TestItem.AddressA, 1_000_000);
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AccountCode, input);

        response.RawData.Should().BeEmpty();
    }

    [Test]
    public void AccountCodeHash_NonExistentAccount_ReturnsZeroHash()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, Address.Zero, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = TestItem.AddressA.Bytes.ToArray();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AccountCodeHash, input);

        response.Result.Should().BeEquivalentTo(new byte[32]);
    }

    [Test]
    public void AccountCodeHash_ExistingEmptyAccount_ReturnsEmptyCodeHash()
    {
        using TestStylusVm helper = new();
        helper.CreateAccount(TestItem.AddressA);
        using StylusEvmApi api = new(helper.VmHost, Address.Zero, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = TestItem.AddressA.Bytes.ToArray();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AccountCodeHash, input);

        response.Result.Should().BeEquivalentTo(ValueKeccak.OfAnEmptyString.BytesAsSpan.ToArray());
    }

    [Test]
    public void AccountCodeHash_ExistingContractAccount_ReturnsCodeHash()
    {
        using TestStylusVm helper = new();
        helper.CreateAccount(TestItem.AddressA);
        byte[] code = [0x60, 0x00, 0x60, 0x00, 0xFD];
        ValueHash256 codeHash = ValueKeccak.Compute(code);
        helper.WorldState.InsertCode(TestItem.AddressA, codeHash, code, Cancun.Instance);
        using StylusEvmApi api = new(helper.VmHost, Address.Zero, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = TestItem.AddressA.Bytes.ToArray();
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AccountCodeHash, input);

        response.Result.Should().BeEquivalentTo(codeHash.BytesAsSpan.ToArray());
    }

    [Test]
    public void EmitLog_NoTopicsWithData_AddsLogEntry()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] data = [0xCA, 0xFE];
        byte[] input = EncodeEmitLog([], data);
        api.Handle(StylusEvmRequestType.EmitLog, input);

        LogEntry[] logs = helper.VmHost.VmState.AccessTracker.Logs.ToArray();
        logs.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new LogEntry(ActingAddress, data, []));
    }

    [Test]
    public void EmitLog_WithTopicsAndData_AddsLogEntry()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        Hash256 topic1 = Keccak.Compute("Transfer(address,address,uint256)"u8);
        Hash256 topic2 = Keccak.Compute("from"u8);
        byte[] data = [0x01, 0x02, 0x03];
        byte[] input = EncodeEmitLog([topic1, topic2], data);
        api.Handle(StylusEvmRequestType.EmitLog, input);

        LogEntry[] logs = helper.VmHost.VmState.AccessTracker.Logs.ToArray();
        logs.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new LogEntry(ActingAddress, data, [topic1, topic2]));
    }

    [Test]
    public void EmitLog_EmptyLogNoData_AddsLogEntry()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, NoOpMemoryModel, helper.GetStylusParams());

        byte[] input = EncodeEmitLog([], []);
        api.Handle(StylusEvmRequestType.EmitLog, input);

        LogEntry[] logs = helper.VmHost.VmState.AccessTracker.Logs.ToArray();
        logs.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new LogEntry(ActingAddress, [], []));
    }

    [Test]
    public void AddPages_NonZeroPages_ReturnsGasCost()
    {
        using TestStylusVm helper = new();
        ushort pageGas = 1000;
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, new StylusMemoryModel(0, pageGas), helper.GetStylusParams());

        byte[] input = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(input, 1);
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AddPages, input);

        response.GasCost.Should().BeGreaterThan(0);
    }

    [Test]
    public void AddPages_ZeroPages_ReturnsZeroCost()
    {
        using TestStylusVm helper = new();
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, new StylusMemoryModel(10, 1000), helper.GetStylusParams());

        byte[] input = new byte[2];
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AddPages, input);

        response.GasCost.Should().Be(0);
    }

    [Test]
    public void AddPages_V59ExceedsPageLimit_ReturnsMaxUint64()
    {
        using TestStylusVm helper = new(arbosVersion: ArbosVersion.FiftyNine);
        StylusParams stylusParams = helper.GetStylusParams();
        stylusParams.SetPageLimit(1);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, new StylusMemoryModel(0, 1000), stylusParams);

        byte[] input = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(input, 2); // grow by 2 → newOpen=2 > PageLimit=1
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AddPages, input);

        response.GasCost.Should().Be(ulong.MaxValue);
    }

    [Test]
    public void AddPages_V59WithinPageLimit_ReturnsNormalCost()
    {
        using TestStylusVm helper = new(arbosVersion: ArbosVersion.FiftyNine);
        StylusParams stylusParams = helper.GetStylusParams();
        stylusParams.SetPageLimit(128);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, new StylusMemoryModel(0, 1000), stylusParams);

        byte[] input = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(input, 1);
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AddPages, input);

        response.GasCost.Should().BeGreaterThan(0);
        response.GasCost.Should().BeLessThan(ulong.MaxValue);
    }

    [Test]
    public void AddPages_V51ExceedsPageLimit_ReturnsNormalCost()
    {
        using TestStylusVm helper = new(arbosVersion: ArbosVersion.FiftyOne);
        StylusParams stylusParams = helper.GetStylusParams();
        stylusParams.SetPageLimit(1);
        using StylusEvmApi api = new(helper.VmHost, ActingAddress, new StylusMemoryModel(0, 1000), stylusParams);

        byte[] input = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(input, 2);
        StylusEvmResponse response = api.Handle(StylusEvmRequestType.AddPages, input);

        response.GasCost.Should().BeGreaterThan(0);
        response.GasCost.Should().BeLessThan(ulong.MaxValue);
    }

    private static byte[] EncodeSetTrieSlots(ulong gas, params (UInt256 index, byte[] value)[] slots)
    {
        int size = 8 + slots.Length * 64;
        byte[] input = new byte[size];
        BinaryPrimitives.WriteUInt64BigEndian(input.AsSpan(0, 8), gas);
        int offset = 8;
        foreach ((UInt256 index, byte[] value) in slots)
        {
            index.ToBigEndian(input.AsSpan(offset, 32));
            value.CopyTo(input.AsSpan(offset + 32, 32));
            offset += 64;
        }
        return input;
    }

    private static byte[] EncodeTransientSet(UInt256 index, byte[] value)
    {
        byte[] input = new byte[64];
        index.ToBigEndian(input.AsSpan(0, 32));
        value.CopyTo(input.AsSpan(32, 32));
        return input;
    }

    private static byte[] EncodeAccountCode(Address address, ulong gasLeft)
    {
        byte[] input = new byte[28];
        address.Bytes.CopyTo(input.AsSpan(0, 20));
        BinaryPrimitives.WriteUInt64BigEndian(input.AsSpan(20, 8), gasLeft);
        return input;
    }

    private static byte[] EncodeEmitLog(Hash256[] topics, byte[] data)
    {
        int size = 4 + topics.Length * 32 + data.Length;
        byte[] input = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(input.AsSpan(0, 4), (uint)topics.Length);
        int offset = 4;
        foreach (Hash256 topic in topics)
        {
            topic.Bytes.CopyTo(input.AsSpan(offset, 32));
            offset += 32;
        }
        data.CopyTo(input.AsSpan(offset));
        return input;
    }
}
