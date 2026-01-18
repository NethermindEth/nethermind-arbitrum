using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using System.Buffers.Binary;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public sealed class ArbAddressTableParserTests
{
    private const ulong DefaultGasSupplied = 100000;

    private static readonly uint _addressExistsId = PrecompileHelper.GetMethodId("addressExists(address)");
    private static readonly uint _compressId = PrecompileHelper.GetMethodId("compress(address)");
    private static readonly uint _decompressId = PrecompileHelper.GetMethodId("decompress(bytes,uint256)");
    private static readonly uint _lookupId = PrecompileHelper.GetMethodId("lookup(address)");
    private static readonly uint _lookupIndexId = PrecompileHelper.GetMethodId("lookupIndex(uint256)");
    private static readonly uint _registerId = PrecompileHelper.GetMethodId("register(address)");
    private static readonly uint _sizeId = PrecompileHelper.GetMethodId("size()");
    private static readonly Address TestAddress = new("0x1234567890123456789012345678901234567890");

    private ArbosState _arbosState = null!;
    private PrecompileTestContextBuilder _context = null!;
    private BlockHeader _genesisBlockHeader = null!;
    private IWorldState _worldState = null!;

    [Test]
    public void ParsesAddressExists_AddressNotRegistered_ReturnsFalse()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        // Don't register the address

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAddressTableParser.PrecompileFunctionDescription[_addressExistsId].AbiFunctionDescription.GetCallInfo().Signature,
            TestAddress
        );

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_addressExistsId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        result[31].Should().Be(0); // ABI-encoded boolean false
    }

    [Test]
    public void ParsesAddressExists_ValidInputData_ReturnsTrue()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        _arbosState.AddressTable.Register(TestAddress);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAddressTableParser.PrecompileFunctionDescription[_addressExistsId].AbiFunctionDescription.GetCallInfo().Signature,
            TestAddress
        );

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_addressExistsId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        result[31].Should().Be(1); // ABI-encoded boolean true
    }

    [Test]
    public void ParsesCompress_ValidInputData_ReturnsCompressedBytes()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAddressTableParser.PrecompileFunctionDescription[_compressId].AbiFunctionDescription.GetCallInfo().Signature,
            TestAddress
        );

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_compressId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(32); // ABI-encoded bytes include offset and length
    }

    [Test]
    public void ParsesDecompress_ValidInputData_ReturnsAddressAndConsumedBytes()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        const ulong gasSupplied = (GasCostOf.ColdSLoad + GasCostOf.DataCopy) * 2 + 1;
        PrecompileTestContextBuilder context = new(_worldState, gasSupplied) { ArbosState = _arbosState };

        byte[] compressed = ArbAddressTable.Compress(context, TestAddress);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAddressTableParser.PrecompileFunctionDescription[_decompressId].AbiFunctionDescription.GetCallInfo().Signature,
            compressed,
            UInt256.Zero
        );

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_decompressId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().NotBeNull();
        result.Length.Should().Be(64);
    }

    [Test]
    public void ParsesLookup_ValidInputData_ReturnsIndex()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        ulong expectedIndex = _arbosState.AddressTable.Register(TestAddress);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAddressTableParser.PrecompileFunctionDescription[_lookupId].AbiFunctionDescription.GetCallInfo().Signature,
            TestAddress
        );

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_lookupId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 resultIndex = new(result, isBigEndian: true);
        resultIndex.Should().Be(new UInt256(expectedIndex));
    }

    [Test]
    public void ParsesLookup_WithUnregisteredAddress_Throws()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAddressTableParser.PrecompileFunctionDescription[_lookupId].AbiFunctionDescription.GetCallInfo().Signature,
            TestAddress
        );

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_lookupId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException($"Address {TestAddress} does not exist in AddressTable");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesLookupIndex_ValidInputData_ReturnsAddress()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        ulong index = _arbosState.AddressTable.Register(TestAddress);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAddressTableParser.PrecompileFunctionDescription[_lookupIndexId].AbiFunctionDescription.GetCallInfo().Signature,
            new UInt256(index)
        );

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_lookupIndexId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);

        Address resultAddress = new(result[12..]); // Extract address from last 20 bytes
        resultAddress.Should().Be(TestAddress);
    }

    [Test]
    public void ParsesRegister_ValidInputData_ReturnsIndex()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAddressTableParser.PrecompileFunctionDescription[_registerId].AbiFunctionDescription.GetCallInfo().Signature,
            TestAddress
        );

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_registerId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 resultIndex = new(result, isBigEndian: true);
        resultIndex.Should().Be(UInt256.Zero); // The first registered address gets index 0
    }

    [Test]
    public void ParsesSize_ValidInputData_ReturnsSize()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        // Register some addresses
        _arbosState.AddressTable.Register(new Address("0x1111111111111111111111111111111111111111"));
        _arbosState.AddressTable.Register(new Address("0x2222222222222222222222222222222222222222"));

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_sizeId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, []);

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
        BinaryPrimitives.WriteUInt32BigEndian(data, 0x12345678);
        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(data);

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? handler);
        exists.Should().BeFalse();
    }

    [Test]
    public void ParsesWithInvalidInputData_ThrowsRevertException()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };

        bool exists = ArbAddressTableParser.PrecompileImplementation.TryGetValue(_addressExistsId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, []);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = _worldState.BeginScope(IWorldState.PreGenesis);
        Block b = ArbOSInitialization.Create(_worldState);
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosState();
        _genesisBlockHeader = b.Header;
    }
}
