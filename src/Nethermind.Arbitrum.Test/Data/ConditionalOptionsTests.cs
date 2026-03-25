// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Text.Json;
using FluentAssertions;
using Nethermind.Arbitrum.Data;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Serialization.Json;

namespace Nethermind.Arbitrum.Test.Data;

[TestFixture]
public class ConditionalOptionsTests
{
    private static readonly JsonSerializerOptions JsonOptions = EthereumJsonSerializer.JsonOptions;
    private static readonly BlockHeader TestHeader = Build.A.BlockHeader.TestObject;

    [Test]
    public void Check_BlockNumberMin_RejectsWhenTooLow()
    {
        ConditionalOptions opts = new() { BlockNumberMin = 100 };
        Result result = opts.Check(99, 0, new FakeStateReader(), TestHeader);
        result.Error.Should().Contain("BlockNumberMin condition not met");
    }

    [Test]
    public void Check_BlockNumberMax_RejectsWhenTooHigh()
    {
        ConditionalOptions opts = new() { BlockNumberMax = 100 };
        Result result = opts.Check(101, 0, new FakeStateReader(), TestHeader);
        result.Error.Should().Contain("BlockNumberMax condition not met");
    }

    [Test]
    public void Check_BlockNumberRange_AcceptsInRange()
    {
        ConditionalOptions opts = new() { BlockNumberMin = 50, BlockNumberMax = 150 };
        Result result = opts.Check(100, 0, new FakeStateReader(), TestHeader);
        ((bool)result).Should().BeTrue();
    }

    [Test]
    public void Check_TimestampMin_RejectsWhenTooEarly()
    {
        ConditionalOptions opts = new() { TimestampMin = 1000 };
        Result result = opts.Check(0, 999, new FakeStateReader(), TestHeader);
        result.Error.Should().Contain("TimestampMin condition not met");
    }

    [Test]
    public void Check_TimestampMax_RejectsWhenTooLate()
    {
        ConditionalOptions opts = new() { TimestampMax = 1000 };
        Result result = opts.Check(0, 1001, new FakeStateReader(), TestHeader);
        result.Error.Should().Contain("TimestampMax condition not met");
    }

    [Test]
    public void Check_RootHashMatch_AcceptsTransaction()
    {
        Address addr = TestItem.AddressA;
        Hash256 rootHash = TestItem.KeccakA;

        FakeStateReader stateReader = new();
        stateReader.SetAccount(addr, new AccountStruct(UInt256.Zero, UInt256.Zero, rootHash.ValueHash256, Keccak.OfAnEmptyString.ValueHash256));

        ConditionalOptions opts = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [addr] = new() { RootHash = rootHash }
            }
        };

        ((bool)opts.Check(0, 0, stateReader, TestHeader)).Should().BeTrue();
    }

    [Test]
    public void Check_RootHashMismatch_RejectsTransaction()
    {
        Address addr = TestItem.AddressA;

        FakeStateReader stateReader = new();
        stateReader.SetAccount(addr, new AccountStruct(UInt256.Zero, UInt256.Zero, TestItem.KeccakB.ValueHash256, Keccak.OfAnEmptyString.ValueHash256));

        ConditionalOptions opts = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [addr] = new() { RootHash = TestItem.KeccakA }
            }
        };

        opts.Check(0, 0, stateReader, TestHeader).Error.Should().Contain("Storage root hash condition not met");
    }

    [Test]
    public void Check_RootHashOnNonexistentAccount_MatchesZeroHash()
    {
        // Nitro returns common.Hash{} (all zeros) for nonexistent accounts.
        // A condition expecting zero hash should pass, while EmptyTreeHash should fail.
        Address addr = TestItem.AddressA;
        FakeStateReader stateReader = new(); // no account set — TryGetAccount returns false

        Hash256 zeroHash = new(new ValueHash256());
        ConditionalOptions optsZero = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [addr] = new() { RootHash = zeroHash }
            }
        };
        ((bool)optsZero.Check(0, 0, stateReader, TestHeader)).Should().BeTrue();

        ConditionalOptions optsEmptyTree = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [addr] = new() { RootHash = Keccak.EmptyTreeHash }
            }
        };
        optsEmptyTree.Check(0, 0, stateReader, TestHeader).Error.Should().Contain("Storage root hash condition not met");
    }

    [Test]
    public void Check_SlotValueMatch_AcceptsTransaction()
    {
        Address addr = TestItem.AddressA;
        UInt256 slot = new(42);
        Hash256 value = TestItem.KeccakA;

        FakeStateReader stateReader = new();
        stateReader.SetStorage(addr, slot, value.Bytes.ToArray());

        ConditionalOptions opts = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [addr] = new() { SlotValues = new Dictionary<UInt256, Hash256> { [slot] = value } }
            }
        };

        ((bool)opts.Check(0, 0, stateReader, TestHeader)).Should().BeTrue();
    }

    [Test]
    public void Check_SlotValueMismatch_RejectsTransaction()
    {
        Address addr = TestItem.AddressA;
        UInt256 slot = new(42);

        FakeStateReader stateReader = new();
        stateReader.SetStorage(addr, slot, TestItem.KeccakB.Bytes.ToArray());

        ConditionalOptions opts = new()
        {
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [addr] = new() { SlotValues = new Dictionary<UInt256, Hash256> { [slot] = TestItem.KeccakA } }
            }
        };

        opts.Check(0, 0, stateReader, TestHeader).Error.Should().Contain("Storage slot value condition not met");
    }

    [Test]
    public void Deserialize_HexString_ParsesAsRootHash()
    {
        string json = $"\"{TestItem.KeccakA}\"";
        AccountStateCondition? condition = JsonSerializer.Deserialize<AccountStateCondition>(json, JsonOptions);
        AccountStateCondition expected = new() { RootHash = TestItem.KeccakA };
        condition.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Deserialize_Object_ParsesAsSlotValues()
    {
        string json = """{"0x2a":"0x0000000000000000000000000000000000000000000000000000000000000001"}""";
        AccountStateCondition? condition = JsonSerializer.Deserialize<AccountStateCondition>(json, JsonOptions);
        AccountStateCondition expected = new()
        {
            SlotValues = new Dictionary<UInt256, Hash256>
            {
                [new UInt256(42)] = new("0x0000000000000000000000000000000000000000000000000000000000000001")
            }
        };
        condition.Should().BeEquivalentTo(expected);
    }

    [Test]
    public void Deserialize_FullConditionalOptions_RoundTrips()
    {
        string json = """
        {
            "knownAccounts": {
                "0x0000000000000000000000000000000000000001": "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421"
            },
            "blockNumberMin": "0x64",
            "blockNumberMax": "0xc8",
            "timestampMin": "0x3e8",
            "timestampMax": "0x7d0"
        }
        """;

        ConditionalOptions? opts = JsonSerializer.Deserialize<ConditionalOptions>(json, JsonOptions);
        ConditionalOptions expected = new()
        {
            BlockNumberMin = 100UL,
            BlockNumberMax = 200UL,
            TimestampMin = 1000UL,
            TimestampMax = 2000UL,
            KnownAccounts = new Dictionary<Address, AccountStateCondition>
            {
                [new Address("0x0000000000000000000000000000000000000001")] = new()
                {
                    RootHash = new Hash256("0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421")
                }
            }
        };
        opts.Should().BeEquivalentTo(expected);
    }

}
