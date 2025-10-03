using System.Buffers.Binary;
using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
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

    // ABI signatures for ArbAggregator methods
    private static readonly AbiSignature GetPreferredAggregatorSignature = new("getPreferredAggregator", AbiType.Address);
    private static readonly AbiSignature GetDefaultAggregatorSignature = new("getDefaultAggregator");
    private static readonly AbiSignature GetBatchPostersSignature = new("getBatchPosters");
    private static readonly AbiSignature AddBatchPosterSignature = new("addBatchPoster", AbiType.Address);
    private static readonly AbiSignature GetFeeCollectorSignature = new("getFeeCollector", AbiType.Address);
    private static readonly AbiSignature SetFeeCollectorSignature = new("setFeeCollector", AbiType.Address, AbiType.Address);
    private static readonly AbiSignature GetTxBaseFeeSignature = new("getTxBaseFee", AbiType.Address);
    private static readonly AbiSignature SetTxBaseFeeSignature = new("setTxBaseFee", AbiType.Address, AbiType.UInt256);

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
        byte[] input = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetPreferredAggregatorSignature, testAddress);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(GetPreferredAggregatorSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, input);

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
        byte[] input = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetDefaultAggregatorSignature);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(GetDefaultAggregatorSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, input);

        Address decodedResult = new(result[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(ArbosAddresses.BatchPosterAddress);
    }

    [Test]
    public void GetBatchPosters_WithValidInput_ReturnsCorrectResult()
    {
        byte[] input = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetBatchPostersSignature);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(GetBatchPostersSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? handler);
        exists.Should().BeTrue();

        byte[] result = handler!(_context, input);

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
        byte[] input = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, AddBatchPosterSignature, newBatchPoster);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(AddBatchPosterSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? addBatchPosterHandler);
        exists.Should().BeTrue();

        byte[] result = addBatchPosterHandler!(_context, input);

        result.Should().BeEmpty(); // No return value

        // Verify the batch poster was added
        byte[] getBatchPostersInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetBatchPostersSignature);
        uint getBatchPostersMethodId = BinaryPrimitives.ReadUInt32BigEndian(GetBatchPostersSignature.Address);
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(getBatchPostersMethodId, out PrecompileHandler? getBatchPostersHandler);
        exists.Should().BeTrue();

        byte[] getBatchPostersResult = getBatchPostersHandler!(_context, getBatchPostersInput);

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
        byte[] input = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, AddBatchPosterSignature, newBatchPoster);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(AddBatchPosterSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? addBatchPosterHandler);
        exists.Should().BeTrue();

        Action action = () => addBatchPosterHandler!(nonOwnerContext, input);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("must be called by chain owner");
    }

    [Test]
    public void GetFeeCollector_WithValidInput_ReturnsCorrectResult()
    {
        Address batchPoster = ArbosAddresses.BatchPosterAddress;
        byte[] input = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetFeeCollectorSignature, batchPoster);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(GetFeeCollectorSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? getFeeCollectorHandler);
        exists.Should().BeTrue();

        byte[] result = getFeeCollectorHandler!(_context, input);

        Address decodedResult = new(result[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(ArbosAddresses.BatchPosterPayToAddress);
    }

    [Test]
    public void SetFeeCollector_WithValidInput_UpdatesFeeCollector()
    {
        // First, add a new batch poster
        Address newBatchPoster = TestItem.AddressF;
        byte[] addInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, AddBatchPosterSignature, newBatchPoster);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(AddBatchPosterSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? addBatchPosterHandler);
        exists.Should().BeTrue();

        byte[] result = addBatchPosterHandler!(_context, addInput);

        // Set fee collector
        Address newFeeCollector = new("0x1111111111111111111111111111111111111111");
        byte[] setInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, SetFeeCollectorSignature, newBatchPoster, newFeeCollector);

        methodId = BinaryPrimitives.ReadUInt32BigEndian(SetFeeCollectorSignature.Address);
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? setFeeCollectorHandler);
        exists.Should().BeTrue();

        result.Should().BeEmpty(); // No return value

        // Verify the fee collector was updated
        byte[] getInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetFeeCollectorSignature, newBatchPoster);
        methodId = BinaryPrimitives.ReadUInt32BigEndian(GetFeeCollectorSignature.Address);
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? getFeeCollectorHandler);
        exists.Should().BeTrue();

        byte[] getResult = getFeeCollectorHandler!(_context, getInput);

        Address decodedResult = new(getResult[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(newFeeCollector);
    }

    [Test]
    public void GetTxBaseFee_WithValidInput_ReturnsZero()
    {
        Address aggregator = new("0x2222222222222222222222222222222222222222");
        byte[] input = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetTxBaseFeeSignature, aggregator);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(GetTxBaseFeeSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? getTxBaseFeeHandler);
        exists.Should().BeTrue();

        byte[] result = getTxBaseFeeHandler!(_context, input);

        UInt256 decodedResult = new(result, isBigEndian: true);
        decodedResult.Should().Be(UInt256.Zero);
    }

    [Test]
    public void SetTxBaseFee_WithValidInput_DoesNotThrow()
    {
        Address aggregator = new("0x3333333333333333333333333333333333333333");
        UInt256 feeInL1Gas = new(1000);
        byte[] input = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, SetTxBaseFeeSignature, aggregator, feeInL1Gas);

        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(SetTxBaseFeeSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? setTxBaseFeeHandler);
        exists.Should().BeTrue();

        byte[] result = setTxBaseFeeHandler!(_context, input);

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
        byte[] getFeeCollectorInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetFeeCollectorSignature, batchPosterAddr);
        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(GetFeeCollectorSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? getFeeCollectorHandler);
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

        byte[] setFeeCollectorInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, SetFeeCollectorSignature, batchPosterAddr, collectorAddr);
        methodId = BinaryPrimitives.ReadUInt32BigEndian(SetFeeCollectorSignature.Address);
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? setFeeCollectorHandler);
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

        byte[] unauthorizedInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, SetFeeCollectorSignature, batchPosterAddr, impostorAddr);
        Action unauthorizedAction = () => setFeeCollectorHandler!(impostorContext, unauthorizedInput);
        unauthorizedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("only a batch poster (or its fee collector / chain owner) may change its fee collector");

        // But the fee collector can replace itself
        ArbitrumPrecompileExecutionContext collectorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(collectorAddr);

        byte[] collectorInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.IncludeSignature, SetFeeCollectorSignature, batchPosterAddr, impostorAddr);
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
        byte[] getTxBaseFeeInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, GetTxBaseFeeSignature, aggregatorAddr);
        uint methodId = BinaryPrimitives.ReadUInt32BigEndian(GetTxBaseFeeSignature.Address);
        bool exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? getTxBaseFeeHandler);
        exists.Should().BeTrue();

        byte[] initialResult = getTxBaseFeeHandler!(_context, getTxBaseFeeInput);
        UInt256 initialFee = new(initialResult, isBigEndian: true);
        initialFee.Should().Be(UInt256.Zero);

        // Set base fee to value -- should be ignored (no-op)
        byte[] setTxBaseFeeInput = AbiEncoder.Instance.Encode(AbiEncodingStyle.None, SetTxBaseFeeSignature, aggregatorAddr, targetFee);
        methodId = BinaryPrimitives.ReadUInt32BigEndian(SetTxBaseFeeSignature.Address);
        exists = ArbAggregatorParser.PrecompileImplementation.TryGetValue(methodId, out PrecompileHandler? setTxBaseFeeHandler);
        exists.Should().BeTrue();

        Action setAction = () => setTxBaseFeeHandler!(aggregatorContext, setTxBaseFeeInput);
        setAction.Should().NotThrow();

        // Base fee should still be zero
        byte[] finalResult = getTxBaseFeeHandler!(_context, getTxBaseFeeInput);
        UInt256 finalFee = new(finalResult, isBigEndian: true);
        finalFee.Should().Be(UInt256.Zero);
    }
}
