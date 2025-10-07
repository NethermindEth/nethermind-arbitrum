// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm;
using Nethermind.Evm.State;
using Nethermind.Logging;
using static Nethermind.Arbitrum.Precompiles.ArbWasm;
using Address = Nethermind.Core.Address;

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

        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec();
    }

    [TearDown]
    public void TearDown()
    {
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
    public void CodeHashVersion_WithNonExistentCodeHash_ReturnsZero()
    {
        Hash256 nonExistentCodeHash = Hash256.Zero;

        ushort version = CodeHashVersion(_context, nonExistentCodeHash);

        version.Should().Be(0);
    }

    [Test]
    public void ProgramVersion_WithNonExistentProgram_ReturnsZero()
    {
        Address nonExistentProgram = Address.Zero;

        ushort version = ProgramVersion(_context, nonExistentProgram);

        version.Should().Be(0);
    }

    [Test]
    public void CodeHashAsmSize_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Action act = () => CodeHashAsmSize(_context, NonActivatedCodeHash);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage(Errors.ProgramNotActivated);
    }

    [Test]
    public void ProgramInitGas_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Action act = () => ProgramInitGas(_context, NonActivatedProgram);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage(Errors.ProgramNotActivated);
    }

    [Test]
    public void ProgramMemoryFootprint_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Action act = () => ProgramMemoryFootprint(_context, NonActivatedProgram);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage(Errors.ProgramNotActivated);
    }

    [Test]
    public void ProgramTimeLeft_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Action act = () => ProgramTimeLeft(_context, NonActivatedProgram);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage(Errors.ProgramNotActivated);
    }

    [Test]
    public void CodeHashKeepalive_WithTooEarlyKeepalive_ThrowsInvalidOperation()
    {
        Hash256 codeHash = Hash256.Zero;

        Action act = () => CodeHashKeepAlive(_context, codeHash);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage(Errors.ProgramNotActivated);
    }

    [Test]
    public void ActivateProgram_WithInsufficientValue_ThrowsOutOfGas()
    {
        Address program = Address.Zero;

        Action act = () => ActivateProgram(_context, program);

        act.Should().Throw<OutOfGasException>();
    }

    [Test]
    public void ActivateProgram_WithAnyCall_BurnsFixedCost()
    {
        PrecompileTestContextBuilder context = new(_worldState, 2_000_000) { ArbosState = _arbosState };
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

        Action act = () => ActivateProgram(_context, nonExistentProgram);

        act.Should().Throw<OutOfGasException>();
    }

    [Test]
    public void ActivateProgram_WithZeroValue_ThrowsOutOfGas()
    {
        Address program = Address.Zero;

        Action act = () => ActivateProgram(_context, program);

        act.Should().Throw<OutOfGasException>();
    }

    [Test]
    public void CodeHashKeepAlive_WithNonActivatedProgram_ThrowsInvalidOperation()
    {
        Hash256 nonActivatedCodeHash = Hash256.Zero;

        Action act = () => CodeHashKeepAlive(_context, nonActivatedCodeHash);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage(Errors.ProgramNotActivated);
    }

    [Test]
    public void CodeHashKeepAlive_WithInsufficientValue_ThrowsInvalidOperation()
    {
        Hash256 codeHash = Hash256.Zero;

        Action act = () => CodeHashKeepAlive(_context, codeHash);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage(Errors.ProgramNotActivated);
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
}
