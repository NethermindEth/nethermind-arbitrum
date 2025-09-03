using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public sealed class ArbAddressTableTests
{
    private const ulong DefaultGasSupplied = 1000; // Sufficient gas for framework costs
    private static readonly Address TestAddress = new("0x1234567890123456789012345678901234567890");

    private IWorldState _worldState = null!;
    private ArbosState _arbosState = null!;
    private PrecompileTestContextBuilder _context = null!;

    [SetUp]
    public void SetUp()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied) { ArbosState = _arbosState };
    }

    [Test]
    public void AddressExists_WithRegisteredAddress_ReturnsTrue()
    {
        _arbosState.AddressTable.Register(TestAddress);

        bool exists = ArbAddressTable.AddressExists(_context, TestAddress);

        exists.Should().BeTrue();
    }

    [Test]
    public void AddressExists_WithUnregisteredAddress_ReturnsFalse()
    {
        bool exists = ArbAddressTable.AddressExists(_context, TestAddress);

        exists.Should().BeFalse();
    }

    [Test]
    public void Compress_WithUnregisteredAddress_ReturnsFullAddress()
    {
        byte[] compressed = ArbAddressTable.Compress(_context, TestAddress);

        compressed.Should().NotBeNull();
        compressed.Length.Should().Be(21); // RLP encoding of a 20-byte address adds 1 byte prefix
    }

    [Test]
    public void Compress_WithRegisteredAddress_ReturnsCompressedIndex()
    {
        _arbosState.AddressTable.Register(TestAddress);

        byte[] compressed = ArbAddressTable.Compress(_context, TestAddress);

        compressed.Should().NotBeNull();
        compressed.Length.Should().BeLessThan(21); // Should be smaller than full address encoding
    }

    [Test]
    public void Decompress_WithValidData_ReturnsAddressAndBytesRead()
    {
        byte[] compressed = _arbosState.AddressTable.Compress(TestAddress);

        (Address address, UInt256 bytesRead) = ArbAddressTable.Decompress(_context, compressed, UInt256.Zero);

        address.Should().Be(TestAddress);
        bytesRead.Should().Be(new UInt256((ulong)compressed.Length));
    }

    [Test]
    public void Decompress_WithInvalidOffset_ThrowsArgumentException()
    {
        byte[] buffer = [1, 2, 3, 4, 5];
        UInt256 invalidOffset = new(10); // Offset beyond buffer length

        Action action = () => ArbAddressTable.Decompress(_context, buffer, invalidOffset);

        action.Should().Throw<ArgumentException>()
              .WithMessage("Offset 10 exceeds buffer length 5 in ArbAddressTable.Decompress");
    }

    [Test]
    public void Lookup_WithRegisteredAddress_ReturnsIndex()
    {
        ulong expectedIndex = _arbosState.AddressTable.Register(TestAddress);

        UInt256 index = ArbAddressTable.Lookup(_context, TestAddress);

        index.Should().Be(new UInt256(expectedIndex));
    }

    [Test]
    public void Lookup_WithUnregisteredAddress_ThrowsArgumentException()
    {
        Action action = () => ArbAddressTable.Lookup(_context, TestAddress);

        action.Should().Throw<ArgumentException>()
       .WithMessage($"Address {TestAddress} does not exist in AddressTable");
    }

    [Test]
    public void LookupIndex_WithValidIndex_ReturnsAddress()
    {
        ulong index = _arbosState.AddressTable.Register(TestAddress);

        Address address = ArbAddressTable.LookupIndex(_context, new UInt256(index));

        address.Should().Be(TestAddress);
    }

    [Test]
    public void LookupIndex_WithInvalidIndex_ThrowsArgumentException()
    {
        UInt256 invalidIndex = new(999);

        Action action = () => ArbAddressTable.LookupIndex(_context, invalidIndex);

        action.Should().Throw<ArgumentException>()
       .WithMessage("Index 999 does not exist in AddressTable (table size: 0)");
    }

    [Test]
    public void LookupIndex_WithIndexTooLarge_ThrowsArgumentException()
    {
        UInt256 tooLargeIndex = UInt256.MaxValue;

        Action action = () => ArbAddressTable.LookupIndex(_context, tooLargeIndex);

        action.Should().Throw<ArgumentException>()
       .WithMessage($"Index {tooLargeIndex} exceeds maximum allowed value {ulong.MaxValue} in ArbAddressTable.LookupIndex");
    }

    [Test]
    public void Register_WithNewAddress_ReturnsIndex()
    {
        UInt256 index = ArbAddressTable.Register(_context, TestAddress);

        index.Should().Be(UInt256.Zero); // The first registered address gets index 0
    }

    [Test]
    public void Register_WithExistingAddress_ReturnsSameIndex()
    {
        ulong expectedIndex = _arbosState.AddressTable.Register(TestAddress);

        UInt256 index = ArbAddressTable.Register(_context, TestAddress);

        index.Should().Be(new UInt256(expectedIndex));
    }

    [Test]
    public void Size_OnEmptyTable_ReturnsZero()
    {
        UInt256 size = ArbAddressTable.Size(_context);

        size.Should().Be(UInt256.Zero);
    }

    [Test]
    public void Size_WithRegisteredAddresses_ReturnsCorrectCount()
    {
        Address address1 = new("0x1111111111111111111111111111111111111111");
        Address address2 = new("0x2222222222222222222222222222222222222222");
        Address address3 = new("0x3333333333333333333333333333333333333333");

        _arbosState.AddressTable.Register(address1);
        _arbosState.AddressTable.Register(address2);
        _arbosState.AddressTable.Register(address3);

        UInt256 size = ArbAddressTable.Size(_context);

        size.Should().Be(new UInt256(3));
    }

    [Test]
    public void CompressAndDecompress_RoundTrip_PreservesAddress()
    {
        Address unregisteredAddress = new("0x9876543210987654321098765432109876543210");
        Address[] testAddresses = [TestAddress, unregisteredAddress];

        // Register only the first address
        _arbosState.AddressTable.Register(testAddresses[0]);

        foreach (Address address in testAddresses)
        {
            const ulong gasSupplied = (GasCostOf.ColdSLoad + GasCostOf.DataCopy) * 2 + 1;
            PrecompileTestContextBuilder context = new(_worldState, gasSupplied) { ArbosState = _arbosState };

            byte[] compressed = ArbAddressTable.Compress(context, address);
            (Address decompressed, _) = ArbAddressTable.Decompress(context, compressed, UInt256.Zero);

            decompressed.Should().Be(address, $"Round trip failed for address {address}");
        }
    }

    [Test]
    public void Decompress_WithOffsetLargerThanIntMax_ThrowsArgumentException()
    {
        // Arrange
        PrecompileTestContextBuilder context = new(_worldState, DefaultGasSupplied) { ArbosState = _arbosState };
        byte[] buffer = [0x01, 0x02, 0x03, 0x04];

        // Test offset larger than int.MaxValue (equivalent to Go's !IsInt64() check)
        UInt256 invalidOffset = new UInt256((ulong)int.MaxValue) + 1;

        // Act & Assert
        Action act = () => ArbAddressTable.Decompress(context, buffer, invalidOffset);
        act.Should().Throw<ArgumentException>()
           .WithMessage($"Offset {invalidOffset} exceeds maximum allowed value {int.MaxValue} in ArbAddressTable.Decompress");
    }

    [Test]
    public void Decompress_WithOffsetBeyondBufferLength_ThrowsArgumentException()
    {
        // Arrange
        PrecompileTestContextBuilder context = new(_worldState, DefaultGasSupplied) { ArbosState = _arbosState };
        byte[] buffer = [0x01, 0x02, 0x03, 0x04];
        UInt256 offsetBeyondBuffer = new UInt256((ulong)buffer.Length + 1);

        // Act & Assert
        Action act = () => ArbAddressTable.Decompress(context, buffer, offsetBeyondBuffer);
        act.Should().Throw<ArgumentException>()
           .WithMessage($"Offset {(int)offsetBeyondBuffer} exceeds buffer length {buffer.Length} in ArbAddressTable.Decompress");
    }
}
