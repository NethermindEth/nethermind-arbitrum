using FluentAssertions;
using Nethermind.Abi;
using Nethermind.Arbitrum.Arbos;
using Nethermind.Arbitrum.Precompiles;
using Nethermind.Arbitrum.Precompiles.Parser;
using Nethermind.Arbitrum.Test.Infrastructure;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Core.Test.Builders;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Arbitrum.Test.Precompiles.Parser;

[TestFixture]
public class ArbAggregatorParserTests
{
    private IWorldState _worldState = null!;
    private ArbosState _arbosState = null!;
    private ArbitrumPrecompileExecutionContext _context = null!;
    private ArbAggregatorParser _parser = null!;
    private Address _chainOwner = null!;

    [SetUp]
    public void SetUp()
    {
        (_worldState, _) = ArbOSInitialization.Create();
        _arbosState = ArbosState.OpenArbosState(_worldState, new SystemBurner(), LimboLogs.Instance.GetClassLogger<ArbosState>());

        _chainOwner = TestItem.AddressA;
        _arbosState.ChainOwners.Add(_chainOwner);

        _context = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(_chainOwner);

        _parser = ArbAggregatorParser.Instance;
    }

    [Test]
    public void GetPreferredAggregator_WithValidInput_ReturnsCorrectResult()
    {
        Address testAddress = TestItem.AddressB;
        byte[] input = ParserTestHelpers.CreateMethodCallDataWithAddress("getPreferredAggregator(address)", testAddress);

        byte[] result = _parser.RunAdvanced(_context, input);

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
        byte[] input = ParserTestHelpers.CreateMethodCallData("getDefaultAggregator()");

        byte[] result = _parser.RunAdvanced(_context, input);

        Address decodedResult = new(result[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(ArbosAddresses.BatchPosterAddress);
    }

    [Test]
    public void GetBatchPosters_WithValidInput_ReturnsCorrectResult()
    {
        byte[] input = ParserTestHelpers.CreateMethodCallData("getBatchPosters()");

        byte[] result = _parser.RunAdvanced(_context, input);

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
        byte[] input = ParserTestHelpers.CreateMethodCallDataWithAddress("addBatchPoster(address)", newBatchPoster);

        byte[] result = _parser.RunAdvanced(_context, input);

        result.Should().BeEmpty(); // No return value

        // Verify the batch poster was added
        byte[] getBatchPostersInput = ParserTestHelpers.CreateMethodCallData("getBatchPosters()");
        byte[] getBatchPostersResult = _parser.RunAdvanced(_context, getBatchPostersInput);

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
        byte[] input = ParserTestHelpers.CreateMethodCallDataWithAddress("addBatchPoster(address)", newBatchPoster);

        Action action = () => _parser.RunAdvanced(nonOwnerContext, input);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("must be called by chain owner");
    }

    [Test]
    public void GetFeeCollector_WithValidInput_ReturnsCorrectResult()
    {
        Address batchPoster = ArbosAddresses.BatchPosterAddress;
        byte[] input = ParserTestHelpers.CreateMethodCallDataWithAddress("getFeeCollector(address)", batchPoster);

        byte[] result = _parser.RunAdvanced(_context, input);

        Address decodedResult = new(result[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(ArbosAddresses.BatchPosterPayToAddress);
    }

    [Test]
    public void SetFeeCollector_WithValidInput_UpdatesFeeCollector()
    {
        // First, add a new batch poster
        Address newBatchPoster = TestItem.AddressF;
        byte[] addInput = ParserTestHelpers.CreateMethodCallDataWithAddress("addBatchPoster(address)", newBatchPoster);
        _parser.RunAdvanced(_context, addInput);

        // Set fee collector
        Address newFeeCollector = new("0x1111111111111111111111111111111111111111");
        byte[] setInput = EncodeMethodCall("setFeeCollector(address,address)", newBatchPoster, newFeeCollector);

        byte[] result = _parser.RunAdvanced(_context, setInput);

        result.Should().BeEmpty(); // No return value

        // Verify the fee collector was updated
        byte[] getInput = ParserTestHelpers.CreateMethodCallDataWithAddress("getFeeCollector(address)", newBatchPoster);
        byte[] getResult = _parser.RunAdvanced(_context, getInput);

        Address decodedResult = new(getResult[(Hash256.Size - Address.Size)..]);
        decodedResult.Should().Be(newFeeCollector);
    }

    [Test]
    public void GetTxBaseFee_WithValidInput_ReturnsZero()
    {
        Address aggregator = new("0x2222222222222222222222222222222222222222");
        byte[] input = ParserTestHelpers.CreateMethodCallDataWithAddress("getTxBaseFee(address)", aggregator);

        byte[] result = _parser.RunAdvanced(_context, input);

        UInt256 decodedResult = new(result, isBigEndian: true);
        decodedResult.Should().Be(UInt256.Zero);
    }

    [Test]
    public void SetTxBaseFee_WithValidInput_DoesNotThrow()
    {
        Address aggregator = new("0x3333333333333333333333333333333333333333");
        UInt256 feeInL1Gas = new(1000);
        byte[] input = EncodeMethodCall("setTxBaseFee(address,uint256)", aggregator, feeInL1Gas);

        byte[] result = _parser.RunAdvanced(_context, input);

        result.Should().BeEmpty(); // No return value
    }

    [Test]
    public void RunAdvanced_WithInvalidMethodId_ThrowsException()
    {
        byte[] invalidInput = [0xFF, 0xFF, 0xFF, 0xFF]; // Invalid method ID

        Action action = () => _parser.RunAdvanced(_context, invalidInput);

        action.Should().Throw<ArgumentException>()
            .WithMessage("Invalid precompile method ID: *");
    }

    [Test]
    public void FeeCollector_CompleteWorkflowThroughParser_WorksCorrectly()
    {
        // This test mirrors the Go TestFeeCollector test case through the parser
        Address batchPosterAddr = ArbosAddresses.BatchPosterAddress;
        Address collectorAddr = new("0x1111111111111111111111111111111111111111");
        Address impostorAddr = new("0x2222222222222222222222222222222222222222");

        // Initial fee collector should be the batch poster address itself
        byte[] getFeeCollectorInput = ParserTestHelpers.CreateMethodCallDataWithAddress("getFeeCollector(address)", batchPosterAddr);
        byte[] initialResult = _parser.RunAdvanced(_context, getFeeCollectorInput);
        Address initialCollector = new(initialResult[(Hash256.Size - Address.Size)..]);
        initialCollector.Should().Be(ArbosAddresses.BatchPosterPayToAddress);

        // Set fee collector to collectorAddr (as batch poster)
        ArbitrumPrecompileExecutionContext batchPosterContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(batchPosterAddr);

        byte[] setFeeCollectorInput = EncodeMethodCall("setFeeCollector(address,address)", batchPosterAddr, collectorAddr);
        byte[] setResult = _parser.RunAdvanced(batchPosterContext, setFeeCollectorInput);
        setResult.Should().BeEmpty(); // No return value

        // Fee collector should now be collectorAddr
        byte[] newResult = _parser.RunAdvanced(_context, getFeeCollectorInput);
        Address newCollector = new(newResult[(Hash256.Size - Address.Size)..]);
        newCollector.Should().Be(collectorAddr);

        // Trying to set someone else's collector should fail
        ArbitrumPrecompileExecutionContext impostorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(impostorAddr);

        byte[] unauthorizedInput = EncodeMethodCall("setFeeCollector(address,address)", batchPosterAddr, impostorAddr);
        Action unauthorizedAction = () => _parser.RunAdvanced(impostorContext, unauthorizedInput);
        unauthorizedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("only a batch poster (or its fee collector / chain owner) may change its fee collector");

        // But the fee collector can replace itself
        ArbitrumPrecompileExecutionContext collectorContext = new PrecompileTestContextBuilder(_worldState, 1_000_000)
            .WithArbosState()
            .WithBlockExecutionContext(Build.A.BlockHeader.TestObject)
            .WithReleaseSpec()
            .WithCaller(collectorAddr);

        byte[] collectorInput = EncodeMethodCall("setFeeCollector(address,address)", batchPosterAddr, impostorAddr);
        Action collectorAction = () => _parser.RunAdvanced(collectorContext, collectorInput);
        collectorAction.Should().NotThrow();

        // Verify the fee collector was updated
        byte[] finalResult = _parser.RunAdvanced(_context, getFeeCollectorInput);
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
        byte[] getTxBaseFeeInput = ParserTestHelpers.CreateMethodCallDataWithAddress("getTxBaseFee(address)", aggregatorAddr);
        byte[] initialResult = _parser.RunAdvanced(_context, getTxBaseFeeInput);
        UInt256 initialFee = new(initialResult, isBigEndian: true);
        initialFee.Should().Be(UInt256.Zero);

        // Set base fee to value -- should be ignored (no-op)
        byte[] setTxBaseFeeInput = EncodeMethodCall("setTxBaseFee(address,uint256)", aggregatorAddr, targetFee);
        Action setAction = () => _parser.RunAdvanced(aggregatorContext, setTxBaseFeeInput);
        setAction.Should().NotThrow();

        // Base fee should still be zero
        byte[] finalResult = _parser.RunAdvanced(_context, getTxBaseFeeInput);
        UInt256 finalFee = new(finalResult, isBigEndian: true);
        finalFee.Should().Be(UInt256.Zero);
    }

    private static byte[] EncodeMethodCall(string signature, params object[] parameters)
    {
        uint methodId = MethodIdHelper.GetMethodId(signature);
        byte[] methodIdBytes = methodId.ToBigEndianByteArray();

        if (parameters.Length == 0) return methodIdBytes;

        // Parse signature to get parameter types
        string parametersPart = signature[(signature.IndexOf('(') + 1)..];
        parametersPart = parametersPart[..^1];

        if (string.IsNullOrEmpty(parametersPart)) return methodIdBytes;

        string[] paramTypeStrings = parametersPart.Split(',');
        AbiType[] paramTypes = paramTypeStrings.Select(s => s.Trim() switch
        {
            "address" => AbiType.Address,
            "uint256" => AbiType.UInt256,
            "bool" => AbiType.Bool,
            _ => throw new ArgumentException($"Unsupported ABI type: {s.Trim()}")
        }).ToArray<AbiType>();

        byte[] encodedParams = AbiEncoder.Instance.Encode(
            AbiEncodingStyle.None,
            new AbiSignature("method", paramTypes),
            parameters
        );

        return methodIdBytes.Concat(encodedParams).ToArray();
    }
}
