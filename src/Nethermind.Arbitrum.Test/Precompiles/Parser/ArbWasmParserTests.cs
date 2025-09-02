// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;
using static Nethermind.Arbitrum.Test.Infrastructure.ParserTestHelpers;
using static Nethermind.Core.Test.Builders.Build;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public sealed class ArbWasmParserTests
{
    private const ulong DefaultGasSupplied = 100000;
    private static readonly Address TestProgram = new("0x1234567890123456789012345678901234567890");
    private static readonly Hash256 TestCodeHash = new("0xabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd");
    private static readonly string ProgramAddressHex = TestProgram.ToString(false, false).PadLeft(64, '0');
    private static readonly string CodeHashHex = TestCodeHash.ToString(false);

    // Method IDs for ArbWasm methods
    private const string StylusVersionMethodId = "0xa996e0c2";
    private const string InkPriceMethodId = "0xd1c17abc";
    private const string MaxStackDepthMethodId = "0x8ccfaa70";
    private const string FreePagesMethodId = "0x4490c19d";
    private const string PageGasMethodId = "0x7af4ba49";
    private const string PageLimitMethodId = "0x9786f96e";
    private const string ActivateProgramMethodId = "0x58c780c2";
    private const string CodeHashVersionMethodId = "0xd70c0ca7";
    private const string CodeHashKeepaliveMethodId = "0xc689bad5";
    private const string CodeHashAsmSizeMethodId = "0x4089267f";
    private const string ProgramInitGasMethodId = "0x62b688aa";
    private const string ProgramMemoryFootprintMethodId = "0xaef36be3";
    private const string ProgramTimeLeftMethodId = "0xc775a62a";
    private const string PageRampMethodId = "0x11c82ae8";
    private const string MinInitGasMethodId = "0x99d0b38d";
    private const string InitCostScalarMethodId = "0x5fc94c0b";
    private const string ExpiryDaysMethodId = "0x309f6555";
    private const string KeepaliveDaysMethodId = "0x0a936455";
    private const string BlockCacheSizeMethodId = "0x7af6e819";
    private const string ProgramVersionMethodId = "0xcc8f4e88";

    private PrecompileTestContextBuilder _context = null!;
    private ArbWasmParser _parser = null!;

    [SetUp]
    public void SetUp()
    {
        (IWorldState worldState, _) = ArbOSInitialization.Create();
        ArbosState.OpenArbosState(worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(worldState, DefaultGasSupplied)
            .WithArbosState()
            .WithBlockExecutionContext(A.BlockHeader.TestObject)
            .WithReleaseSpec();
        _parser = new ArbWasmParser();
    }

    [Test]
    public void StylusVersion_WithValidInput_ReturnsEncodedVersion()
    {
        byte[] inputData = CreateMethodCallDataFromHex(StylusVersionMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(2); // Current stylus version
    }

    [Test]
    public void InkPrice_WithValidInput_ReturnsEncodedPrice()
    {
        byte[] inputData = CreateMethodCallDataFromHex(InkPriceMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 price = new(result, isBigEndian: true);
        price.Should().Be(10000); // InitialInkPrice = 10000
    }

    [Test]
    public void MaxStackDepth_WithValidInput_ReturnsEncodedDepth()
    {
        byte[] inputData = CreateMethodCallDataFromHex(MaxStackDepthMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 depth = new(result, isBigEndian: true);
        depth.Should().Be(262144); // InitialStackDepth = 4 * 65,536 = 262,144
    }

    [Test]
    public void FreePages_WithValidInput_ReturnsEncodedPages()
    {
        byte[] inputData = CreateMethodCallDataFromHex(FreePagesMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 pages = new(result, isBigEndian: true);
        pages.Should().Be(2); // InitialFreePages = 2
    }

    [Test]
    public void PageGas_WithValidInput_ReturnsEncodedGas()
    {
        byte[] inputData = CreateMethodCallDataFromHex(PageGasMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 gas = new(result, isBigEndian: true);
        gas.Should().Be(1000); // InitialPageGas = 1000
    }

    [Test]
    public void PageLimit_WithValidInput_ReturnsEncodedLimit()
    {
        byte[] inputData = CreateMethodCallDataFromHex(PageLimitMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 limit = new(result, isBigEndian: true);
        limit.Should().Be(128);
    }

    [Test]
    public void ActivateProgram_WithValidAddress_ThrowsOutOfGas()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ActivateProgramMethodId, ProgramAddressHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<OutOfGasException>();
    }

    [Test]
    public void CodeHashVersion_WithValidCodeHash_ReturnsVersion()
    {
        byte[] inputData = CreateMethodCallDataFromHex(CodeHashVersionMethodId, CodeHashHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(0); // Returns 0 for non-existent programs
    }

    [Test]
    public void CodeHashKeepalive_WithValidCodeHash_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(CodeHashKeepaliveMethodId, CodeHashHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void CodeHashAsmSize_WithValidCodeHash_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(CodeHashAsmSizeMethodId, CodeHashHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ProgramInitGas_WithValidAddress_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ProgramInitGasMethodId, ProgramAddressHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ProgramMemoryFootprint_WithValidAddress_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ProgramMemoryFootprintMethodId, ProgramAddressHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ProgramTimeLeft_WithValidAddress_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ProgramTimeLeftMethodId, ProgramAddressHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Parser_WithInvalidMethodId_ThrowsArgumentException()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };
        byte[] inputData = CreateMethodCallDataFromHex("0xFFFFFFFF");

        Action action = () => _parser.RunAdvanced(contextWithNoGas, inputData);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Unknown precompile method ID: *");
    }

    [Test]
    public void Parser_WithInvalidInputData_ThrowsEndOfStream()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };
        byte[] inputData = [0x01, 0x02]; // Less than 4 bytes

        Action action = () => _parser.RunAdvanced(contextWithNoGas, inputData);

        action.Should().Throw<EndOfStreamException>();
    }

    [Test]
    public void PageRamp_WithValidInput_ReturnsEncodedRamp()
    {
        byte[] inputData = CreateMethodCallDataFromHex(PageRampMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 ramp = new(result, isBigEndian: true);
        ramp.Should().Be(620674314); // InitialPageRamp = 620674314
    }

    [Test]
    public void MinInitGas_WithValidInput_ReturnsEncodedGas()
    {
        byte[] inputData = CreateMethodCallDataFromHex(MinInitGasMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(64); // Returns (gas, cached) tuple
        // Decode the tuple - first 32 bytes for gas, next 32 bytes for cached
        UInt256 gas = new(result.AsSpan(0, 32), isBigEndian: true);
        UInt256 cached = new(result.AsSpan(32, 32), isBigEndian: true);
        gas.Should().Be(8832); // V2MinInitGas = 8832
        cached.Should().Be(352); // InitialMinCachedGas = 11, 11 * 32 = 352
    }

    [Test]
    public void InitCostScalar_WithValidInput_ReturnsEncodedScalar()
    {
        byte[] inputData = CreateMethodCallDataFromHex(InitCostScalarMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 scalar = new(result, isBigEndian: true);
        scalar.Should().Be(100); // InitialInitCostScalar = 50, 50 * 2% = 100%
    }

    [Test]
    public void ExpiryDays_WithValidInput_ReturnsEncodedDays()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ExpiryDaysMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 days = new(result, isBigEndian: true);
        days.Should().Be(365); // InitialExpiryDays = 365
    }

    [Test]
    public void KeepaliveDays_WithValidInput_ReturnsEncodedDays()
    {
        byte[] inputData = CreateMethodCallDataFromHex(KeepaliveDaysMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 days = new(result, isBigEndian: true);
        days.Should().Be(31); // InitialKeepaliveDays = 31
    }

    [Test]
    public void BlockCacheSize_WithValidInput_ReturnsEncodedSize()
    {
        byte[] inputData = CreateMethodCallDataFromHex(BlockCacheSizeMethodId);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 size = new(result, isBigEndian: true);
        size.Should().Be(32); // InitialRecentCacheSize = 32
    }

    [Test]
    public void ProgramVersion_WithValidAddress_ReturnsVersion()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ProgramVersionMethodId, ProgramAddressHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(0); // Returns 0 for non-existent programs
    }

    [Test]
    public void ActivateProgram_WithInsufficientGas_ThrowsOutOfGas()
    {
        PrecompileTestContextBuilder contextWithLowGas = _context with { GasSupplied = 1000 };
        byte[] inputData = CreateMethodCallDataFromHex(ActivateProgramMethodId, ProgramAddressHex);

        Action action = () => _parser.RunAdvanced(contextWithLowGas, inputData);

        action.Should().Throw<OutOfGasException>();
    }

    [Test]
    public void CodeHashVersion_WithNonExistentCodeHash_ReturnsZero()
    {
        byte[] inputData = CreateMethodCallDataFromHex(CodeHashVersionMethodId, CodeHashHex);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(0); // Returns 0 for non-existent programs
    }

    [Test]
    public void ProgramInitGas_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ProgramInitGasMethodId, ProgramAddressHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramMemoryFootprint_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ProgramMemoryFootprintMethodId, ProgramAddressHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramTimeLeft_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(ProgramTimeLeftMethodId, ProgramAddressHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void CodeHashKeepalive_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        byte[] inputData = CreateMethodCallDataFromHex(CodeHashKeepaliveMethodId, CodeHashHex);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void Parser_WithEmptyInputData_ThrowsEndOfStream()
    {
        byte[] inputData = [];

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<EndOfStreamException>();
    }

    [Test]
    public void Parser_WithCorruptedMethodId_ThrowsArgumentException()
    {
        byte[] inputData = [0xFF, 0xFF, 0xFF, 0xFF]; // Invalid method ID

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Unknown precompile method ID: *");
    }
}
