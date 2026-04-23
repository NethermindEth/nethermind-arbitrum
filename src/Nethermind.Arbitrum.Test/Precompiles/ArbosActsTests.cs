// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Abi;
using Nethermind.Arbitrum.Test.Precompiles.Abi;
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
public sealed class ArbosActsTests
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
    public void StartBlock_WhenCalledByNonArbOS_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        Action action = () => ArbosActs.StartBlock(_context, 1000, 100UL, 200UL, 12UL);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void StartBlock_WithZeroParameters_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        Action action = () => ArbosActs.StartBlock(_context, 0, 0UL, 0UL, 0UL);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void StartBlock_WithMaxParameters_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        Action action = () => ArbosActs.StartBlock(_context, UInt256.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void BatchPostingReport_WhenCalledByNonArbOS_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address batchPoster = new("0x0000000000000000000000000000000000000456");

        Action action = () => ArbosActs.BatchPostingReport(_context, 1234567890, batchPoster, 1UL, 50000UL, 2000);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void BatchPostingReport_WithZeroParametersAndEmptyAddress_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);

        Action action = () => ArbosActs.BatchPostingReport(_context, 0, Address.Zero, 0, 0, 0);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void BatchPostingReportV2_WhenCalledByNonArbOS_ThrowsCallerNotArbOSException()
    {
        using IDisposable worldStateDisposer = _worldState.BeginScope(_genesisBlockHeader);
        Address batchPoster = new("0x0000000000000000000000000000000000000456");

        Action action = () => ArbosActs.BatchPostingReportV2(_context, 1234567890, batchPoster, 1UL, 1000UL, 800UL, 5000UL, 2000);

        AssertCallerNotArbOSException(action);
    }

    [Test]
    public void Address_Always_ReturnsArbosAddress()
    {
        ArbosActs.Address.Should().Be(ArbosAddresses.ArbosAddress);
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

    [Test]
    public void Abi_WhenParsed_ContainsExpectedFunctionSignatures()
    {
        Dictionary<uint, ArbitrumFunctionDescription> allFunctions = PrecompileTestAbiHelpers.GetAllFunctionDescriptions(Solgen.ArbosActs.Abi);

        allFunctions.Keys.Should().BeEquivalentTo(new[]
        {
            PrecompileHelper.GetMethodId("startBlock(uint256,uint64,uint64,uint64)"),
            PrecompileHelper.GetMethodId("batchPostingReport(uint256,address,uint64,uint64,uint256)"),
            PrecompileHelper.GetMethodId("batchPostingReportV2(uint256,address,uint64,uint64,uint64,uint64,uint256)"),
        });
    }

    [Test]
    public void Abi_WhenParsed_ContainsExpectedErrors()
    {
        Dictionary<string, AbiErrorDescription> allErrors = PrecompileTestAbiHelpers.GetAllErrorDescriptions(Solgen.ArbosActs.Abi);

        allErrors.Keys.Should().BeEquivalentTo("CallerNotArbOS");
    }

    [Test]
    public void Abi_WhenParsed_ContainsNoEvents()
    {
        PrecompileTestAbiHelpers.GetAllEventDescriptions(Solgen.ArbosActs.Abi).Should().BeEmpty();
    }

    [Test]
    public void MethodIds_AllFunctions_MatchExpectedSelectors()
    {
        PrecompileHelper.GetMethodId("startBlock(uint256,uint64,uint64,uint64)").Should().Be(0x6bf6a42du);
        PrecompileHelper.GetMethodId("batchPostingReport(uint256,address,uint64,uint64,uint256)").Should().Be(0xb6693771u);
        PrecompileHelper.GetMethodId("batchPostingReportV2(uint256,address,uint64,uint64,uint64,uint64,uint256)").Should().Be(0x9998269eu);
    }

    [Test]
    public void ErrorSelectors_AllErrors_MatchExpectedValues()
    {
        // keccak256("CallerNotArbOS()")[0..4]
        ArbosActs.CallerNotArbOS.GetSelector().Should().Be(0xf812e656u);
    }
}
