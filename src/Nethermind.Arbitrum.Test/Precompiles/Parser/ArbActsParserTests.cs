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
using Nethermind.Arbitrum.State;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public sealed class ArbActsParserTests
{
    private const ulong DefaultGasSupplied = 100000;

    private static readonly uint _startBlockId = PrecompileHelper.GetMethodId("startBlock(uint256,uint64,uint64,uint64)");
    private static readonly uint _batchPostingReportId = PrecompileHelper.GetMethodId("batchPostingReport(uint256,address,uint64,uint64,uint256)");
    private static readonly uint _batchPostingReportV2Id = PrecompileHelper.GetMethodId("batchPostingReportV2(uint256,address,uint64,uint64,uint64,uint64,uint256)");

    private IArbitrumWorldState _worldState = null!;
    private ArbosState _arbosState = null!;
    private BlockHeader _genesisBlockHeader = null!;
    private PrecompileTestContextBuilder _context = null!;

    [SetUp]
    public void SetUp()
    {
        _worldState = TestArbitrumWorldState.CreateNewInMemory();
        using var worldStateDisposer = _worldState.BeginScope(IWorldState.PreGenesis);
        Block block = ArbOSInitialization.Create(_worldState);
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied)
            .WithArbosState();
        _genesisBlockHeader = block.Header;
    }

    [Test]
    public void ParsesStartBlock_WithValidInputData_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbActsParser.PrecompileFunctionDescription[_startBlockId].AbiFunctionDescription.GetCallInfo().Signature,
            new UInt256(1000),
            100UL,
            200UL,
            12UL
        );

        bool exists = ArbActsParser.PrecompileImplementation.TryGetValue(_startBlockId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbActsTests.AssertCallerNotArbOSException(action);
    }

    [Test]
    public void ParsesStartBlock_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbActsParser.PrecompileImplementation.TryGetValue(_startBlockId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] malformedCalldata = new byte[10];

        Action action = () => handler!(_context, malformedCalldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesBatchPostingReport_WithValidInputData_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address batchPoster = new("0x0000000000000000000000000000000000000456");

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbActsParser.PrecompileFunctionDescription[_batchPostingReportId].AbiFunctionDescription.GetCallInfo().Signature,
            new UInt256(1234567890),
            batchPoster,
            1UL,
            50000UL,
            new UInt256(2000)
        );

        bool exists = ArbActsParser.PrecompileImplementation.TryGetValue(_batchPostingReportId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbActsTests.AssertCallerNotArbOSException(action);
    }

    [Test]
    public void ParsesBatchPostingReport_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbActsParser.PrecompileImplementation.TryGetValue(_batchPostingReportId, out PrecompileHandler? handler);
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

        bool exists = ArbActsParser.PrecompileImplementation.TryGetValue(invalidMethodId, out PrecompileHandler? handler);

        exists.Should().BeFalse();
        handler.Should().BeNull();
    }

    [Test]
    public void ParsesBatchPostingReportV2_WithValidInputData_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address batchPoster = new("0x0000000000000000000000000000000000000456");

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbActsParser.PrecompileFunctionDescription[_batchPostingReportV2Id].AbiFunctionDescription.GetCallInfo().Signature,
            new UInt256(1234567890),
            batchPoster,
            1UL,
            1000UL,
            800UL,
            5000UL,
            new UInt256(2000)
        );

        bool exists = ArbActsParser.PrecompileImplementation.TryGetValue(_batchPostingReportV2Id, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbActsTests.AssertCallerNotArbOSException(action);
    }

    [Test]
    public void ParsesBatchPostingReportV2_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbActsParser.PrecompileImplementation.TryGetValue(_batchPostingReportV2Id, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] malformedCalldata = new byte[10];

        Action action = () => handler!(_context, malformedCalldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void Address_Always_ReturnsArbosAddress()
    {
        ArbActsParser.Address.Should().Be(ArbosAddresses.ArbosAddress);
    }
}
