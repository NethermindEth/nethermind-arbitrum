// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Logging;
using static Nethermind.Arbitrum.Precompiles.ArbWasm;
using Address = Nethermind.Core.Address;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public sealed class ArbWasmTests
{
    private const ulong DefaultGasSupplied = 1_000_000;
    private const ushort InitialExpiryDays = 365;
    private const ulong ActivationFixedCost = 1_659_168;
    private static readonly Hash256 NonActivatedCodeHash = Hash256.Zero;
    private static readonly Address NonActivatedProgram = Address.Zero;

    private IWorldState _worldState = null!;
    private ArbosState _arbosState = null!;
    private PrecompileTestContextBuilder _context = null!;
    private IDisposable? _worldStateScope;
    private StackAccessTracker _accessTracker;

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = _worldState.BeginScope(IWorldState.PreGenesis);

        _ = ArbOSInitialization.Create(_worldState);
        _arbosState = ArbosState.OpenArbosState(
            _worldState,
            new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());

        // The fixture owns the tracker; tests reference _accessTracker rather than relying on the
        // record's AccessTracker copy, which would alias the same _trackingState after Dispose.
        _accessTracker = new StackAccessTracker();
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
        {
            AccessTracker = _accessTracker,
        }
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec();
    }

    [TearDown]
    public void TearDown()
    {
        _accessTracker.Dispose();
        _worldStateScope?.Dispose();
    }

    [Test]
    public void StylusVersion_Always_ReturnsCurrentVersion()
    {
        ushort version = StylusVersion(_context);

        version.Should().Be(2);
    }

    [Test]
    public void InkPrice_Always_ReturnsPositiveValue()
    {
        uint price = InkPrice(_context);

        price.Should().Be(10_000); // InitialInkPrice = 10,000
    }

    [Test]
    public void MaxStackDepth_Always_ReturnsPositiveValue()
    {
        uint depth = MaxStackDepth(_context);

        depth.Should().Be(262_144); // InitialStackDepth = 4 * 65,536 = 262,144
    }

    [Test]
    public void FreePages_Always_ReturnsNonNegativeValue()
    {
        ushort pages = FreePages(_context);

        pages.Should().Be(2); // InitialFreePages = 2
    }

    [Test]
    public void PageGas_Always_ReturnsPositiveValue()
    {
        ushort gas = PageGas(_context);

        gas.Should().Be(1_000); // InitialPageGas = 1,000
    }

    [Test]
    public void PageRamp_Always_ReturnsPositiveValue()
    {
        ulong ramp = PageRamp(_context);

        ramp.Should().Be(620_674_314); // InitialPageRamp = 620,674,314
    }

    [Test]
    public void PageLimit_Always_ReturnsPositiveValue()
    {
        ushort limit = PageLimit(_context);

        limit.Should().Be(128); // InitialPageLimit = 128
    }

    [Test]
    public void MinInitGas_WithSupportedVersion_ReturnsValidValues()
    {
        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.StylusChargingFixes);

        (ulong gas, ulong cached) = MinInitGas(context);

        gas.Should().Be(8_832); // 69 * 128 = 8,832 (V2MinInitGas * MinInitGasUnits)
        cached.Should().Be(352); // 11 * 32 = 352 (InitialMinCachedGas * MinCachedGasUnits)
    }

    [Test]
    public void MinInitGas_WithUnsupportedVersion_ThrowsRevertException()
    {
        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosState()
            .WithArbosVersion(ArbosVersion.StylusChargingFixes - 1);

        Action act = () => MinInitGas(context);

        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void InitCostScalar_Always_ReturnsPositiveValue()
    {
        ulong percent = InitCostScalar(_context);

        percent.Should().Be(100);
    }

    [Test]
    public void ExpiryDays_Always_ReturnsPositiveValue()
    {
        ushort days = ExpiryDays(_context);

        days.Should().Be(InitialExpiryDays);
    }

    [Test]
    public void KeepaliveDays_Always_ReturnsPositiveValue()
    {
        ushort days = KeepaliveDays(_context);

        days.Should().Be(31); // InitialKeepaliveDays = 31
    }

    [Test]
    public void BlockCacheSize_Always_ReturnsNonNegativeValue()
    {
        ushort count = BlockCacheSize(_context);

        count.Should().Be(32); // InitialRecentCacheSize = 32
    }

    [Test]
    public void CodeHashVersion_WithNonExistentCodeHash_ThrowsProgramNotActivatedError()
    {
        Hash256 nonExistentCodeHash = Hash256.Zero;

        Action action = () => CodeHashVersion(_context, nonExistentCodeHash);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramVersion_WithNonExistentProgram_ThrowsProgramNotActivatedError()
    {
        Address nonExistentProgram = Address.Zero;

        Action action = () => ProgramVersion(_context, nonExistentProgram);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashAsmSize_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Action act = () => CodeHashAsmSize(_context, NonActivatedCodeHash);

        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramInitGas_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Action act = () => ProgramInitGas(_context, NonActivatedProgram);

        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramMemoryFootprint_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Action act = () => ProgramMemoryFootprint(_context, NonActivatedProgram);

        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ProgramTimeLeft_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Action act = () => ProgramTimeLeft(_context, NonActivatedProgram);

        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashKeepalive_WithTooEarlyKeepalive_ThrowsInvalidOperation()
    {
        Hash256 codeHash = Hash256.Zero;

        Action act = () => CodeHashKeepAlive(_context, codeHash);

        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ActivateProgram_WithInsufficientValue_ThrowsOutOfGas()
    {
        Address program = Address.Zero;

        Action action = () => ActivateProgram(_context, program);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ActivateProgram_WithAnyCall_BurnsFixedCost()
    {
        PrecompileTestContextBuilder context = new(_worldState, 2_000_000)
        {
            ArbosState = _arbosState,
            AccessTracker = _accessTracker,
        };
        Address program = Address.Zero;
        ulong initialGas = context.GasLeft;

        try
        {
            ActivateProgram(context, program);
        }
        catch
        {
            // Expected to fail due to missing program, but should still burn gas
        }

        ulong gasUsed = initialGas - context.GasLeft;
        gasUsed.Should().Be(ActivationFixedCost);
    }

    [Test]
    public void ActivateProgram_WithNonExistentProgram_ThrowsOutOfGas()
    {
        Address nonExistentProgram = new("0x1234567890123456789012345678901234567890");

        Action action = () => ActivateProgram(_context, nonExistentProgram);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ActivateProgram_WithZeroValue_ThrowsOutOfGas()
    {
        Address program = Address.Zero;

        Action action = () => ActivateProgram(_context, program);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateOutOfGasException();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashKeepAlive_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Hash256 nonActivatedCodeHash = Hash256.Zero;

        Action act = () => CodeHashKeepAlive(_context, nonActivatedCodeHash);

        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void CodeHashKeepAlive_WithInsufficientValue_ThrowsInvalidOperation()
    {
        Hash256 codeHash = Hash256.Zero;

        Action act = () => CodeHashKeepAlive(_context, codeHash);

        ArbitrumPrecompileException exception = act.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ProgramNotActivatedError();
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ExpiryDays_WithDefaultParams_ReturnsPositiveValue()
    {
        ushort days = ExpiryDays(_context);

        days.Should().Be(InitialExpiryDays);
    }

    [Test]
    public void KeepaliveDays_WithDefaultParams_ReturnsPositiveValue()
    {
        ushort days = KeepaliveDays(_context);

        days.Should().Be(31);
    }

    [Test]
    public void BlockCacheSize_WithDefaultParams_ReturnsNonNegativeValue()
    {
        ushort size = BlockCacheSize(_context);

        size.Should().Be(32);
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbWasm.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileTestAbiHelpers.GetMethodId("activateProgram(address)"),
            PrecompileTestAbiHelpers.GetMethodId("codehashKeepalive(bytes32)"),
            PrecompileTestAbiHelpers.GetMethodId("stylusVersion()"),
            PrecompileTestAbiHelpers.GetMethodId("inkPrice()"),
            PrecompileTestAbiHelpers.GetMethodId("maxStackDepth()"),
            PrecompileTestAbiHelpers.GetMethodId("freePages()"),
            PrecompileTestAbiHelpers.GetMethodId("pageGas()"),
            PrecompileTestAbiHelpers.GetMethodId("pageRamp()"),
            PrecompileTestAbiHelpers.GetMethodId("pageLimit()"),
            PrecompileTestAbiHelpers.GetMethodId("minInitGas()"),
            PrecompileTestAbiHelpers.GetMethodId("initCostScalar()"),
            PrecompileTestAbiHelpers.GetMethodId("expiryDays()"),
            PrecompileTestAbiHelpers.GetMethodId("keepaliveDays()"),
            PrecompileTestAbiHelpers.GetMethodId("blockCacheSize()"),
            PrecompileTestAbiHelpers.GetMethodId("codehashVersion(bytes32)"),
            PrecompileTestAbiHelpers.GetMethodId("codehashAsmSize(bytes32)"),
            PrecompileTestAbiHelpers.GetMethodId("programVersion(address)"),
            PrecompileTestAbiHelpers.GetMethodId("programInitGas(address)"),
            PrecompileTestAbiHelpers.GetMethodId("programMemoryFootprint(address)"),
            PrecompileTestAbiHelpers.GetMethodId("programTimeLeft(address)"),
            PrecompileTestAbiHelpers.GetMethodId("activationGas()"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedEvents()
    {
        Dictionary<string, AbiEventDescription> allEvents = PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbWasm.Abi);

        allEvents.Keys.Should().BeEquivalentTo("ProgramActivated", "ProgramLifetimeExtended");
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedErrors()
    {
        Dictionary<string, AbiErrorDescription> allErrors = PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbWasm.Abi);

        allErrors.Keys.Should().BeEquivalentTo("ProgramNotWasm", "ProgramNotActivated", "ProgramNeedsUpgrade", "ProgramExpired", "ProgramUpToDate", "ProgramKeepaliveTooSoon", "ProgramInsufficientValue");
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("activateProgram(address)").Should().Be(Solgen.ArbWasm.Methods.ActivateProgram);
        PrecompileTestAbiHelpers.GetMethodId("codehashKeepalive(bytes32)").Should().Be(Solgen.ArbWasm.Methods.CodehashKeepalive);
        PrecompileTestAbiHelpers.GetMethodId("stylusVersion()").Should().Be(Solgen.ArbWasm.Methods.StylusVersion);
        PrecompileTestAbiHelpers.GetMethodId("inkPrice()").Should().Be(Solgen.ArbWasm.Methods.InkPrice);
        PrecompileTestAbiHelpers.GetMethodId("maxStackDepth()").Should().Be(Solgen.ArbWasm.Methods.MaxStackDepth);
        PrecompileTestAbiHelpers.GetMethodId("freePages()").Should().Be(Solgen.ArbWasm.Methods.FreePages);
        PrecompileTestAbiHelpers.GetMethodId("pageGas()").Should().Be(Solgen.ArbWasm.Methods.PageGas);
        PrecompileTestAbiHelpers.GetMethodId("pageRamp()").Should().Be(Solgen.ArbWasm.Methods.PageRamp);
        PrecompileTestAbiHelpers.GetMethodId("pageLimit()").Should().Be(Solgen.ArbWasm.Methods.PageLimit);
        PrecompileTestAbiHelpers.GetMethodId("minInitGas()").Should().Be(Solgen.ArbWasm.Methods.MinInitGas);
        PrecompileTestAbiHelpers.GetMethodId("initCostScalar()").Should().Be(Solgen.ArbWasm.Methods.InitCostScalar);
        PrecompileTestAbiHelpers.GetMethodId("expiryDays()").Should().Be(Solgen.ArbWasm.Methods.ExpiryDays);
        PrecompileTestAbiHelpers.GetMethodId("keepaliveDays()").Should().Be(Solgen.ArbWasm.Methods.KeepaliveDays);
        PrecompileTestAbiHelpers.GetMethodId("blockCacheSize()").Should().Be(Solgen.ArbWasm.Methods.BlockCacheSize);
        PrecompileTestAbiHelpers.GetMethodId("codehashVersion(bytes32)").Should().Be(Solgen.ArbWasm.Methods.CodehashVersion);
        PrecompileTestAbiHelpers.GetMethodId("codehashAsmSize(bytes32)").Should().Be(Solgen.ArbWasm.Methods.CodehashAsmSize);
        PrecompileTestAbiHelpers.GetMethodId("programVersion(address)").Should().Be(Solgen.ArbWasm.Methods.ProgramVersion);
        PrecompileTestAbiHelpers.GetMethodId("programInitGas(address)").Should().Be(Solgen.ArbWasm.Methods.ProgramInitGas);
        PrecompileTestAbiHelpers.GetMethodId("programMemoryFootprint(address)").Should().Be(Solgen.ArbWasm.Methods.ProgramMemoryFootprint);
        PrecompileTestAbiHelpers.GetMethodId("programTimeLeft(address)").Should().Be(Solgen.ArbWasm.Methods.ProgramTimeLeft);
    }

    [Test]
    public void EventTopics_AllEvents_MatchExpectedHashes()
    {
        Dictionary<string, AbiEventDescription> events = PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbWasm.Abi);

        events["ProgramActivated"].GetHash().Should().Be(
            new Hash256(Solgen.ArbWasm.Events.ProgramActivated.Topic0Hex));

        events["ProgramLifetimeExtended"].GetHash().Should().Be(
            new Hash256(Solgen.ArbWasm.Events.ProgramLifetimeExtended.Topic0Hex));
    }

    [Test]
    public void ErrorSelectors_AllErrors_MatchExpectedValues()
    {
        Dictionary<string, AbiErrorDescription> errors = PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbWasm.Abi);

        errors["ProgramNotWasm"].GetSelector().Should().Be(Solgen.ArbWasm.Errors.ProgramNotWasm.Selector);
        errors["ProgramNotActivated"].GetSelector().Should().Be(Solgen.ArbWasm.Errors.ProgramNotActivated.Selector);
        errors["ProgramNeedsUpgrade"].GetSelector().Should().Be(Solgen.ArbWasm.Errors.ProgramNeedsUpgrade.Selector);
        errors["ProgramExpired"].GetSelector().Should().Be(Solgen.ArbWasm.Errors.ProgramExpired.Selector);
        errors["ProgramUpToDate"].GetSelector().Should().Be(Solgen.ArbWasm.Errors.ProgramUpToDate.Selector);
        errors["ProgramKeepaliveTooSoon"].GetSelector().Should().Be(Solgen.ArbWasm.Errors.ProgramKeepaliveTooSoon.Selector);
        errors["ProgramInsufficientValue"].GetSelector().Should().Be(Solgen.ArbWasm.Errors.ProgramInsufficientValue.Selector);
    }

    [TestCase("stylusVersion()")]
    [TestCase("activateProgram(address)")]
    [TestCase("codehashVersion(bytes32)")]
    public void MethodVisibility_BelowStylusArbOSVersion_IsSilentlyRejected(string signature)
    {
        // The entire ArbWasm precompile is gated at ArbosVersion.Stylus; below that, the check at
        // PrecompileHelper.cs:61-62 exits before reading the method ID, so handler stays null too.
        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosVersion(ArbosVersion.Stylus - 1);

        bool result = ArbWasmParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileTestAbiHelpers.GetMethodId(signature), out bool shouldRevert, out PrecompileHandler? handler);

        result.Should().BeFalse();
        shouldRevert.Should().BeFalse();
        handler.Should().BeNull();
    }

    [TestCase("stylusVersion()")]
    [TestCase("activateProgram(address)")]
    [TestCase("codehashVersion(bytes32)")]
    public void MethodVisibility_AtStylusArbOSVersion_IsDispatched(string signature)
    {
        PrecompileTestContextBuilder context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosVersion(ArbosVersion.Stylus)
            .WithExecutingAccount(ArbWasmParser.Address);

        bool result = ArbWasmParser.Instance.TryCheckMethodVisibility(context, NullLogger.Instance,
            PrecompileTestAbiHelpers.GetMethodId(signature), out bool _, out PrecompileHandler? handler);

        result.Should().BeTrue();
        handler.Should().NotBeNull();
    }
}
