// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Serialization.Rlp;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public sealed class AddressTableTests
{
    private static readonly Address TestAccount = new("0xA4B05FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

    [Test]
    public void Initialize_OnNewStorage_CreatesEmptyTable()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);

        AddressTable.Initialize(storage);
        var addressTable = new AddressTable(storage);

        addressTable.Size().Should().Be(0);

        var (_, found) = addressTable.Lookup(Address.Zero);
        found.Should().BeFalse();

        var (_, indexFound) = addressTable.LookupIndex(0);
        indexFound.Should().BeFalse();
    }

    [Test]
    public void Register_WithNewAddress_ReturnsZeroIndex()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var index = addressTable.Register(testAddress);

        index.Should().Be(0);
        addressTable.Size().Should().Be(1);
    }

    [Test]
    public void Register_WithExistingAddress_ReturnsSameIndex()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var firstIndex = addressTable.Register(testAddress);
        var secondIndex = addressTable.Register(testAddress);

        firstIndex.Should().Be(secondIndex);
        addressTable.Size().Should().Be(1);
    }

    [Test]
    public void Register_WithMultipleAddresses_AssignsSequentialIndices()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
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
    public void Lookup_WithExistingAddress_ReturnsCorrectIndex()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        var expectedIndex = addressTable.Register(testAddress);

        var (actualIndex, found) = addressTable.Lookup(testAddress);

        found.Should().BeTrue();
        actualIndex.Should().Be(expectedIndex);
    }

    [Test]
    public void Lookup_WithNonExistentAddress_ReturnsNotFound()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var (_, found) = addressTable.Lookup(testAddress);

        found.Should().BeFalse();
    }

    [Test]
    public void AddressExists_WithRegisteredAddress_ReturnsTrue()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        var exists = addressTable.AddressExists(testAddress);

        exists.Should().BeTrue();
    }

    [Test]
    public void AddressExists_WithUnregisteredAddress_ReturnsFalse()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        var exists = addressTable.AddressExists(testAddress);

        exists.Should().BeFalse();
    }

    [Test]
    public void LookupIndex_WithValidIndex_ReturnsCorrectAddress()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        var index = addressTable.Register(testAddress);

        var (actualAddress, found) = addressTable.LookupIndex(index);

        found.Should().BeTrue();
        actualAddress.Should().Be(testAddress);
    }

    [Test]
    public void LookupIndex_OnEmptyTable_ReturnsNotFound()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);

        var (_, found) = addressTable.LookupIndex(0);

        found.Should().BeFalse();
    }

    [Test]
    public void LookupIndex_WithOutOfRangeIndex_ReturnsNotFound()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        var (_, found) = addressTable.LookupIndex(1); // Only index 0 should exist

        found.Should().BeFalse();
    }

    [Test]
    public void Compress_WithUnregisteredAddress_ReturnsFullAddress()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
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
    public void Compress_WithRegisteredAddress_ReturnsCompressedIndex()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        var compressed = addressTable.Compress(testAddress);

        compressed.Should().NotBeNull();
        compressed.Length.Should().BeLessThan(21); // Should be smaller than full address encoding
    }

    [TestCase("0x1234567890123456789012345678901234567890")]
    [TestCase("0x00c86e5946b60764dc788d1bfe778b9ddeb4f823")] //this case breaks if index is encoded as UInt256
    public void Decompress_WithFullAddressData_ReturnsOriginalAddress(string addressBytes)
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address(addressBytes);
        var compressed = addressTable.Compress(testAddress);

        var (decompressedAddress, bytesRead) = addressTable.Decompress(compressed);

        decompressedAddress.Should().Be(testAddress);
        bytesRead.Should().Be((ulong)compressed.Length);
    }

    [Test]
    public void Decompress_WithCompressedIndexData_ReturnsOriginalAddress()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);
        var compressed = addressTable.Compress(testAddress);

        var (decompressedAddress, bytesRead) = addressTable.Decompress(compressed);

        decompressedAddress.Should().Be(testAddress);
        bytesRead.Should().Be((ulong)compressed.Length);
    }

    [Test]
    public void Decompress_WithInvalidIndex_ThrowsInvalidOperationException()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
        var addressTable = new AddressTable(storage);

        // Create compressed data with invalid index (999)
        var invalidCompressed = Rlp.Encode(999ul).Bytes;

        var action = () => addressTable.Decompress(invalidCompressed);
        action.Should().Throw<InvalidOperationException>()
              .WithMessage("Invalid index in compressed address");
    }

    [Test]
    public void CompressAndDecompress_InSequence_PreservesOriginalAddress()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
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
    public void AddressTable_WithSameStorage_PreservesStateAcrossInstances()
    {
        TestArbosStorage.Create(out _, out ArbosStorage storage);
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
}
