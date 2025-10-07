// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public sealed class ArbWasmParserTests
{
    private const ulong DefaultGasSupplied = 100000;
    private static readonly Address TestProgram = new("0x1234567890123456789012345678901234567890");
    private static readonly Hash256 TestCodeHash = new("0xabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd");


    private PrecompileTestContextBuilder _context = null!;
    private ArbWasmParser _parser = null!;
    private IDisposable? _worldStateScope;

    // ABI signatures for ArbWasm methods
    private static readonly AbiSignature StylusVersionSignature = new("stylusVersion");
    private static readonly AbiSignature InkPriceSignature = new("inkPrice");
    private static readonly AbiSignature MaxStackDepthSignature = new("maxStackDepth");
    private static readonly AbiSignature FreePagesSignature = new("freePages");
    private static readonly AbiSignature PageGasSignature = new("pageGas");
    private static readonly AbiSignature PageLimitSignature = new("pageLimit");
    private static readonly AbiSignature ActivateProgramSignature = new("activateProgram", AbiType.Address);
    private static readonly AbiSignature CodeHashVersionSignature = new("codehashVersion", AbiType.Bytes32);
    private static readonly AbiSignature CodeHashKeepaliveSignature = new("codehashKeepalive", AbiType.Bytes32);
    private static readonly AbiSignature CodeHashAsmSizeSignature = new("codehashAsmSize", AbiType.Bytes32);
    private static readonly AbiSignature ProgramInitGasSignature = new("programInitGas", AbiType.Address);
    private static readonly AbiSignature ProgramMemoryFootprintSignature = new("programMemoryFootprint", AbiType.Address);
    private static readonly AbiSignature ProgramTimeLeftSignature = new("programTimeLeft", AbiType.Address);
    private static readonly AbiSignature PageRampSignature = new("pageRamp");
    private static readonly AbiSignature MinInitGasSignature = new("minInitGas");
    private static readonly AbiSignature InitCostScalarSignature = new("initCostScalar");
    private static readonly AbiSignature ExpiryDaysSignature = new("expiryDays");
    private static readonly AbiSignature KeepaliveDaysSignature = new("keepaliveDays");
    private static readonly AbiSignature BlockCacheSizeSignature = new("blockCacheSize");
    private static readonly AbiSignature ProgramVersionSignature = new("programVersion", AbiType.Address);

    [SetUp]
    public void SetUp()
    {
        IWorldState worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(worldState);
        ArbosState.OpenArbosState(worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(worldState, DefaultGasSupplied)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec();
        _parser = new ArbWasmParser();
    }

    [TearDown]
    public void TearDown()
    {
        _worldStateScope?.Dispose();
    }

    [Test]
    public void StylusVersion_WithValidInput_ReturnsEncodedVersion()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, StylusVersionSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(2); // Current stylus version
    }

    [Test]
    public void InkPrice_WithValidInput_ReturnsEncodedPrice()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, InkPriceSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 price = new(result, isBigEndian: true);
        price.Should().Be(10000); // InitialInkPrice = 10000
    }

    [Test]
    public void MaxStackDepth_WithValidInput_ReturnsEncodedDepth()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, MaxStackDepthSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 depth = new(result, isBigEndian: true);
        depth.Should().Be(262144); // InitialStackDepth = 4 * 65,536 = 262,144
    }

    [Test]
    public void FreePages_WithValidInput_ReturnsEncodedPages()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, FreePagesSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 pages = new(result, isBigEndian: true);
        pages.Should().Be(2); // InitialFreePages = 2
    }

    [Test]
    public void PageGas_WithValidInput_ReturnsEncodedGas()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, PageGasSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 gas = new(result, isBigEndian: true);
        gas.Should().Be(1000); // InitialPageGas = 1000
    }

    [Test]
    public void PageLimit_WithValidInput_ReturnsEncodedLimit()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, PageLimitSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 limit = new(result, isBigEndian: true);
        limit.Should().Be(128);
    }

    [Test]
    public void ActivateProgram_WithValidAddress_ThrowsOutOfGas()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ActivateProgramSignature, TestProgram);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashVersion_WithValidCodeHash_ReturnsVersion()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, CodeHashVersionSignature, TestCodeHash);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(0); // Returns 0 for non-existent programs
    }

    [Test]
    public void CodeHashKeepalive_WithNonExistentCodeHash_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, CodeHashKeepaliveSignature, TestCodeHash.Bytes.ToArray());

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void CodeHashAsmSize_WithNonExistentCodeHash_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, CodeHashAsmSizeSignature, TestCodeHash.Bytes.ToArray());

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ProgramInitGas_WithNonExistentAddress_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ProgramInitGasSignature, TestProgram);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ProgramMemoryFootprint_WithNonExistentAddress_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ProgramMemoryFootprintSignature, TestProgram);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ProgramTimeLeft_WithNonExistentAddress_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ProgramTimeLeftSignature, TestProgram);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Parser_WithInvalidMethodId_ThrowsArgumentException()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, new AbiSignature("0xFFFFFFFF"));

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
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, PageRampSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 ramp = new(result, isBigEndian: true);
        ramp.Should().Be(620674314); // InitialPageRamp = 620674314
    }

    [Test]
    public void MinInitGas_WithValidInput_ReturnsEncodedGas()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, MinInitGasSignature);

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
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, InitCostScalarSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 scalar = new(result, isBigEndian: true);
        scalar.Should().Be(100); // InitialInitCostScalar = 50, 50 * 2% = 100%
    }

    [Test]
    public void ExpiryDays_WithValidInput_ReturnsEncodedDays()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ExpiryDaysSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 days = new(result, isBigEndian: true);
        days.Should().Be(365); // InitialExpiryDays = 365
    }

    [Test]
    public void KeepaliveDays_WithValidInput_ReturnsEncodedDays()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, KeepaliveDaysSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 days = new(result, isBigEndian: true);
        days.Should().Be(31); // InitialKeepaliveDays = 31
    }

    [Test]
    public void BlockCacheSize_WithValidInput_ReturnsEncodedSize()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, BlockCacheSizeSignature);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 size = new(result, isBigEndian: true);
        size.Should().Be(32); // InitialRecentCacheSize = 32
    }

    [Test]
    public void ProgramVersion_WithValidAddress_ReturnsVersion()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ProgramVersionSignature, TestProgram);

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
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ActivateProgramSignature, TestProgram);

        Action action = () => _parser.RunAdvanced(contextWithLowGas, inputData);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashVersion_WithNonExistentCodeHash_ReturnsZero()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, CodeHashVersionSignature, TestCodeHash);

        byte[] result = _parser.RunAdvanced(_context, inputData);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(0); // Returns 0 for non-existent programs
    }

    [Test]
    public void ProgramInitGas_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ProgramInitGasSignature, TestProgram);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramMemoryFootprint_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ProgramMemoryFootprintSignature, TestProgram);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void ProgramTimeLeft_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, ProgramTimeLeftSignature, TestProgram);

        Action action = () => _parser.RunAdvanced(_context, inputData);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProgramNotActivated*");
    }

    [Test]
    public void CodeHashKeepalive_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        byte[] inputData = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, CodeHashKeepaliveSignature, TestCodeHash.Bytes.ToArray());

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
