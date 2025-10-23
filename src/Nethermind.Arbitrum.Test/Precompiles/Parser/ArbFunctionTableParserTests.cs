// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public sealed class ArbFunctionTableParserTests
{
    private const ulong DefaultGasSupplied = 100000;

    private static readonly uint _uploadId = PrecompileHelper.GetMethodId("upload(bytes)");
    private static readonly uint _sizeId = PrecompileHelper.GetMethodId("size(address)");
    private static readonly uint _getId = PrecompileHelper.GetMethodId("get(address,uint256)");

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
    public void ParsesUpload_WithValidInputData_Succeeds()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] buffer = new byte[] { 0, 0, 0, 0 };

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbFunctionTableParser.PrecompileFunctionDescription[_uploadId].AbiFunctionDescription.GetCallInfo().Signature,
            buffer
        );

        bool exists = ArbFunctionTableParser.PrecompileImplementation.TryGetValue(_uploadId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().BeEmpty();
    }

    [Test]
    public void ParsesUpload_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbFunctionTableParser.PrecompileImplementation.TryGetValue(_uploadId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] malformedCalldata = new byte[10];

        Action action = () => handler!(_context, malformedCalldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesSize_WithValidInputData_ReturnsZero()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address addr = new("0x0000000000000000000000000000000000000123");

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbFunctionTableParser.PrecompileFunctionDescription[_sizeId].AbiFunctionDescription.GetCallInfo().Signature,
            addr
        );

        bool exists = ArbFunctionTableParser.PrecompileImplementation.TryGetValue(_sizeId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, calldata);

        result.Should().NotBeNull();
        result.Length.Should().Be(32);
        UInt256 size = new(result, isBigEndian: true);
        size.Should().Be(UInt256.Zero);
    }

    [Test]
    public void ParsesSize_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbFunctionTableParser.PrecompileImplementation.TryGetValue(_sizeId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] malformedCalldata = new byte[10];

        Action action = () => handler!(_context, malformedCalldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateRevertException("", true);
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesGet_WithValidInputData_ThrowsTableIsEmptyException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address addr = new("0x0000000000000000000000000000000000000123");
        UInt256 index = 10;

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbFunctionTableParser.PrecompileFunctionDescription[_getId].AbiFunctionDescription.GetCallInfo().Signature,
            addr,
            index
        );

        bool exists = ArbFunctionTableParser.PrecompileImplementation.TryGetValue(_getId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        Action action = () => handler!(_context, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("table is empty");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void ParsesGet_WithInvalidInputData_ThrowsRevertException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        bool exists = ArbFunctionTableParser.PrecompileImplementation.TryGetValue(_getId, out PrecompileHandler? handler);
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

        bool exists = ArbFunctionTableParser.PrecompileImplementation.TryGetValue(invalidMethodId, out PrecompileHandler? handler);

        exists.Should().BeFalse();
        handler.Should().BeNull();
    }

    [Test]
    public void Address_Always_ReturnsArbFunctionTableAddress()
    {
        ArbFunctionTableParser.Address.Should().Be(ArbosAddresses.ArbFunctionTableAddress);
    }
}
