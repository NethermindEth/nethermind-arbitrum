// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;
using Solgen = Nethermind.Arbitrum.Precompiles.Solgen;

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
        using IDisposable worldStateDisposer = _worldState.BeginScope(IWorldState.PreGenesis);
        Block b = ArbOSInitialization.Create(_worldState);
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied) { ArbosState = _arbosState };
        _genesisBlockHeader = b.Header;
    }

    [Test]
    public void BurnArbGas_WithValidAmount_BurnsGas()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = 1000;
        ulong initialGas = _context.GasLeft;

        ArbosTest.BurnArbGas(_context, gasAmount);

        ulong gasUsed = initialGas - _context.GasLeft;
        gasUsed.Should().Be((ulong)gasAmount);
    }

    [Test]
    public void BurnArbGas_WithZeroAmount_BurnsZeroGas()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = UInt256.Zero;
        ulong initialGas = _context.GasLeft;

        ArbosTest.BurnArbGas(_context, gasAmount);

        ulong gasUsed = initialGas - _context.GasLeft;
        gasUsed.Should().Be(0);
    }

    [Test]
    public void BurnArbGas_WithMaxUInt64Amount_BurnsGas()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = ulong.MaxValue;

        // BurnAllowingOutOfGas intentionally does not throw — it silently consumes all remaining gas
        ArbTest.BurnArbGas(_context, gasAmount);

        _context.GasLeft.Should().Be(0);
    }

    [Test]
    public void BurnArbGas_WithAmountExceedingUInt64_ThrowsNotAUInt64Exception()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = (UInt256)ulong.MaxValue + 1;

        Action action = () => ArbosTest.BurnArbGas(_context, gasAmount);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("not a uint64");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void Address_Always_ReturnsArbosTestAddress()
    {
        ArbosTest.Address.Should().Be(ArbosAddresses.ArbosTestAddress);
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbosTest.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileTestAbiHelpers.GetMethodId("burnArbGas(uint256)"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbosTest.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbosTest.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileTestAbiHelpers.GetMethodId("burnArbGas(uint256)").Should().Be(Solgen.ArbosTest.Methods.BurnArbGas);
    }
}
