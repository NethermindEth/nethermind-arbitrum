// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Arbos.Storage;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Logging;
using NUnit.Framework;

namespace Nethermind.Arbitrum.Test.Arbos.Storage;

public class AddressTableTests
{
    private static readonly ILogManager Logger = LimboLogs.Instance;
    private static readonly Address TestAccount = new("0xA4B05FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF");

    [Test]
    public void Initialize_Always_CreatesEmptyTable()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();

        // Act
        AddressTable.Initialize(storage);
        var addressTable = new AddressTable(storage);

        // Assert
        addressTable.Size().Should().Be(0);

        var (_, found) = addressTable.Lookup(Address.Zero);
        found.Should().BeFalse();

        var (_, indexFound) = addressTable.LookupIndex(0);
        indexFound.Should().BeFalse();
    }

    [Test]
    public void Register_NewAddress_AddsToTableAndReturnsZeroIndex()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        // Act
        ulong index = addressTable.Register(testAddress);

        // Assert
        index.Should().Be(0);
        addressTable.Size().Should().Be(1);
    }

    [Test]
    public void Register_ExistingAddress_ReturnsSameIndex()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        // Act
        ulong firstIndex = addressTable.Register(testAddress);
        ulong secondIndex = addressTable.Register(testAddress);

        // Assert
        firstIndex.Should().Be(secondIndex);
        addressTable.Size().Should().Be(1);
    }

    [Test]
    public void Register_MultipleAddresses_AssignsSequentialIndices()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var address1 = new Address("0x1111111111111111111111111111111111111111");
        var address2 = new Address("0x2222222222222222222222222222222222222222");
        var address3 = new Address("0x3333333333333333333333333333333333333333");

        // Act
        ulong index1 = addressTable.Register(address1);
        ulong index2 = addressTable.Register(address2);
        ulong index3 = addressTable.Register(address3);

        // Assert
        index1.Should().Be(0);
        index2.Should().Be(1);
        index3.Should().Be(2);
        addressTable.Size().Should().Be(3);
    }

    [Test]
    public void Lookup_ExistingAddress_ReturnsCorrectIndex()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        ulong expectedIndex = addressTable.Register(testAddress);

        // Act
        var (actualIndex, found) = addressTable.Lookup(testAddress);

        // Assert
        found.Should().BeTrue();
        actualIndex.Should().Be(expectedIndex);
    }

    [Test]
    public void Lookup_NonExistentAddress_ReturnsNotFound()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        // Act
        var (_, found) = addressTable.Lookup(testAddress);

        // Assert
        found.Should().BeFalse();
    }

    [Test]
    public void AddressExists_ExistingAddress_ReturnsTrue()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        // Act
        bool exists = addressTable.AddressExists(testAddress);

        // Assert
        exists.Should().BeTrue();
    }

    [Test]
    public void AddressExists_NonExistentAddress_ReturnsFalse()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        // Act
        bool exists = addressTable.AddressExists(testAddress);

        // Assert
        exists.Should().BeFalse();
    }

    [Test]
    public void LookupIndex_ValidIndex_ReturnsCorrectAddress()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        ulong index = addressTable.Register(testAddress);

        // Act
        var (actualAddress, found) = addressTable.LookupIndex(index);

        // Assert
        found.Should().BeTrue();
        actualAddress.Should().Be(testAddress);
    }

    [Test]
    public void LookupIndex_InvalidIndex_ReturnsNotFound()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);

        // Act
        var (_, found) = addressTable.LookupIndex(0);

        // Assert
        found.Should().BeFalse();
    }

    [Test]
    public void LookupIndex_IndexOutOfRange_ReturnsNotFound()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        // Act
        var (_, found) = addressTable.LookupIndex(1); // Only index 0 should exist

        // Assert
        found.Should().BeFalse();
    }

    [Test]
    public void Compress_AddressNotInTable_ReturnsFullAddress()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        // Act
        byte[] compressed = addressTable.Compress(testAddress);

        // Assert
        compressed.Should().NotBeNull();
        compressed.Length.Should().Be(21); // RLP encoding of 20-byte address adds 1 byte prefix

        // The compressed data should contain the full address
        var decodedBytes = compressed.Skip(1).ToArray(); // Skip RLP prefix
        decodedBytes.Should().BeEquivalentTo(testAddress.Bytes);
    }

    [Test]
    public void Compress_AddressInTable_ReturnsCompressedIndex()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);

        // Act
        byte[] compressed = addressTable.Compress(testAddress);

        // Assert
        compressed.Should().NotBeNull();
        compressed.Length.Should().BeLessThan(21); // Should be smaller than full address encoding
    }

    [Test]
    public void Decompress_FullAddress_ReturnsOriginalAddress()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        byte[] compressed = addressTable.Compress(testAddress);

        // Act
        var (decompressedAddress, bytesRead) = addressTable.Decompress(compressed);

        // Assert
        decompressedAddress.Should().Be(testAddress);
        bytesRead.Should().Be((ulong)compressed.Length);
    }

    [Test]
    public void Decompress_CompressedIndex_ReturnsOriginalAddress()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddress = new Address("0x1234567890123456789012345678901234567890");
        addressTable.Register(testAddress);
        byte[] compressed = addressTable.Compress(testAddress);

        // Act
        var (decompressedAddress, bytesRead) = addressTable.Decompress(compressed);

        // Assert
        decompressedAddress.Should().Be(testAddress);
        bytesRead.Should().Be((ulong)compressed.Length);
    }

    [Test]
    public void Decompress_InvalidIndex_ThrowsException()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);

        // Create compressed data with invalid index (999)
        byte[] invalidCompressed = Nethermind.Serialization.Rlp.Rlp.Encode(999ul).Bytes;

        // Act & Assert
        var action = () => addressTable.Decompress(invalidCompressed);
        action.Should().Throw<InvalidOperationException>()
              .WithMessage("Invalid index in compressed address");
    }

    [Test]
    public void RoundTrip_CompressDecompress_PreservesAddress()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var addressTable = new AddressTable(storage);
        var testAddresses = new[]
        {
            new Address("0x1111111111111111111111111111111111111111"),
            new Address("0x2222222222222222222222222222222222222222"),
            new Address("0x3333333333333333333333333333333333333333")
        };

        // Register some addresses
        addressTable.Register(testAddresses[0]);
        addressTable.Register(testAddresses[2]); // Skip middle one intentionally

        // Act & Assert for each address
        foreach (var address in testAddresses)
        {
            byte[] compressed = addressTable.Compress(address);
            var (decompressed, _) = addressTable.Decompress(compressed);

            decompressed.Should().Be(address, $"Round trip failed for address {address}");
        }
    }

    [Test]
    public void Persistence_TableStatePreservedAcrossInstances()
    {
        // Arrange
        var (worldState, storage) = CreateStorage();
        var testAddress = new Address("0x1234567890123456789012345678901234567890");

        // First instance - register address
        var addressTable1 = new AddressTable(storage);
        ulong originalIndex = addressTable1.Register(testAddress);

        // Second instance - should see the same data
        var addressTable2 = new AddressTable(storage);

        // Act & Assert
        addressTable2.Size().Should().Be(1);

        var (lookupIndex, found) = addressTable2.Lookup(testAddress);
        found.Should().BeTrue();
        lookupIndex.Should().Be(originalIndex);

        var (lookupAddress, indexFound) = addressTable2.LookupIndex(originalIndex);
        indexFound.Should().BeTrue();
        lookupAddress.Should().Be(testAddress);
    }

    private static (TrackingWorldState worldState, ArbosStorage storage) CreateStorage()
    {
        var worldState = TrackingWorldState.CreateNewInMemory();
        worldState.CreateAccountIfNotExists(TestAccount, Nethermind.Int256.UInt256.Zero, Nethermind.Int256.UInt256.One);
        var burner = new SystemBurner();
        var storage = new ArbosStorage(worldState, burner, TestAccount);

        return (worldState, storage);
    }
}
