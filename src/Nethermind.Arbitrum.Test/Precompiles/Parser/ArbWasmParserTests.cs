// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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

    private static readonly uint ActivateProgramId = PrecompileTestAbiHelpers.GetMethodId("activateProgram(address)");
    private static readonly uint CodeHashKeepaliveId = PrecompileTestAbiHelpers.GetMethodId("codehashKeepalive(bytes32)");
    private static readonly uint StylusVersionId = PrecompileTestAbiHelpers.GetMethodId("stylusVersion()");
    private static readonly uint InkPriceId = PrecompileTestAbiHelpers.GetMethodId("inkPrice()");
    private static readonly uint MaxStackDepthId = PrecompileTestAbiHelpers.GetMethodId("maxStackDepth()");
    private static readonly uint FreePagesId = PrecompileTestAbiHelpers.GetMethodId("freePages()");
    private static readonly uint PageGasId = PrecompileTestAbiHelpers.GetMethodId("pageGas()");
    private static readonly uint PageRampId = PrecompileTestAbiHelpers.GetMethodId("pageRamp()");
    private static readonly uint PageLimitId = PrecompileTestAbiHelpers.GetMethodId("pageLimit()");
    private static readonly uint MinInitGasId = PrecompileTestAbiHelpers.GetMethodId("minInitGas()");
    private static readonly uint InitCostScalarId = PrecompileTestAbiHelpers.GetMethodId("initCostScalar()");
    private static readonly uint ExpiryDaysId = PrecompileTestAbiHelpers.GetMethodId("expiryDays()");
    private static readonly uint KeepaliveDaysId = PrecompileTestAbiHelpers.GetMethodId("keepaliveDays()");
    private static readonly uint BlockCacheSizeId = PrecompileTestAbiHelpers.GetMethodId("blockCacheSize()");
    private static readonly uint CodeHashVersionId = PrecompileTestAbiHelpers.GetMethodId("codehashVersion(bytes32)");
    private static readonly uint CodeHashAsmSizeId = PrecompileTestAbiHelpers.GetMethodId("codehashAsmSize(bytes32)");
    private static readonly uint ProgramVersionId = PrecompileTestAbiHelpers.GetMethodId("programVersion(address)");
    private static readonly uint ProgramInitGasId = PrecompileTestAbiHelpers.GetMethodId("programInitGas(address)");
    private static readonly uint ProgramMemoryFootprintId = PrecompileTestAbiHelpers.GetMethodId("programMemoryFootprint(address)");
    private static readonly uint ProgramTimeLeftId = PrecompileTestAbiHelpers.GetMethodId("programTimeLeft(address)");

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
        ArbWasmParser.PrecompileImplementation.TryGetValue(StylusVersionId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 version = new(result, isBigEndian: true);
        version.Should().Be(2); // Current stylus version
    }

    [Test]
    public void InkPrice_WithValidInput_ReturnsEncodedPrice()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(InkPriceId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 price = new(result, isBigEndian: true);
        price.Should().Be(10000); // InitialInkPrice = 10000
    }

    [Test]
    public void MaxStackDepth_WithValidInput_ReturnsEncodedDepth()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(MaxStackDepthId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 depth = new(result, isBigEndian: true);
        depth.Should().Be(262144); // InitialStackDepth = 4 * 65,536 = 262,144
    }

    [Test]
    public void FreePages_WithValidInput_ReturnsEncodedPages()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(FreePagesId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 pages = new(result, isBigEndian: true);
        pages.Should().Be(2); // InitialFreePages = 2
    }

    [Test]
    public void PageGas_WithValidInput_ReturnsEncodedGas()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(PageGasId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 gas = new(result, isBigEndian: true);
        gas.Should().Be(1000); // InitialPageGas = 1000
    }

    [Test]
    public void PageLimit_WithValidInput_ReturnsEncodedLimit()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(PageLimitId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 limit = new(result, isBigEndian: true);
        limit.Should().Be(128);
    }

    [Test]
    public void ActivateProgram_WithValidAddress_ThrowsOutOfGas()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(ActivateProgramId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ActivateProgramId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(CodeHashVersionId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[CodeHashVersionId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(CodeHashKeepaliveId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[CodeHashKeepaliveId].AbiFunctionDescription.GetCallInfo().Signature, TestCodeHash.Bytes.ToArray());
        Action action = () => handler!(_context, calldata);

        action.Should().Throw<ArbitrumPrecompileException>();
    }

    [Test]
    public void CodeHashAsmSize_WithNonExistentCodeHash_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(CodeHashAsmSizeId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[CodeHashAsmSizeId].AbiFunctionDescription.GetCallInfo().Signature,
            TestCodeHash.Bytes.ToArray()
        );
        Action action = () => handler!(_context, calldata);

        action.Should().Throw<ArbitrumPrecompileException>();
    }

    [Test]
    public void ProgramInitGas_WithNonExistentAddress_ThrowsArbitrumPrecompileException()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(ProgramInitGasId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ProgramInitGasId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(ProgramMemoryFootprintId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ProgramMemoryFootprintId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(ProgramTimeLeftId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ProgramTimeLeftId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(PageRampId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 ramp = new(result, isBigEndian: true);
        ramp.Should().Be(620674314); // InitialPageRamp = 620674314
    }

    [Test]
    public void MinInitGas_WithValidInput_ReturnsEncodedGas()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(MinInitGasId, out PrecompileHandler? handler);
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(InitCostScalarId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 scalar = new(result, isBigEndian: true);
        scalar.Should().Be(100); // InitialInitCostScalar = 50, 50 * 2% = 100%
    }

    [Test]
    public void ExpiryDays_WithValidInput_ReturnsEncodedDays()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(ExpiryDaysId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 days = new(result, isBigEndian: true);
        days.Should().Be(365); // InitialExpiryDays = 365
    }

    [Test]
    public void KeepaliveDays_WithValidInput_ReturnsEncodedDays()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(KeepaliveDaysId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 days = new(result, isBigEndian: true);
        days.Should().Be(31); // InitialKeepaliveDays = 31
    }

    [Test]
    public void BlockCacheSize_WithValidInput_ReturnsEncodedSize()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(BlockCacheSizeId, out PrecompileHandler? handler);
        byte[] result = handler!(_context, []);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 size = new(result, isBigEndian: true);
        size.Should().Be(32); // InitialRecentCacheSize = 32
    }

    [Test]
    public void ProgramVersion_NonExistingProgram_ThrowsProgramNotActivatedError()
    {
        ArbWasmParser.PrecompileImplementation.TryGetValue(ProgramVersionId, out PrecompileHandler? handler);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ProgramVersionId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(ActivateProgramId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ActivateProgramId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(CodeHashVersionId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[CodeHashVersionId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(ProgramInitGasId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ProgramInitGasId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(ProgramMemoryFootprintId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ProgramMemoryFootprintId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(ProgramTimeLeftId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[ProgramTimeLeftId].AbiFunctionDescription.GetCallInfo().Signature,
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
        ArbWasmParser.PrecompileImplementation.TryGetValue(CodeHashKeepaliveId, out PrecompileHandler? handler);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbWasmParser.PrecompileFunctionDescription[CodeHashKeepaliveId].AbiFunctionDescription.GetCallInfo().Signature, TestCodeHash.BytesToArray());

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException expected = ArbWasm.ProgramNotActivatedError();
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }
}
