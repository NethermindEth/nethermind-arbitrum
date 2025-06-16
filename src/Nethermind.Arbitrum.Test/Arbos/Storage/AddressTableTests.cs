// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Int256;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public sealed class AddressTableTests
{
    private static readonly Address TestAccount = new("0xA4B05FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

    [Test]
    public void Initialize_Always_CreatesEmptyTable()
    {
        var storage = CreateStorage();

        AddressTable.Initialize(storage);
        var addressTable = new AddressTable(storage);

        addressTable.Size().Should().Be(0);

        var (_, found) = addressTable.Lookup(Address.Zero);
        found.Should().BeFalse();

        var (_, indexFound) = addressTable.LookupIndex(0);
        indexFound.Should().BeFalse();
    }

    [Test]
    public void Register_NewAddress_AddsToTableAndReturnsZeroIndex()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var index = addressTable.Register(testAddress);

        index.Should().Be(0);
        addressTable.Size().Should().Be(1);
    }

    [Test]
    public void Register_ExistingAddress_ReturnsSameIndex()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var firstIndex = addressTable.Register(testAddress);
        var secondIndex = addressTable.Register(testAddress);

        firstIndex.Should().Be(secondIndex);
        addressTable.Size().Should().Be(1);
    }

    [Test]
    public void Register_MultipleAddresses_AssignsSequentialIndices()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var address1 = new Address("0x1111111111111111111111111111111111111111");
        var address2 = new Address("0x2222222222222222222222222222222222222222");
        var address3 = new Address("0x3333333333333333333333333333333333333333");

        var index1 = addressTable.Register(address1);
        var index2 = addressTable.Register(address2);
        var index3 = addressTable.Register(address3);

        index1.Should().Be(0);
        index2.Should().Be(1);
        index3.Should().Be(2);
        addressTable.Size().Should().Be(3);
    }

    [Test]
    public void Lookup_ExistingAddress_ReturnsCorrectIndex()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        var expectedIndex = addressTable.Register(testAddress);

        var (actualIndex, found) = addressTable.Lookup(testAddress);

        found.Should().BeTrue();
        actualIndex.Should().Be(expectedIndex);
    }

    [Test]
    public void Lookup_NonExistentAddress_ReturnsNotFound()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var (_, found) = addressTable.Lookup(testAddress);

        found.Should().BeFalse();
    }

    [Test]
    public void AddressExists_ExistingAddress_ReturnsTrue()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        var exists = addressTable.AddressExists(testAddress);

        exists.Should().BeTrue();
    }

    [Test]
    public void AddressExists_NonExistentAddress_ReturnsFalse()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var exists = addressTable.AddressExists(testAddress);

        exists.Should().BeFalse();
    }

    [Test]
    public void LookupIndex_ValidIndex_ReturnsCorrectAddress()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        var index = addressTable.Register(testAddress);

        var (actualAddress, found) = addressTable.LookupIndex(index);

        found.Should().BeTrue();
        actualAddress.Should().Be(testAddress);
    }

    [Test]
    public void LookupIndex_InvalidIndex_ReturnsNotFound()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);

        var (_, found) = addressTable.LookupIndex(0);

        found.Should().BeFalse();
    }

    [Test]
    public void LookupIndex_IndexOutOfRange_ReturnsNotFound()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        var (_, found) = addressTable.LookupIndex(1); // Only index 0 should exist

        found.Should().BeFalse();
    }

    [Test]
    public void Compress_AddressNotInTable_ReturnsFullAddress()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var compressed = addressTable.Compress(testAddress);

        compressed.Should().NotBeNull();
        compressed.Length.Should().Be(21); // RLP encoding of 20-byte address adds 1 byte prefix

        // The compressed data should contain the full address
        var decodedBytes = compressed.AsSpan(1); // Skip RLP prefix
        decodedBytes.SequenceEqual(testAddress.Bytes).Should().BeTrue();
    }

    [Test]
    public void Compress_AddressInTable_ReturnsCompressedIndex()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        var compressed = addressTable.Compress(testAddress);

        compressed.Should().NotBeNull();
        compressed.Length.Should().BeLessThan(21); // Should be smaller than full address encoding
    }

    [Test]
    public void Decompress_FullAddress_ReturnsOriginalAddress()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        var compressed = addressTable.Compress(testAddress);

        var (decompressedAddress, bytesRead) = addressTable.Decompress(compressed);

        decompressedAddress.Should().Be(testAddress);
        bytesRead.Should().Be((ulong)compressed.Length);
    }

    [Test]
    public void Decompress_CompressedIndex_ReturnsOriginalAddress()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);
        var compressed = addressTable.Compress(testAddress);

        var (decompressedAddress, bytesRead) = addressTable.Decompress(compressed);

        decompressedAddress.Should().Be(testAddress);
        bytesRead.Should().Be((ulong)compressed.Length);
    }

    [Test]
    public void Decompress_InvalidIndex_ThrowsException()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);

        // Create compressed data with invalid index (999)
        var invalidCompressed = Rlp.Encode(999ul).Bytes;

        var action = () => addressTable.Decompress(invalidCompressed);
        action.Should().Throw<InvalidOperationException>()
              .WithMessage("Invalid index in compressed address");
    }

    [Test]
    public void RoundTrip_CompressDecompress_PreservesAddress()
    {
        var storage = CreateStorage();
        var addressTable = new AddressTable(storage);
        ReadOnlySpan<Address> testAddresses =
        [
            new Address("0x1111111111111111111111111111111111111111"),
            new Address("0x2222222222222222222222222222222222222222"),
            new Address("0x3333333333333333333333333333333333333333")
        ];

        // Register some addresses
        addressTable.Register(testAddresses[0]);
        addressTable.Register(testAddresses[2]); // Skip middle one intentionally

        foreach (var address in testAddresses)
        {
            var compressed = addressTable.Compress(address);
            var (decompressed, _) = addressTable.Decompress(compressed);

            decompressed.Should().Be(address, $"Round trip failed for address {address}");
        }
    }

    [Test]
    public void Persistence_TableStatePreservedAcrossInstances()
    {
        var storage = CreateStorage();
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        // First instance - register address
        var addressTable1 = new AddressTable(storage);
        var originalIndex = addressTable1.Register(testAddress);

        // Second instance - should see the same data
        var addressTable2 = new AddressTable(storage);

        addressTable2.Size().Should().Be(1);

        var (lookupIndex, found) = addressTable2.Lookup(testAddress);
        found.Should().BeTrue();
        lookupIndex.Should().Be(originalIndex);

        var (lookupAddress, indexFound) = addressTable2.LookupIndex(originalIndex);
        indexFound.Should().BeTrue();
        lookupAddress.Should().Be(testAddress);
    }

    private static ArbosStorage CreateStorage()
    {
        var worldState = TrackingWorldState.CreateNewInMemory();
        worldState.CreateAccountIfNotExists(TestAccount, UInt256.Zero, UInt256.One);
        var burner = new SystemBurner();
        return new ArbosStorage(worldState, burner, TestAccount);
    }
}
