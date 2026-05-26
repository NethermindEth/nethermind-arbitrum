// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

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
public sealed class ArbosActsParserTests
{
    private const ulong DefaultGasSupplied = 100000;

    private static readonly uint StartBlockId = PrecompileTestAbiHelpers.GetMethodId("startBlock(uint256,uint64,uint64,uint64)");
    private static readonly uint BatchPostingReportId = PrecompileTestAbiHelpers.GetMethodId("batchPostingReport(uint256,address,uint64,uint64,uint256)");
    private static readonly uint BatchPostingReportV2Id = PrecompileTestAbiHelpers.GetMethodId("batchPostingReportV2(uint256,address,uint64,uint64,uint64,uint64,uint256)");

    private IWorldState _worldState = null!;
    private ArbosState _arbosState = null!;
    private BlockHeader _genesisBlockHeader = null!;
    private PrecompileTestContextBuilder _context = null!;

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        using IDisposable worldStateDisposer = _worldState.BeginScope(IWorldState.PreGenesis);
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
            ArbosActsParser.PrecompileFunctionDescription[StartBlockId].AbiFunctionDescription.GetCallInfo().Signature,
            new UInt256(1000),
            100UL,
            200UL,
            12UL
        );

        bool exists = ArbosActsParser.PrecompileImplementation.TryGetValue(StartBlockId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbosActsTests.AssertCallerNotArbOSException(action);
    }

    [Test]
    public void ParsesStartBlock_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbosActsParser.PrecompileImplementation.TryGetValue(StartBlockId, out PrecompileHandler? handler);
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
            ArbosActsParser.PrecompileFunctionDescription[BatchPostingReportId].AbiFunctionDescription.GetCallInfo().Signature,
            new UInt256(1234567890),
            batchPoster,
            1UL,
            50000UL,
            new UInt256(2000)
        );

        bool exists = ArbosActsParser.PrecompileImplementation.TryGetValue(BatchPostingReportId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbosActsTests.AssertCallerNotArbOSException(action);
    }

    [Test]
    public void ParsesBatchPostingReport_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbosActsParser.PrecompileImplementation.TryGetValue(BatchPostingReportId, out PrecompileHandler? handler);
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

        bool exists = ArbosActsParser.PrecompileImplementation.TryGetValue(invalidMethodId, out PrecompileHandler? handler);

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
            ArbosActsParser.PrecompileFunctionDescription[BatchPostingReportV2Id].AbiFunctionDescription.GetCallInfo().Signature,
            new UInt256(1234567890),
            batchPoster,
            1UL,
            1000UL,
            800UL,
            5000UL,
            new UInt256(2000)
        );

        bool exists = ArbosActsParser.PrecompileImplementation.TryGetValue(BatchPostingReportV2Id, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbosActsTests.AssertCallerNotArbOSException(action);
    }

    [Test]
    public void ParsesBatchPostingReportV2_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbosActsParser.PrecompileImplementation.TryGetValue(BatchPostingReportV2Id, out PrecompileHandler? handler);
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
        ArbosActsParser.Address.Should().Be(ArbosAddresses.ArbosAddress);
    }
}
