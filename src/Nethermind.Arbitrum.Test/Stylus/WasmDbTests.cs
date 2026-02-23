// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using System.Security.Cryptography;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Stylus;
using Nethermind.Arbitrum.Stylus;
using Nethermind.Core.Crypto;
using Nethermind.Db;

namespace Nethermind.Arbitrum.Test.Stylus;

public class WasmDbTests
{
    [Test]
    public void IsEmpty_NewDb_ReturnsTrue()
    {
        IWasmDb wasmDb = CreateDb();

        wasmDb.IsEmpty().Should().BeTrue();
    }

    [Test]
    public void IsEmpty_HasValues_ReturnsFalse()
    {
        IWasmDb wasmDb = CreateDb();

        wasmDb.SetWasmSchemaVersion(1);

        wasmDb.IsEmpty().Should().BeFalse();
    }

    [TestCase(0)]
    [TestCase(10)]
    [TestCase(255)]
    public void SetGetWasmSchemaVersion_Always_GetsWhatIsSet(byte version)
    {
        IWasmDb wasmDb = CreateDb();

        wasmDb.SetWasmSchemaVersion(version);

        wasmDb.GetWasmSchemaVersion().Should().Be(version);
    }

    [TestCase(0u)]
    [TestCase(10u)]
    [TestCase(uint.MaxValue)]
    public void SetGetWasmerSerializeVersion_Always_GetsWhatIsSet(uint version)
    {
        IWasmDb wasmDb = CreateDb();

        wasmDb.SetWasmerSerializeVersion(version);

        wasmDb.GetWasmerSerializeVersion().Should().Be(version);
    }

    [Test]
    public void TryGetActivatedAsm_EmptyDb_ReturnsFalseAndEmptyBytes()
    {
        IWasmDb wasmDb = CreateDb();
        ValueHash256 moduleHash = new(RandomNumberGenerator.GetBytes(32));

        bool result = wasmDb.TryGetActivatedAsm(StylusTargets.Arm64TargetName, in moduleHash, out byte[] bytes);

        result.Should().BeFalse();
        bytes.Should().BeEmpty();
    }

    [Test]
    public void TryGetActivatedAsm_ExistingEntry_ReturnsTrueAndBytes()
    {
        IWasmDb wasmDb = CreateDb();
        ValueHash256 moduleHash = new(RandomNumberGenerator.GetBytes(32));
        byte[] wasm = RandomNumberGenerator.GetBytes(64);

        wasmDb.WriteActivation(in moduleHash, new Dictionary<string, byte[]> { { StylusTargets.Arm64TargetName, wasm } });
        bool result = wasmDb.TryGetActivatedAsm(StylusTargets.Arm64TargetName, in moduleHash, out byte[] bytes);

        result.Should().BeTrue();
        bytes.Should().BeEquivalentTo(wasm);
    }

    [Test]
    public void WriteAllActivations_Always_WritesAllWasmsToDb()
    {
        IWasmDb wasmDb = CreateDb();
        Hash256 moduleHash1 = new(RandomNumberGenerator.GetBytes(32));
        byte[] wasm1 = RandomNumberGenerator.GetBytes(64);
        Hash256 moduleHash2 = new(RandomNumberGenerator.GetBytes(32));
        byte[] wasm2 = RandomNumberGenerator.GetBytes(64);

        wasmDb.WriteAllActivations(new Dictionary<Hash256AsKey, IReadOnlyDictionary<string, byte[]>>()
        {
            { new Hash256AsKey(moduleHash1), new Dictionary<string, byte[]> { { StylusTargets.Arm64TargetName, wasm1 } } },
            { new Hash256AsKey(moduleHash2), new Dictionary<string, byte[]> { { StylusTargets.Amd64TargetName, wasm2 } } }
        });

        bool result1 = wasmDb.TryGetActivatedAsm(StylusTargets.Arm64TargetName, in moduleHash1.ValueHash256, out byte[] bytes1);
        bool result2 = wasmDb.TryGetActivatedAsm(StylusTargets.Amd64TargetName, in moduleHash2.ValueHash256, out byte[] bytes2);

        result1.Should().BeTrue();
        bytes1.Should().BeEquivalentTo(wasm1);
        result2.Should().BeTrue();
        bytes2.Should().BeEquivalentTo(wasm2);
    }

    [Test]
    public void DeleteWasmEntries_EmptyDb_NoEntriesDeleted()
    {
        IWasmDb wasmDb = CreateDb();

        DeleteWasmResult result = wasmDb.DeleteWasmEntries([WasmStoreSchema.ActivatedAsmWavmPrefix]);

        result.Should().BeEquivalentTo(new DeleteWasmResult(0, 0));
    }

    [Test]
    public void DeleteWasmEntries_ExistingEntries_DeletesCorrectly()
    {
        IWasmDb wasmDb = CreateDb();
        ValueHash256 moduleHash = new(RandomNumberGenerator.GetBytes(32));

        wasmDb.WriteActivation(in moduleHash, new Dictionary<string, byte[]>
        {
            [StylusTargets.Arm64TargetName] = RandomNumberGenerator.GetBytes(64),
            [StylusTargets.Amd64TargetName] = RandomNumberGenerator.GetBytes(64)
        });

        DeleteWasmResult result = wasmDb.DeleteWasmEntries([WasmStoreSchema.ActivatedAsmArmPrefix]);
        bool amdIsLeft = wasmDb.TryGetActivatedAsm(StylusTargets.Amd64TargetName, in moduleHash, out _);

        // Verify that only the ARM64 entry was deleted and AMD64 entry remains
        result.Should().BeEquivalentTo(new DeleteWasmResult(1, 0));
        amdIsLeft.Should().BeTrue();
    }

    [Test]
    public void DeleteWasmEntries_ExistingEntriesWithKeyLengthMismatch_Skips()
    {
        IWasmDb wasmDb = CreateDb();
        ValueHash256 moduleHash = new(RandomNumberGenerator.GetBytes(32));

        wasmDb.WriteActivation(in moduleHash, new Dictionary<string, byte[]>
        {
            [StylusTargets.Arm64TargetName] = RandomNumberGenerator.GetBytes(64)
        });

        // Delete with the key of wrong length
        DeleteWasmResult result = wasmDb.DeleteWasmEntries([WasmStoreSchema.ActivatedAsmArmPrefix], expectedKeyLength: 3);

        result.Should().BeEquivalentTo(new DeleteWasmResult(0, 1));
    }

    private static WasmDb CreateDb()
    {
        return new WasmDb(new MemDb());
    }
}
