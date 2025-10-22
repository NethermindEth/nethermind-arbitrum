// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public sealed class ArbosTestParserTests
{
    private const ulong DefaultGasSupplied = 100000;

    private static readonly uint _burnArbGasId = PrecompileHelper.GetMethodId("burnArbGas(uint256)");

    private IWorldState _worldState = null!;
    private ArbosState _arbosState = null!;
    private BlockHeader _genesisBlockHeader = null!;
    private PrecompileTestContextBuilder _context = null!;

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        using var worldStateDisposer = _worldState.BeginScope(IWorldState.PreGenesis);
        Block block = ArbOSInitialization.Create(_worldState);
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosState();
        _genesisBlockHeader = block.Header;
    }

    [Test]
    public void ParsesBurnArbGas_WithValidInputData_BurnsGas()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = 1000;

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbosTestParser.PrecompileFunctionDescription[_burnArbGasId].AbiFunctionDescription.GetCallInfo().Signature,
            gasAmount
        );

        bool exists = ArbosTestParser.PrecompileImplementation.TryGetValue(_burnArbGasId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        ulong initialGas = _context.GasLeft;
        byte[] result = handler!(_context, calldata);

        result.Should().BeEmpty();
        ulong gasUsed = initialGas - _context.GasLeft;
        gasUsed.Should().Be((ulong)gasAmount);
    }

    [Test]
    public void ParsesBurnArbGas_WithAmountExceedingUInt64_ThrowsNotAUInt64Exception()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        UInt256 gasAmount = (UInt256)ulong.MaxValue + 1;

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbosTestParser.PrecompileFunctionDescription[_burnArbGasId].AbiFunctionDescription.GetCallInfo().Signature,
            gasAmount
        );

        bool exists = ArbosTestParser.PrecompileImplementation.TryGetValue(_burnArbGasId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("not a uint64");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesBurnArbGas_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbosTestParser.PrecompileImplementation.TryGetValue(_burnArbGasId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] malformedCalldata = new byte[10];

        Action action = () => handler!(_context, malformedCalldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void PrecompileImplementation_WithInvalidMethodId_ReturnsNotFound()
    {
        byte[] data = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, 0x12345678);
        uint invalidMethodId = BinaryPrimitives.ReadUInt32BigEndian(data);

        bool exists = ArbosTestParser.PrecompileImplementation.TryGetValue(invalidMethodId, out PrecompileHandler? handler);

        exists.Should().BeFalse();
        handler.Should().BeNull();
    }

    [Test]
    public void Address_Always_ReturnsArbosTestAddress()
    {
        ArbosTestParser.Address.Should().Be(ArbosAddresses.ArbosTestAddress);
    }
}
