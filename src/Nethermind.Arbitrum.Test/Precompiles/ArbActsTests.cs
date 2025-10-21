using FluentAssertions;
using Nethermind.Abi;
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
public sealed class ArbActsTests
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
    public void StartBlock_WhenCalledByNonArbOS_ThrowsCallerNotArbOSException()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        Action action = () => ArbActs.StartBlock(_context, 1000, 100UL, 200UL, 12UL);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void StartBlock_WithZeroParameters_ThrowsCallerNotArbOSException()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        Action action = () => ArbActs.StartBlock(_context, 0, 0UL, 0UL, 0UL);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void StartBlock_WithMaxParameters_ThrowsCallerNotArbOSException()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        Action action = () => ArbActs.StartBlock(_context, UInt256.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void BatchPostingReport_WhenCalledByNonArbOS_ThrowsCallerNotArbOSException()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address batchPoster = new("0x0000000000000000000000000000000000000456");

        Action action = () => ArbActs.BatchPostingReport(_context, 1234567890, batchPoster, 1UL, 50000UL, 2000);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void BatchPostingReport_WithZeroParametersAndEmptyAddress_ThrowsCallerNotArbOSException()
    {
        using var worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        Action action = () => ArbActs.BatchPostingReport(_context, 0, Address.Zero, 0, 0, 0);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void Address_Always_ReturnsArbosAddress()
    {
        ArbActs.Address.Should().Be(ArbosAddresses.ArbosAddress);
    }

    [Test]
    public void Abi_Always_ContainsRequiredMethodsAndError()
    {
        ArbActs.Abi.Should().NotBeNullOrEmpty();
        ArbActs.Abi.Should().Contain("startBlock");
        ArbActs.Abi.Should().Contain("batchPostingReport");
        ArbActs.Abi.Should().Contain("CallerNotArbOS");
    }

    public static void AssertCallerNotArbOSException(Action action)
    {
        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        exception.Type.Should().Be(ArbitrumPrecompileException.PrecompileExceptionType.SolidityError);

        exception.OutOfGas.Should().BeFalse();
        exception.IsRevertDuringCalldataDecoding.Should().BeFalse();

        // Calculate expected error data
        byte[] expectedErrorData = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.IncludeSignature,
            new AbiSignature("CallerNotArbOS")
        );

        exception.Output.Should().Equal(expectedErrorData);
    }
}
