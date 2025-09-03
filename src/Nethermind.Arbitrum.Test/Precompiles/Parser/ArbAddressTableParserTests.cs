using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using static Nethermind.Core.Extensions.Bytes;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public sealed class ArbAddressTableParserTests
{
    private const ulong DefaultGasSupplied = 100000;
    private static readonly Address TestAddress = new("0x1234567890123456789012345678901234567890");
    private static readonly string AddressHex = TestAddress.ToString(false, false).PadLeft(64, '0');

    private const string AddressExistsMethodId = "0xa5025222";
    private const string CompressMethodId = "0xf6a455a2";
    private const string LookupMethodId = "0xd4b6b5da";
    private const string LookupIndexMethodId = "0x8a186788";
    private const string RegisterMethodId = "0x4420e486";
    private const string SizeMethodId = "0x949d225d";

    private ArbosState _arbosState = null!;
    private PrecompileTestContextBuilder _context = null!;
    private ArbAddressTableParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        IWorldStateManager worldStateManager = TestWorldStateFactory.CreateForTest();
        IWorldState worldState = worldStateManager.GlobalWorldState;
        using var worldStateDisposer = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);
        _arbosState = ArbosState.OpenArbosState(worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(worldState, DefaultGasSupplied)
            .WithArbosState();
        _parser = new ArbAddressTableParser();
    }

    private static byte[] CreateMethodCallData(string methodId, ReadOnlySpan<char> parameters = default)
    {
        string hexString = methodId + new string(parameters);
        return FromHexString(hexString);
    }

    [Test]
    public void ParsesAddressExists_ValidInputData_ReturnsTrue()
    {
        _arbosState.AddressTable.Register(TestAddress);

        byte[] inputData = CreateMethodCallData(AddressExistsMethodId, AddressHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        result[31].Should().Be(1); // ABI-encoded boolean true
    }

    [Test]
    public void ParsesAddressExists_ValidInputData_ReturnsFalse()
    {
        // Don't register the address

        byte[] inputData = CreateMethodCallData(AddressExistsMethodId, AddressHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        result[31].Should().Be(0); // ABI-encoded boolean false
    }

    [Test]
    public void ParsesCompress_ValidInputData_ReturnsCompressedBytes()
    {
        byte[] inputData = CreateMethodCallData(CompressMethodId, AddressHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(32); // ABI-encoded bytes include offset and length
    }

    [Test]
    public void ParsesLookup_ValidInputData_ReturnsIndex()
    {
        ulong expectedIndex = _arbosState.AddressTable.Register(TestAddress);

        byte[] inputData = CreateMethodCallData(LookupMethodId, AddressHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 resultIndex = new(result, isBigEndian: true);
        resultIndex.Should().Be(new UInt256(expectedIndex));
    }

    [Test]
    public void ParsesLookup_WithUnregisteredAddress_Throws()
    {
        byte[] inputData = CreateMethodCallData(LookupMethodId, AddressHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<ArgumentException>()
              .WithMessage($"Address {TestAddress} does not exist in AddressTable");
    }

    [Test]
    public void ParsesLookupIndex_ValidInputData_ReturnsAddress()
    {
        ulong index = _arbosState.AddressTable.Register(TestAddress);

        string indexHex = new UInt256(index).ToString("x64");
        byte[] inputData = CreateMethodCallData(LookupIndexMethodId, indexHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);

        Address resultAddress = new(result[12..]); // Extract address from last 20 bytes
        resultAddress.Should().Be(TestAddress);
    }

    [Test]
    public void ParsesRegister_ValidInputData_ReturnsIndex()
    {
        byte[] inputData = CreateMethodCallData(RegisterMethodId, AddressHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 resultIndex = new(result, isBigEndian: true);
        resultIndex.Should().Be(UInt256.Zero); // The first registered address gets index 0
    }

    [Test]
    public void ParsesSize_ValidInputData_ReturnsSize()
    {
        // Register some addresses
        _arbosState.AddressTable.Register(new Address("0x1111111111111111111111111111111111111111"));
        _arbosState.AddressTable.Register(new Address("0x2222222222222222222222222222222222222222"));

        byte[] inputData = CreateMethodCallData(SizeMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 resultSize = new(result, isBigEndian: true);
        resultSize.Should().Be(new UInt256(2));
    }

    [Test]
    public void ParsesInvalidMethodId_Throws()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };
        byte[] inputData = CreateMethodCallData("0x12345678");

        Action action = () => _parser.RunAdvanced(contextWithNoGas, inputData);

        action.Should().Throw<ArgumentException>()
              .WithMessage("Invalid precompile method ID: *");
    }

    [Test]
    public void ParsesWithInvalidInputData_Throws()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };
        byte[] inputData = CreateMethodCallData(AddressExistsMethodId, "12"); // Too short

        Action action = () => _parser.RunAdvanced(contextWithNoGas, inputData);

        action.Should().Throw<EndOfStreamException>();
    }
}
