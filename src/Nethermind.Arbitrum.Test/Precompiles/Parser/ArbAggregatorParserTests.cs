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
using Nethermind.Core.Crypto;
using Nethermind.Core.Test;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.State;
using Nethermind.Int256;
using Nethermind.Logging;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public class ArbAggregatorParserTests
{
    private IWorldState _worldState = null!;
    private IDisposable? _worldStateScope;
    private ArbosState _arbosState = null!;
    private ArbitrumPrecompileExecutionContext _context = null!;
    private Address _chainOwner = null!;

    private static readonly uint _getPreferredAggregatorId = PrecompileHelper.GetMethodId("getPreferredAggregator(address)");
    private static readonly uint _getDefaultAggregatorId = PrecompileHelper.GetMethodId("getDefaultAggregator()");
    private static readonly uint _getBatchPostersId = PrecompileHelper.GetMethodId("getBatchPosters()");
    private static readonly uint _addBatchPosterId = PrecompileHelper.GetMethodId("addBatchPoster(address)");
    private static readonly uint _getFeeCollectorId = PrecompileHelper.GetMethodId("getFeeCollector(address)");
    private static readonly uint _setFeeCollectorId = PrecompileHelper.GetMethodId("setFeeCollector(address,address)");
    private static readonly uint _getTxBaseFeeId = PrecompileHelper.GetMethodId("getTxBaseFee(address)");
    private static readonly uint _setTxBaseFeeId = PrecompileHelper.GetMethodId("setTxBaseFee(address,uint256)");

    [SetUp]
    public void SetUp()
    {
        _worldState = TestWorldStateFactory.CreateForTest();
        _worldStateScope = _worldState.BeginScope(IWorldState.PreGenesis);
        _ = ArbOSInitialization.Create(_worldState);

        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(), LimboLogs.Instance.GetClassLogger<ArbosState>());

        _chainOwner = TestItem.AddressA;
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
    public void GetPreferredAggregator_WithValidInput_ReturnsCorrectResult()
    {
        Address testAddress = TestItem.AddressB;
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getPreferredAggregatorId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_getPreferredAggregatorId].AbiFunctionDescription.GetCallInfo().Signature,
            testAddress
        );

        byte[] result = handler!(_context, calldata);

        // Decode the result
        object[] decodedResult = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            new AbiSignature("getPreferredAggregator", AbiType.Address, AbiType.Bool),
            result
        );

        decodedResult[0].Should().Be(ArbosAddresses.BatchPosterAddress);
        decodedResult[1].Should().Be(true);
    }

    [Test]
    public void GetDefaultAggregator_WithValidInput_ReturnsCorrectResult()
    {
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getDefaultAggregatorId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, []);

        Address decodedResult = new(result[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(ArbosAddresses.BatchPosterAddress);
    }

    [Test]
    public void GetBatchPosters_WithValidInput_ReturnsCorrectResult()
    {
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getBatchPostersId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, []);

        // Decode the result
        object[] decodedResult = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            new AbiSignature("getBatchPosters", new AbiArray(AbiType.Address)),
            result
        );

        Address[] batchPosters = (Address[])decodedResult[0];
        batchPosters.Should().Contain(ArbosAddresses.BatchPosterAddress);
    }

    [Test]
    public void AddBatchPoster_WithValidInput_AddsNewBatchPoster()
    {
        Address newBatchPoster = TestItem.AddressC;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_addBatchPosterId].AbiFunctionDescription.GetCallInfo().Signature,
            newBatchPoster
        );

        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_addBatchPosterId, out PrecompileHandler? addBatchPosterHandler);
        exists.Should().BeTrue();

        byte[] result = addBatchPosterHandler!(_context, calldata);

        result.Should().BeEmpty(); // No return value

        // Verify the batch poster was added
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getBatchPostersId, out PrecompileHandler? getBatchPostersHandler);
        exists.Should().BeTrue();

        byte[] getBatchPostersResult = getBatchPostersHandler!(_context, []);

        object[] decodedResult = AbiEncoder.Instance.Decode(
            AbiEncodingStyle.None,
            new AbiSignature("getBatchPosters", new AbiArray(AbiType.Address)),
            getBatchPostersResult
        );

        Address[] batchPosters = (Address[])decodedResult[0];
        batchPosters.Should().Contain(newBatchPoster);
    }

    [Test]
    public void AddBatchPoster_WhenCallerIsNotOwner_ThrowsException()
    {
        Address newBatchPoster = TestItem.AddressD;
        ArbitrumPrecompileExecutionContext nonOwnerContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(TestItem.AddressE);

        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_addBatchPosterId, out PrecompileHandler? addBatchPosterHandler);
        exists.Should().BeTrue();

        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_addBatchPosterId].AbiFunctionDescription.GetCallInfo().Signature,
            newBatchPoster
        );
        Action action = () => addBatchPosterHandler!(nonOwnerContext, calldata);

        ArbitrumPrecompileException exception = action.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("must be called by chain owner");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());
    }

    [Test]
    public void GetFeeCollector_WithValidInput_ReturnsCorrectResult()
    {
        Address batchPoster = ArbosAddresses.BatchPosterAddress;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_getFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            batchPoster
        );

        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getFeeCollectorId, out PrecompileHandler? getFeeCollectorHandler);
        exists.Should().BeTrue();

        byte[] result = getFeeCollectorHandler!(_context, calldata);

        Address decodedResult = new(result[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(ArbosAddresses.BatchPosterPayToAddress);
    }

    [Test]
    public void SetFeeCollector_WithValidInput_UpdatesFeeCollector()
    {
        // First, add a new batch poster
        Address newBatchPoster = TestItem.AddressF;
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_addBatchPosterId].AbiFunctionDescription.GetCallInfo().Signature,
            newBatchPoster
        );

        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_addBatchPosterId, out PrecompileHandler? addBatchPosterHandler);
        exists.Should().BeTrue();

        byte[] result = addBatchPosterHandler!(_context, calldata);

        // Set fee collector
        Address newFeeCollector = new("0x1111111111111111111111111111111111111111");
        byte[] setInput = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_setFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            newBatchPoster, newFeeCollector
        );

        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_setFeeCollectorId, out PrecompileHandler? setFeeCollectorHandler);
        exists.Should().BeTrue();

        result = setFeeCollectorHandler!(_context, setInput);

        result.Should().BeEmpty(); // No return value

        // Verify the fee collector was updated
        byte[] getInput = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_getFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            newBatchPoster
        );
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getFeeCollectorId, out PrecompileHandler? getFeeCollectorHandler);
        exists.Should().BeTrue();

        byte[] getResult = getFeeCollectorHandler!(_context, getInput);

        Address decodedResult = new(getResult[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(newFeeCollector);
    }

    [Test]
    public void GetTxBaseFee_WithValidInput_ReturnsZero()
    {
        Address aggregator = new("0x2222222222222222222222222222222222222222");
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_getTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            aggregator
        );

        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getTxBaseFeeId, out PrecompileHandler? getTxBaseFeeHandler);
        exists.Should().BeTrue();

        byte[] result = getTxBaseFeeHandler!(_context, calldata);

        UInt256 decodedResult = new(result, isBigEndian: true);
        decodedResult.Should().Be(UInt256.Zero);
    }

    [Test]
    public void SetTxBaseFee_WithValidInput_DoesNotThrow()
    {
        Address aggregator = new("0x3333333333333333333333333333333333333333");
        UInt256 feeInL1Gas = new(1000);
        byte[] calldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_setTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            aggregator, feeInL1Gas
        );

        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_setTxBaseFeeId, out PrecompileHandler? setTxBaseFeeHandler);
        exists.Should().BeTrue();

        byte[] result = setTxBaseFeeHandler!(_context, calldata);

        result.Should().BeEmpty(); // No return value
    }

    [Test]
    public void TryExecuteAMethod_WithInvalidMethodId_ThrowsException()
    {
        byte[] invalidInput = [0xFF, 0xFF, 0xFF, 0xFF]; // Invalid method ID

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(invalidInput);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? invalidMethodHandler);
        exists.Should().BeFalse();
    }

    [Test]
    public void FeeCollector_CompleteWorkflowThroughParser_WorksCorrectly()
    {
        // This test mirrors the Go TestFeeCollector test case through the parser
        Address batchPosterAddr = ArbosAddresses.BatchPosterAddress;
        Address collectorAddr = new("0x1111111111111111111111111111111111111111");
        Address impostorAddr = new("0x2222222222222222222222222222222222222222");

        // Initial fee collector should be the batch poster address itself
        byte[] getFeeCollectorInput = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_getFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            batchPosterAddr
        );
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getFeeCollectorId, out PrecompileHandler? getFeeCollectorHandler);
        exists.Should().BeTrue();

        byte[] initialResult = getFeeCollectorHandler!(_context, getFeeCollectorInput);
        Address initialCollector = new(initialResult[(Hash256.Size - Address.Size)..]);
        initialCollector.Should().Be(ArbosAddresses.BatchPosterPayToAddress);

        // Set fee collector to collectorAddr (as batch poster)
        ArbitrumPrecompileExecutionContext batchPosterContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(batchPosterAddr);

        byte[] setFeeCollectorInput = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_setFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            batchPosterAddr, collectorAddr
        );
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_setFeeCollectorId, out PrecompileHandler? setFeeCollectorHandler);
        exists.Should().BeTrue();

        byte[] setResult = setFeeCollectorHandler!(batchPosterContext, setFeeCollectorInput);
        setResult.Should().BeEmpty(); // No return value

        // Fee collector should now be collectorAddr
        byte[] newResult = getFeeCollectorHandler!(batchPosterContext, getFeeCollectorInput);
        Address newCollector = new(newResult[(Hash256.Size - Address.Size)..]);
        newCollector.Should().Be(collectorAddr);

        // Trying to set someone else's collector should fail
        ArbitrumPrecompileExecutionContext impostorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(impostorAddr);

        byte[] unauthorizedInput = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_setFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            batchPosterAddr, impostorAddr
        );
        Action unauthorizedAction = () => setFeeCollectorHandler!(impostorContext, unauthorizedInput);
        ArbitrumPrecompileException exception = unauthorizedAction.Should().Throw<ArbitrumPrecompileException>().Which;
        ArbitrumPrecompileException expected = ArbitrumPrecompileException.CreateFailureException("only a batch poster (or its fee collector / chain owner) may change its fee collector");
        exception.Should().BeEquivalentTo(expected, o => o.ForArbitrumPrecompileException());

        // But the fee collector can replace itself
        ArbitrumPrecompileExecutionContext collectorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(collectorAddr);

        byte[] collectorInput = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_setFeeCollectorId].AbiFunctionDescription.GetCallInfo().Signature,
            batchPosterAddr, impostorAddr
        );
        Action collectorAction = () => setFeeCollectorHandler!(collectorContext, collectorInput);
        collectorAction.Should().NotThrow();

        // Verify the fee collector was updated
        byte[] finalResult = getFeeCollectorHandler!(collectorContext, getFeeCollectorInput);
        Address finalCollector = new(finalResult[(Hash256.Size - Address.Size)..]);
        finalCollector.Should().Be(impostorAddr);
    }

    [Test]
    public void TxBaseFee_DeprecatedBehaviorThroughParser_AlwaysReturnsZeroAndIgnoresSets()
    {
        // This test mirrors the Go TestTxBaseFee test case through the parser
        Address aggregatorAddr = new("0x3333333333333333333333333333333333333333");
        UInt256 targetFee = new(973);

        ArbitrumPrecompileExecutionContext aggregatorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(aggregatorAddr);

        // Initial result should be zero
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_getTxBaseFeeId, out PrecompileHandler? getTxBaseFeeHandler);
        exists.Should().BeTrue();

        byte[] getTxBaseFeeCalldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_getTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            aggregatorAddr
        );

        byte[] initialResult = getTxBaseFeeHandler!(_context, getTxBaseFeeCalldata);
        UInt256 initialFee = new(initialResult, isBigEndian: true);
        initialFee.Should().Be(UInt256.Zero);

        // Set base fee to value -- should be ignored (no-op)
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(_setTxBaseFeeId, out PrecompileHandler? setTxBaseFeeHandler);
        exists.Should().BeTrue();

        byte[] setTxBaseFeeCalldata = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            ArbAggregatorParser.PrecompileFunctionDescription[_setTxBaseFeeId].AbiFunctionDescription.GetCallInfo().Signature,
            aggregatorAddr, targetFee
        );

        Action setAction = () => setTxBaseFeeHandler!(aggregatorContext, setTxBaseFeeCalldata);
        setAction.Should().NotThrow();

        // Base fee should still be zero
        byte[] finalResult = getTxBaseFeeHandler!(_context, getTxBaseFeeCalldata);
        UInt256 finalFee = new(finalResult, isBigEndian: true);
        finalFee.Should().Be(UInt256.Zero);
    }
}
