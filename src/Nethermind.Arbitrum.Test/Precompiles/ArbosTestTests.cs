// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public sealed class ArbosTestTests
{
    private const ulong DefaultGasSupplied = 100000;

    private IWorldState _worldState = null!;
    private ArbosState _arbosState = null!;
    private BlockHeader _genesisBlockHeader = null!;
    private PrecompileTestContextBuilder _context = null!;

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = _worldState.BeginScope(IWorldState.PreGenesis);
        Block b = ArbOSInitialization.Create(_worldState);
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied) { ArbosState = _arbosState };
        _genesisBlockHeader = b.Header;
    }

    [Test]
    public void BurnArbGas_WithValidAmount_BurnsGas()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = 1000;
        ulong initialGas = _context.GasLeft;

        ArbosTest.BurnArbGas(_context, gasAmount);

        ulong gasUsed = initialGas - _context.GasLeft;
        gasUsed.Should().Be((ulong)gasAmount);
    }

    [Test]
    public void BurnArbGas_WithZeroAmount_BurnsZeroGas()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = UInt256.Zero;
        ulong initialGas = _context.GasLeft;

        ArbosTest.BurnArbGas(_context, gasAmount);

        ulong gasUsed = initialGas - _context.GasLeft;
        gasUsed.Should().Be(0);
    }

    [Test]
    public void BurnArbGas_WithMaxUInt64Amount_BurnsGas()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = ulong.MaxValue;

        Action action = () => ArbosTest.BurnArbGas(_context, gasAmount);

        action.Should().Throw<ArbitrumPrecompileException>()
            .Where(e => e.OutOfGas);
    }

    [Test]
    public void BurnArbGas_WithAmountExceedingUInt64_ThrowsNotAUInt64Exception()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = (UInt256)ulong.MaxValue + 1;

        Action action = () => ArbosTest.BurnArbGas(_context, gasAmount);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Message.Should().Contain("not a uint64");
    }

    [Test]
    public void Address_Always_ReturnsArbosTestAddress()
    {
        ArbosTest.Address.Should().Be(ArbosAddresses.ArbosTestAddress);
    }

    [Test]
    public void Abi_Always_ContainsRequiredMethods()
    {
        ArbosTest.Abi.Should().NotBeNullOrEmpty();
        ArbosTest.Abi.Should().Contain("burnArbGas");
    }
}
