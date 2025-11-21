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
        using var worldStateDisposer = _worldState.BeginScope(IWorldState.PreGenesis);
        Block b = ArbOSInitialization.Create(_worldState);
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(),
            LimboLogs.Instance.GetClassLogger<ArbosState>());
        _context = new PrecompileTestContextBuilder(_worldState, DefaultGasSupplied) { ArbosState = _arbosState };
        _genesisBlockHeader = b.Header;
    }

    [Test]
    public void Upload_WithAnyData_DoesNothing()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        byte[] buffer = new byte[] { 0, 0, 0, 0 };

        Action action = () => ArbFunctionTable.Upload(_context, buffer);

        action.Should().NotThrow();
    }

    [Test]
    public void Size_WithAnyAddress_ReturnsZero()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address addr = new("0x0000000000000000000000000000000000000123");

        UInt256 size = ArbFunctionTable.Size(_context, addr);

        size.Should().Be(UInt256.Zero);
    }

    [Test]
    public void Get_WithAnyAddressAndIndex_ThrowsTableIsEmptyException()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
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
    public void Abi_Always_ContainsRequiredMethods()
    {
        ArbFunctionTable.Abi.Should().NotBeNullOrEmpty();
        ArbFunctionTable.Abi.Should().Contain("upload");
        ArbFunctionTable.Abi.Should().Contain("size");
        ArbFunctionTable.Abi.Should().Contain("get");
    }
}
