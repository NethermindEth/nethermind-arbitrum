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

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public sealed class ArbFunctionTableTests
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
    public void Upload_WithAnyData_DoesNothing()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] buffer = new byte[] { 0, 0, 0, 0 };

        Action action = () => ArbFunctionTable.Upload(_context, buffer);

        action.Should().NotThrow();
    }

    [Test]
    public void Size_WithAnyAddress_ReturnsZero()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address addr = new("0x0000000000000000000000000000000000000123");

        UInt256 size = ArbFunctionTable.Size(_context, addr);

        size.Should().Be(UInt256.Zero);
    }

    [Test]
    public void Get_WithAnyAddressAndIndex_ThrowsTableIsEmptyException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address addr = new("0x0000000000000000000000000000000000000123");
        UInt256 index = 10;

        Action action = () => ArbFunctionTable.Get(_context, addr, index);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("table is empty");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void Address_Always_ReturnsArbFunctionTableAddress()
    {
        ArbFunctionTable.Address.Should().Be(ArbosAddresses.ArbFunctionTableAddress);
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = AbiMetadata.GetAllFunctionDescriptions(ArbFunctionTable.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileHelper.GetMethodId("upload(bytes)"),
            PrecompileHelper.GetMethodId("size(address)"),
            PrecompileHelper.GetMethodId("get(address,uint256)"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        AbiMetadata.GetAllEventDescriptions(ArbFunctionTable.Abi).Should().BeEmpty();
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoErrors()
    {
        AbiMetadata.GetAllErrorDescriptions(ArbFunctionTable.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("upload(bytes)").Should().Be(0xce2ae159u);
        PrecompileHelper.GetMethodId("size(address)").Should().Be(0x88987068u);
        PrecompileHelper.GetMethodId("get(address,uint256)").Should().Be(0xb464631bu);
    }
}
