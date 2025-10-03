using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public sealed class ArbAddressTableParserTests
{
    private const ulong DefaultGasSupplied = 100000;
    private static readonly Address TestAddress = new("0x1234567890123456789012345678901234567890");

    private static readonly uint _addressExistsId = PrecompileHelper.GetMethodId("addressExists(address)");
    private static readonly uint _compressId = PrecompileHelper.GetMethodId("compress(address)");
    private static readonly uint _lookupId = PrecompileHelper.GetMethodId("lookup(address)");
    private static readonly uint _lookupIndexId = PrecompileHelper.GetMethodId("lookupIndex(uint256)");
    private static readonly uint _registerId = PrecompileHelper.GetMethodId("register(address)");
    private static readonly uint _sizeId = PrecompileHelper.GetMethodId("size()");

    // ABI signatures for ArbAddressTable methods
    private static readonly AbiSignature AddressExistsSignature = new("addressExists", AbiType.Address);
    private static readonly AbiSignature CompressSignature = new("compress", AbiType.Address);
    private static readonly AbiSignature LookupSignature = new("lookup", AbiType.Address);
    private static readonly AbiSignature LookupIndexSignature = new("lookupIndex", AbiType.UInt256);
    private static readonly AbiSignature RegisterSignature = new("register", AbiType.Address);
    private static readonly AbiSignature SizeSignature = new("size");

    private ArbosState _arbosState = null!;
    private PrecompileTestContextBuilder _context = null!;
    private IWorldState _worldState = null!;
    private BlockHeader _genesisBlockHeader = null!;

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = _worldState.BeginScope(IWorldState.PreGenesis);
        Block b = ArbOSInitialization.Create(_worldState);
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosState();
        _genesisBlockHeader = b.Header;
    }

    [Test]
    public void ParsesAddressExists_ValidInputData_ReturnsTrue()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        _arbosState.AddressTable.Register(TestAddress);

        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, AddressExistsSignature, TestAddress);

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_addressExistsId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        result[31].Should().Be(1); // ABI-encoded boolean true
    }

    [Test]
    public void ParsesAddressExists_AddressNotRegistered_ReturnsFalse()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        // Don't register the address

        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, AddressExistsSignature, TestAddress);

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_addressExistsId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        result[31].Should().Be(0); // ABI-encoded boolean false
    }

    [Test]
    public void ParsesCompress_ValidInputData_ReturnsCompressedBytes()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, CompressSignature, TestAddress);

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_compressId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(32); // ABI-encoded bytes include offset and length
    }

    [Test]
    public void ParsesLookup_ValidInputData_ReturnsIndex()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        ulong expectedIndex = _arbosState.AddressTable.Register(TestAddress);

        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, LookupSignature, TestAddress);

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_lookupId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 resultIndex = new(result, isBigEndian: true);
        resultIndex.Should().Be(new UInt256(expectedIndex));
    }

    [Test]
    public void ParsesLookup_WithUnregisteredAddress_Throws()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, LookupSignature, TestAddress);

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_lookupId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, inputData);

        action.Should().Throw<ArgumentException>()
              .WithMessage($"Address {TestAddress} does not exist in AddressTable");
    }

    [Test]
    public void ParsesLookupIndex_ValidInputData_ReturnsAddress()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        ulong index = _arbosState.AddressTable.Register(TestAddress);

        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, LookupIndexSignature, new UInt256(index));

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_lookupIndexId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);

        Address resultAddress = new(result[12..]); // Extract address from last 20 bytes
        resultAddress.Should().Be(TestAddress);
    }

    [Test]
    public void ParsesRegister_ValidInputData_ReturnsIndex()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, RegisterSignature, TestAddress);

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_registerId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 resultIndex = new(result, isBigEndian: true);
        resultIndex.Should().Be(UInt256.Zero); // The first registered address gets index 0
    }

    [Test]
    public void ParsesSize_ValidInputData_ReturnsSize()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        // Register some addresses
        _arbosState.AddressTable.Register(new Address("0x1111111111111111111111111111111111111111"));
        _arbosState.AddressTable.Register(new Address("0x2222222222222222222222222222222222222222"));

        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, SizeSignature);

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_sizeId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 resultSize = new(result, isBigEndian: true);
        resultSize.Should().Be(new UInt256(2));
    }

    [Test]
    public void ParsesSomeMethodId_InvalidMethodId_ImplementationNotRegistered()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };
        byte[] data = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data, 0x12345678);
        byte[] inputData = data;

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_sizeId, out PrecompileHandler? handler);
        exists.Should().BeFalse();
    }

    [Test]
    public void ParsesWithInvalidInputData_ThrowsRevertException()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };

        AbiSignature abiSignature = new("addressExists", AbiType.Address);
        byte[] inputData = abiSignature.Address;

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_sizeId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, inputData);

        action.Should().Throw<RevertException>();
    }
}
