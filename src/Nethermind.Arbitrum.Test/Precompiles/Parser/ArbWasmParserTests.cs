// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
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

    private static readonly uint _activateProgramId = PrecompileHelper.GetMethodId("activateProgram(address)");
    private static readonly uint _codeHashKeepaliveId = PrecompileHelper.GetMethodId("codehashKeepalive(bytes32)");
    private static readonly uint _stylusVersionId = PrecompileHelper.GetMethodId("stylusVersion()");
    private static readonly uint _inkPriceId = PrecompileHelper.GetMethodId("inkPrice()");
    private static readonly uint _maxStackDepthId = PrecompileHelper.GetMethodId("maxStackDepth()");
    private static readonly uint _freePagesId = PrecompileHelper.GetMethodId("freePages()");
    private static readonly uint _pageGasId = PrecompileHelper.GetMethodId("pageGas()");
    private static readonly uint _pageRampId = PrecompileHelper.GetMethodId("pageRamp()");
    private static readonly uint _pageLimitId = PrecompileHelper.GetMethodId("pageLimit()");
    private static readonly uint _minInitGasId = PrecompileHelper.GetMethodId("minInitGas()");
    private static readonly uint _initCostScalarId = PrecompileHelper.GetMethodId("initCostScalar()");
    private static readonly uint _expiryDaysId = PrecompileHelper.GetMethodId("expiryDays()");
    private static readonly uint _keepaliveDaysId = PrecompileHelper.GetMethodId("keepaliveDays()");
    private static readonly uint _blockCacheSizeId = PrecompileHelper.GetMethodId("blockCacheSize()");
    private static readonly uint _codeHashVersionId = PrecompileHelper.GetMethodId("codehashVersion(bytes32)");
    private static readonly uint _codeHashAsmSizeId = PrecompileHelper.GetMethodId("codehashAsmSize(bytes32)");
    private static readonly uint _programVersionId = PrecompileHelper.GetMethodId("programVersion(address)");
    private static readonly uint _programInitGasId = PrecompileHelper.GetMethodId("programInitGas(address)");
    private static readonly uint _programMemoryFootprintId = PrecompileHelper.GetMethodId("programMemoryFootprint(address)");
    private static readonly uint _programTimeLeftId = PrecompileHelper.GetMethodId("programTimeLeft(address)");

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
        ArbWasmParser.PrecompileImplementation.TryGetValue(_stylusVersionId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(2); // Current stylus version
    }

    [Test]
    public void InkPrice_WithValidInput_ReturnsEncodedPrice()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_inkPriceId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 price = new(result, isBigEndian: true);
        price.Should().Be(10000); // InitialInkPrice = 10000
    }

    [Test]
    public void MaxStackDepth_WithValidInput_ReturnsEncodedDepth()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_maxStackDepthId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 depth = new(result, isBigEndian: true);
        depth.Should().Be(262144); // InitialStackDepth = 4 * 65,536 = 262,144
    }

    [Test]
    public void FreePages_WithValidInput_ReturnsEncodedPages()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_freePagesId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 pages = new(result, isBigEndian: true);
        pages.Should().Be(2); // InitialFreePages = 2
    }

    [Test]
    public void PageGas_WithValidInput_ReturnsEncodedGas()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_pageGasId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 gas = new(result, isBigEndian: true);
        gas.Should().Be(1000); // InitialPageGas = 1000
    }

    [Test]
    public void PageLimit_WithValidInput_ReturnsEncodedLimit()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_pageLimitId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 limit = new(result, isBigEndian: true);
        limit.Should().Be(128);
    }

    [Test]
    public void ActivateProgram_WithValidAddress_ThrowsOutOfGas()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_activateProgramId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_activateProgramId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );
        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashVersion_WithInValidCodeHash_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_codeHashVersionId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_codeHashVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            TestCodeHash
        );
        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
		ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
		exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashKeepalive_WithNonExistentCodeHash_ThrowsInvalidOperation()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_codeHashKeepaliveId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_codeHashKeepaliveId].AbiFunctionDescription.GetCallInfo().Signature,
            [TestCodeHash.Bytes.ToArray()]
        );
        Action action = () => handler!(_context, calldata);

        action.Should().Throw<ArbitrumPrecompileException>();
    }

    [Test]
    public void CodeHashAsmSize_WithNonExistentCodeHash_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_codeHashAsmSizeId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_codeHashAsmSizeId].AbiFunctionDescription.GetCallInfo().Signature,
            TestCodeHash.Bytes.ToArray()
        );
        Action action = () => handler!(_context, calldata);

        action.Should().Throw<ArbitrumPrecompileException>();
    }

    [Test]
    public void ProgramInitGas_WithNonExistentAddress_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_programInitGasId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_programInitGasId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );
        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
		ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
		exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramMemoryFootprint_WithNonExistentAddress_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_programMemoryFootprintId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_programMemoryFootprintId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );
        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramTimeLeft_WithNonExistentAddress_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_programTimeLeftId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_programTimeLeftId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );
        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void Parser_WithInvalidMethodId_HandlerDoesNotExist()
    {
        PrecompileTestContextBuilder contextWithNoGas = _context with { GasSupplied = 0 };
        uint methodId = 1234; // incorrect method id
        bool exists = ArbWasmParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? handler);
        exists.Should().BeFalse();
    }

    [Test]
    public void PageRamp_WithValidInput_ReturnsEncodedRamp()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_pageRampId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 ramp = new(result, isBigEndian: true);
        ramp.Should().Be(620674314); // InitialPageRamp = 620674314
    }

    [Test]
    public void MinInitGas_WithValidInput_ReturnsEncodedGas()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_minInitGasId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

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
        ArbWasmParser.PrecompileImplementation.TryGetValue(_initCostScalarId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 scalar = new(result, isBigEndian: true);
        scalar.Should().Be(100); // InitialInitCostScalar = 50, 50 * 2% = 100%
    }

    [Test]
    public void ExpiryDays_WithValidInput_ReturnsEncodedDays()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_expiryDaysId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 days = new(result, isBigEndian: true);
        days.Should().Be(365); // InitialExpiryDays = 365
    }

    [Test]
    public void KeepaliveDays_WithValidInput_ReturnsEncodedDays()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_keepaliveDaysId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 days = new(result, isBigEndian: true);
        days.Should().Be(31); // InitialKeepaliveDays = 31
    }

    [Test]
    public void BlockCacheSize_WithValidInput_ReturnsEncodedSize()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_blockCacheSizeId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 size = new(result, isBigEndian: true);
        size.Should().Be(32); // InitialRecentCacheSize = 32
    }

    [Test]
    public void ProgramVersion_NonExistingProgram_ThrowsProgramNotActivatedError()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_programVersionId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_programVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );
        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ActivateProgram_WithInsufficientGas_ThrowsOutOfGas()
    {
        PrecompileTestContextBuilder contextWithLowGas = _context with { GasSupplied = 1000 };
        ArbWasmParser.PrecompileImplementation.TryGetValue(_activateProgramId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_activateProgramId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );

        Action action = () => handler!(contextWithLowGas, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashVersion_WithNonExistentCodeHash_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_codeHashVersionId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_codeHashVersionId].AbiFunctionDescription.GetCallInfo().Signature,
            TestCodeHash
        );

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
		ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
		exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramInitGas_WithNonActivatedProgram_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_programInitGasId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_programInitGasId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
		ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
		exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramMemoryFootprint_WithNonActivatedProgram_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_programMemoryFootprintId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_programMemoryFootprintId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramTimeLeft_WithNonActivatedProgram_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_programTimeLeftId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_programTimeLeftId].AbiFunctionDescription.GetCallInfo().Signature,
            TestProgram
        );

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashKeepalive_WithNonActivatedProgram_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(_codeHashKeepaliveId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[_codeHashKeepaliveId].AbiFunctionDescription.GetCallInfo().Signature,
            [TestCodeHash.BytesToArray()]
        );

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
		ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
		exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }
}
