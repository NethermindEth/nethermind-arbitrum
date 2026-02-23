// SPDX-License-Identifier: BUSL-1.1
// SPDX-FileCopyrightText: https://github.com/NethermindEth/nethermind-arbitrum/blob/main/LICENSE.md

using FluentAssertions;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Exceptions;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles;

[TestFixture]
public class ArbAggregatorTests
{
    private IWorldState _worldState = null!;
    private IDisposable? _worldStateScope;
    private ArbosState _arbosState = null!;
    private ArbitrumPrecompileExecutionContext _context = null!;
    private Address _chainOwner = null!;
    private Address _nonOwner = null!;
    private Address _batchPoster = null!;
    private Address _feeCollector = null!;

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = _worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(_worldState);

        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(), LimboLogs.Instance.GetClassLogger<ArbosState>());

        _chainOwner = TestItem.AddressA;
        _nonOwner = TestItem.AddressB;
        _batchPoster = TestItem.AddressC;
        _feeCollector = TestItem.AddressD;

        // Add a chain owner
        _arbosState.ChainOwners.Add(_chainOwner);

        _context = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(_chainOwner);
    }

    [TearDown]
    public void TearDown()
    {
        _worldStateScope?.Dispose();
    }

    [Test]
    public void GetPreferredAggregator_Always_ReturnsBatchPosterAddressAsDefault()
    {
        Address testAddress = TestItem.AddressE;

        (Address prefAgg, bool isDefault) = ArbAggregator.GetPreferredAggregator(_context, testAddress);

        prefAgg.Should().Be(ArbosAddresses.BatchPosterAddress);
        isDefault.Should().BeTrue();
    }

    [Test]
    public void GetDefaultAggregator_Always_ReturnsBatchPosterAddress()
    {
        Address result = ArbAggregator.GetDefaultAggregator(_context);

        result.Should().Be(ArbosAddresses.BatchPosterAddress);
    }

    [Test]
    public void GetBatchPosters_WithDefaultPoster_ReturnsDefaultBatchPoster()
    {
        Address[] result = ArbAggregator.GetBatchPosters(_context);

        result.Should().Contain(ArbosAddresses.BatchPosterAddress);
        result.Length.Should().BeGreaterOrEqualTo(1);
    }

    [Test]
    public void AddBatchPoster_WhenCallerIsOwner_AddsNewBatchPoster()
    {
        Address newBatchPoster = TestItem.AddressF;

        ArbAggregator.AddBatchPoster(_context, newBatchPoster);

        Address[] batchPosters = ArbAggregator.GetBatchPosters(_context);
        batchPosters.Should().Contain(newBatchPoster);
    }

    [Test]
    public void AddBatchPoster_WhenCallerIsNotOwner_ThrowsException()
    {
        Address newBatchPoster = TestItem.AddressF;
        ArbitrumPrecompileExecutionContext nonOwnerContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(_nonOwner);

        Action action = () => ArbAggregator.AddBatchPoster(nonOwnerContext, newBatchPoster);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("must be called by chain owner");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void AddBatchPoster_WhenBatchPosterAlreadyExists_DoesNotThrow()
    {
        Address newBatchPoster = new("0x1111111111111111111111111111111111111111");

        // First, add should succeed
        Action firstAdd = () => ArbAggregator.AddBatchPoster(_context, newBatchPoster);
        firstAdd.Should().NotThrow();

        // Second add of same poster should also not throw (Go behavior)
        Action secondAdd = () => ArbAggregator.AddBatchPoster(_context, newBatchPoster);
        secondAdd.Should().NotThrow();
    }

    [Test]
    public void GetFeeCollector_WithExistingBatchPoster_ReturnsCorrectFeeCollector()
    {
        // Default batch poster should have itself as a fee collector
        Address result = ArbAggregator.GetFeeCollector(_context, ArbosAddresses.BatchPosterAddress);

        result.Should().Be(ArbosAddresses.BatchPosterPayToAddress);
    }

    [Test]
    public void GetFeeCollector_WithNonExistentBatchPoster_ThrowsException()
    {
        Address nonExistentPoster = new("0x1234567890123456789012345678901234567890");

        Action action = () => ArbAggregator.GetFeeCollector(_context, nonExistentPoster);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void SetFeeCollector_WhenCallerIsBatchPoster_UpdatesFeeCollector()
    {
        // First, add a new batch poster
        ArbAggregator.AddBatchPoster(_context, _batchPoster);

        ArbitrumPrecompileExecutionContext batchPosterContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(_batchPoster);
        Address newFeeCollector = new("0x1111111111111111111111111111111111111111");

        ArbAggregator.SetFeeCollector(batchPosterContext, _batchPoster, newFeeCollector);

        Address result = ArbAggregator.GetFeeCollector(_context, _batchPoster);
        result.Should().Be(newFeeCollector);
    }

    [Test]
    public void SetFeeCollector_WhenCallerIsFeeCollector_UpdatesFeeCollector()
    {
        // First, add a new batch poster and set an initial fee collector
        ArbAggregator.AddBatchPoster(_context, _batchPoster);
        ArbitrumPrecompileExecutionContext batchPosterContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(_batchPoster);
        ArbAggregator.SetFeeCollector(batchPosterContext, _batchPoster, _feeCollector);

        // Now update from fee collector
        ArbitrumPrecompileExecutionContext feeCollectorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(_feeCollector);
        Address newFeeCollector = new("0x2222222222222222222222222222222222222222");

        ArbAggregator.SetFeeCollector(feeCollectorContext, _batchPoster, newFeeCollector);

        Address result = ArbAggregator.GetFeeCollector(_context, _batchPoster);
        result.Should().Be(newFeeCollector);
    }

    [Test]
    public void SetFeeCollector_WhenCallerIsOwner_UpdatesFeeCollector()
    {
        // First, add a new batch poster
        ArbAggregator.AddBatchPoster(_context, _batchPoster);

        Address newFeeCollector = new("0x3333333333333333333333333333333333333333");

        ArbAggregator.SetFeeCollector(_context, _batchPoster, newFeeCollector);

        Address result = ArbAggregator.GetFeeCollector(_context, _batchPoster);
        result.Should().Be(newFeeCollector);
    }

    [Test]
    public void SetFeeCollector_WhenCallerLacksPermission_ThrowsException()
    {
        // First, add a new batch poster
        ArbAggregator.AddBatchPoster(_context, _batchPoster);

        ArbitrumPrecompileExecutionContext unauthorizedContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(_nonOwner);
        Address newFeeCollector = new("0x4444444444444444444444444444444444444444");

        Action action = () => ArbAggregator.SetFeeCollector(unauthorizedContext, _batchPoster, newFeeCollector);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("only a batch poster (or its fee collector / chain owner) may change its fee collector");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void SetFeeCollector_WithNonExistentBatchPoster_ThrowsException()
    {
        Address nonExistentPoster = new("0x5555555555555555555555555555555555555555");
        Address newFeeCollector = new("0x6666666666666666666666666666666666666666");

        Action action = () => ArbAggregator.SetFeeCollector(_context, nonExistentPoster, newFeeCollector);

        action.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void GetTxBaseFee_Always_ReturnsZero()
    {
        Address aggregator = new("0x7777777777777777777777777777777777777777");

        UInt256 result = ArbAggregator.GetTxBaseFee(_context, aggregator);

        result.Should().Be(0);
    }

    [Test]
    public void SetTxBaseFee_Always_DoesNotThrow()
    {
        Address aggregator = new("0x8888888888888888888888888888888888888888");
        UInt256 feeInL1Gas = new(1000);

        Action action = () => ArbAggregator.SetTxBaseFee(_context, aggregator, feeInL1Gas);

        action.Should().NotThrow();
    }

    [Test]
    public void FeeCollector_CompleteWorkflow_WorksCorrectly()
    {
        // This test mirrors the Go TestFeeCollector test case
        Address batchPosterAddr = ArbosAddresses.BatchPosterAddress;
        Address collectorAddr = new("0x1111111111111111111111111111111111111111");
        Address impostorAddr = new("0x2222222222222222222222222222222222222222");

        // Initial fee collector should be the batch poster address itself
        Address initialCollector = ArbAggregator.GetFeeCollector(_context, batchPosterAddr);
        initialCollector.Should().Be(ArbosAddresses.BatchPosterPayToAddress);

        // Set fee collector to collectorAddr (as batch poster)
        ArbitrumPrecompileExecutionContext batchPosterContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(batchPosterAddr);

        ArbAggregator.SetFeeCollector(batchPosterContext, batchPosterAddr, collectorAddr);

        // Fee collector should now be collectorAddr
        Address newCollector = ArbAggregator.GetFeeCollector(_context, batchPosterAddr);
        newCollector.Should().Be(collectorAddr);

        // Trying to set someone else's collector should fail
        ArbitrumPrecompileExecutionContext impostorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(impostorAddr);

        Action unauthorizedAction = () => ArbAggregator.SetFeeCollector(impostorContext, batchPosterAddr, impostorAddr);
        ArbitrumPrecompileException exception = unauthorizedAction.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("only a batch poster (or its fee collector / chain owner) may change its fee collector");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());

        // But the fee collector can replace itself
        ArbitrumPrecompileExecutionContext collectorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(collectorAddr);

        Action collectorAction = () => ArbAggregator.SetFeeCollector(collectorContext, batchPosterAddr, impostorAddr);
        collectorAction.Should().NotThrow();

        // Verify the fee collector was updated
        Address finalCollector = ArbAggregator.GetFeeCollector(_context, batchPosterAddr);
        finalCollector.Should().Be(impostorAddr);
    }

    [Test]
    public void TxBaseFee_DeprecatedBehavior_AlwaysReturnsZeroAndIgnoresSets()
    {
        // This test mirrors the Go TestTxBaseFee test case
        Address aggregatorAddr = new("0x3333333333333333333333333333333333333333");
        UInt256 targetFee = new(973);

        ArbitrumPrecompileExecutionContext aggregatorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(aggregatorAddr);

        // Initial result should be zero
        UInt256 initialFee = ArbAggregator.GetTxBaseFee(_context, aggregatorAddr);
        initialFee.Should().Be(UInt256.Zero);

        // Set base fee to value -- should be ignored (no-op)
        Action setAction = () => ArbAggregator.SetTxBaseFee(aggregatorContext, aggregatorAddr, targetFee);
        setAction.Should().NotThrow();

        // Base fee should still be zero
        UInt256 finalFee = ArbAggregator.GetTxBaseFee(_context, aggregatorAddr);
        finalFee.Should().Be(UInt256.Zero);
    }
}
